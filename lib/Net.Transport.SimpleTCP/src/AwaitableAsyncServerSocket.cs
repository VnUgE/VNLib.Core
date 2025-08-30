/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: AwaitableAsyncServerSocket.cs 
*
* AwaitableAsyncServerSocket.cs is part of VNLib.Net.Transport.SimpleTCP which 
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.SimpleTCP is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.SimpleTCP is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Net.Sockets;
using System.IO.Pipelines;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using System.Runtime.InteropServices;

using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Transport.Tcp
{
    internal sealed class AwaitableAsyncServerSocket :
        ITcpConnectionDescriptor,
        IDisposable,
        ISocketIo,
        IReusable
    {
        private static readonly bool IsWindows = OperatingSystem.IsWindows();

        private Socket? _socket;

        public readonly SocketPipeLineWorker SocketWorker;
        private readonly bool _reuseSocket;
        private readonly AwaitableValueSocketEventArgs _recvArgs = new();
        private readonly AwaitableValueSocketEventArgs _allArgs = new();

        private Task _sendTask = Task.CompletedTask;
        private Task _recvTask = Task.CompletedTask;

        public AwaitableAsyncServerSocket(bool reuseSocket, PipeOptions options) : base()
        {
            _reuseSocket = reuseSocket && IsWindows;    //Reuse only available on Windows
            SocketWorker = new(options);

            //Set reuse flags now
            _recvArgs.DisconnectReuseSocket = _reuseSocket;
            _allArgs.DisconnectReuseSocket = _reuseSocket;
        }


        public async ValueTask<SocketError> AcceptAsync(Socket serverSocket, int recvBuffSize, int sendBuffSize)
        {
            /*
             * WSA allows the kernel to wait for data during an accept before 
             * invoking user-space callback to save a kernel trap. Since this is 
             * only available on Windows
             */
            if (IsWindows)
            {
                //get buffer from the pipe to write initial accept data to
                Memory<byte> buffer = SocketWorker.GetMemory(recvBuffSize);
                _allArgs.SetBuffer(buffer);
            }

            //Begin the accept, and attempt to reuse the socket if available
            SocketError error = await _allArgs.AcceptAsync(serverSocket, _socket);

            if (error == SocketError.Success)
            {
                //Store socket on success
                _socket = _allArgs.AcceptSocket!;

                //It is safe to start the pipeline now
                _sendTask = SocketWorker.SendDoWorkAsync(this, sendBuffSize);

                /*
                 * Passing the number of transferred bytes to the recv task will cause accepted 
                 * data to be published (if zero thats fine too)
                 */
                _recvTask = SocketWorker.RecvDoWorkAsync(this, _allArgs.BytesTransferred, recvBuffSize);
            }

            _allArgs.AcceptSocket = null;

            return error;
        }


        public async ValueTask<SocketError> CloseConnectionAsync()
        {
            _ = _socket ?? throw new InvalidOperationException("Socket is not connected");

            //Wait for the pipeline to end before disconnecting the socket
            await SocketWorker.ShutDownClientPipeAsync();

            //Wait for the send task to complete before disconnecting
            await _sendTask.ConfigureAwait(false);

            //Disconnect the socket
            SocketError error = await _allArgs.DisconnectAsync(_socket);

            /*
             * Release hooks will take care of socket cleanup 
             * if it's required.
             */

            //Wait for recv to complete
            await _recvTask.ConfigureAwait(false);

            /*
             * Sockets can be reused as much as possible on Windows. If the socket
             * failes to disconnect cleanly, the release function won't clean it up
             * so it needs to be cleaned up here so at least our args instance
             * can be reused.
             */
            if (_reuseSocket && error != SocketError.Success)
            {
                _socket.Dispose();
                _socket = null;
            }

            return error;
        }

        ///<inheritdoc/>
        ValueTask<int> ISocketIo.SendAsync(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags)
        {
            //Socket must always be defined as this function is called from the pipeline
            Debug.Assert(_socket != null, "Socket is not connected");

            //Get memory from readonly memory so it can be sent using asyncargs
            Memory<byte> asMemory = MemoryMarshal.AsMemory(buffer);

            return _allArgs.SendAsync(_socket, asMemory, socketFlags);
        }

        ///<inheritdoc/>
        ValueTask<int> ISocketIo.ReceiveAsync(Memory<byte> buffer, SocketFlags socketFlags)
        {
            //Socket must always be defined as this function is called from the pipeline
            Debug.Assert(_socket != null, "Socket is not connected");

            return _recvArgs.ReceiveAsync(_socket, buffer, socketFlags);
        }

        void IReusable.Prepare()
        {
            Debug.Assert(_socket == null || _reuseSocket, "Exepcted stale socket to be NULL on non-Windows platform");

            _allArgs.Prepare();
            _recvArgs.Prepare();
            SocketWorker.Prepare();
        }

        bool IReusable.Release()
        {
            //Release should never be called before the pipeline is complete
            Debug.Assert(_sendTask.IsCompleted, "Socket was released before send task completed");
            Debug.Assert(_recvTask.IsCompleted, "Socket was released before recv task completed");

            _allArgs.Release();
            _recvArgs.Release();

            //if the socket is still 'connected' (or no reuse), dispose it and clear the accept socket
            if (_socket?.Connected == true || !_reuseSocket)
            {
                _socket?.Dispose();
                _socket = null;
            }

            return SocketWorker.Release();
        }

        ///<inheritdoc/>
        Stream ITcpConnectionDescriptor.GetStream() => SocketWorker.NetworkStream;

        ///<inheritdoc/>
        void ITcpConnectionDescriptor.GetEndpoints(out IPEndPoint localEndpoint, out IPEndPoint remoteEndpoint)
        {
            localEndpoint = (_socket!.LocalEndPoint as IPEndPoint)!;
            remoteEndpoint = (_socket!.RemoteEndPoint as IPEndPoint)!;
        }

        ///<inheritdoc/>
        public void Dispose()
        {
            //Dispose the socket if its set
            _socket?.Dispose();
            _socket = null;

            _allArgs.Dispose();
            _recvArgs.Dispose();

            //Cleanup socket worker
            SocketWorker.DisposeInternal();
        }

        private sealed class AwaitableValueSocketEventArgs :
            SocketAsyncEventArgs,
            IValueTaskSource<SocketError>,
            IValueTaskSource<int>
        {
            private ManualResetValueTaskSourceCore<int> AsyncTaskCore;

            public void Prepare()
            {
                SocketError = SocketError.Success;
                SocketFlags = SocketFlags.None;
            }

            public void Release()
            {
                //Make sure any operation specific data is cleared
                AcceptSocket = null;
                UserToken = null;
                SetBuffer(default);
            }

            protected override void OnCompleted(SocketAsyncEventArgs e)
            {

                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                    case SocketAsyncOperation.Send:

                        //Clear buffer after async op
                        SetBuffer(default);

                        //If the operation was successful, set the number of bytes transferred
                        if (SocketError == SocketError.Success)
                        {
                            AsyncTaskCore.SetResult(e.BytesTransferred);
                        }
                        else
                        {
                            AsyncTaskCore.SetException(new SocketException((int)SocketError));
                        }
                        break;

                    case SocketAsyncOperation.Accept:
                        AsyncTaskCore.SetResult((int)e.SocketError);
                        break;

                    case SocketAsyncOperation.Disconnect:
                        AsyncTaskCore.SetResult((int)e.SocketError);
                        break;

                    default:
                        AsyncTaskCore.SetException(new InvalidOperationException("Invalid socket operation"));
                        break;
                }

                //Clear flags/errors on completion
                SocketError = SocketError.Success;
                SocketFlags = SocketFlags.None;
            }

            /// <summary>
            /// Begins an asynchronous accept operation on the current (bound) socket 
            /// </summary>
            /// <param name="sock">The server socket to accept the connection</param>
            /// <param name="reusedSocket">Optional socket to reuse for the accept operation</param>
            /// <returns>True if the IO operation is pending</returns>
            public ValueTask<SocketError> AcceptAsync(Socket sock, Socket? reusedSocket)
            {
                OnBeforeOperation(SocketFlags.None);

                AcceptSocket = reusedSocket;

                return sock.AcceptAsync(this)
                    ? new ValueTask<SocketError>(this, AsyncTaskCore.Version)
                    : ValueTask.FromResult(SocketError);
            }

            /// <summary>
            /// Begins an async disconnect operation on a currentl connected socket
            /// </summary>
            /// <returns>True if the operation is pending</returns>
            public ValueTask<SocketError> DisconnectAsync(Socket serverSock)
            {
                OnBeforeOperation(SocketFlags.None);

                return serverSock.DisconnectAsync(this)
                    ? new ValueTask<SocketError>(this, AsyncTaskCore.Version)
                    : ValueTask.FromResult(SocketError);
            }


            public ValueTask<int> SendAsync(Socket socket, Memory<byte> buffer, SocketFlags flags)
            {
                OnBeforeOperation(flags);

                SetBuffer(buffer);

                if (socket.SendAsync(this))
                {
                    //Async send
                    return new ValueTask<int>(this, AsyncTaskCore.Version);
                }

                // Syncronous send
                return GetSyncTxRxResult();
            }

            public ValueTask<int> ReceiveAsync(Socket socket, Memory<byte> buffer, SocketFlags flags)
            {
                OnBeforeOperation(flags);

                SetBuffer(buffer);

                if (socket.ReceiveAsync(this))
                {
                    //Async receive
                    return new ValueTask<int>(this, AsyncTaskCore.Version);
                }

                // Syncronous receive
                return GetSyncTxRxResult();
            }

            private ValueTask<int> GetSyncTxRxResult()
            {
                return SocketError switch
                {
                    SocketError.Success => ValueTask.FromResult(BytesTransferred),
                    _ => ValueTask.FromException<int>(new SocketException((int)SocketError))
                };
            }

            private void OnBeforeOperation(SocketFlags flags)
            {
                //Reset the task source, flags, and internal error state
                AsyncTaskCore.Reset();
                SocketError = SocketError.Success;
                SocketFlags = flags;
            }

            ///<inheritdoc/>
            public SocketError GetResult(short token) => (SocketError)AsyncTaskCore.GetResult(token);

            ///<inheritdoc/>
            public ValueTaskSourceStatus GetStatus(short token) => AsyncTaskCore.GetStatus(token);

            ///<inheritdoc/>
            public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
                => AsyncTaskCore.OnCompleted(continuation, state, token, flags);

            ///<inheritdoc/>
            int IValueTaskSource<int>.GetResult(short token) => AsyncTaskCore.GetResult(token);
        }
    }
}
