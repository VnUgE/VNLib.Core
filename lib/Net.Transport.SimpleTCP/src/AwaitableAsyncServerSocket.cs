/*
* Copyright (c) 2024 Vaughn Nugent
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
        private readonly AwaitableValueSocketEventArgs _recvArgs = new();
        private readonly AwaitableValueSocketEventArgs _allArgs = new();

        private Task _sendTask = Task.CompletedTask;
        private Task _recvTask = Task.CompletedTask;

        public AwaitableAsyncServerSocket(PipeOptions options) : base()
        {
            SocketWorker = new(options);

            //Set reuse flags now
            _recvArgs.DisconnectReuseSocket = IsWindows;
            _allArgs.DisconnectReuseSocket = IsWindows;
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

                //Also on windows we can reuse the previous socket if its set
                _allArgs.AcceptSocket = _socket;
            }

            //Begin the accept
            SocketError error = await _allArgs.AcceptAsync(serverSocket);

            if(error == SocketError.Success)
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
    
            //Clear the buffer reference
            _allArgs.SetBuffer(default);

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

            return error;
        }

        ///<inheritdoc/>
        ValueTask<int> ISocketIo.SendAsync(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags)
        {
            //Socket must always be defined as this function is called from the pipeline
            Debug.Assert(_socket != null, "Socket is not connected");

            //Get memory from readonly memory and set the send buffer
            Memory<byte> asMemory = MemoryMarshal.AsMemory(buffer);
            _allArgs.SetBuffer(asMemory);

            return _allArgs.SendAsync(_socket, socketFlags);
        }

        ///<inheritdoc/>
        ValueTask<int> ISocketIo.ReceiveAsync(Memory<byte> buffer, SocketFlags socketFlags)
        {
            //Socket must always be defined as this function is called from the pipeline
            Debug.Assert(_socket != null, "Socket is not connected");

            _recvArgs.SetBuffer(buffer);
            return _recvArgs.ReceiveAsync(_socket, socketFlags);
        }

        void IReusable.Prepare()
        {
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

            //if the sockeet is connected (or not windows), dispose it and clear the accept socket
            if (_socket?.Connected == true || !IsWindows)
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

                        //If the operation was successfull, set the number of bytes transferred
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
            /// <returns>True if the IO operation is pending</returns>
            public ValueTask<SocketError> AcceptAsync(Socket sock)
            {
                //Store the semaphore in the user token event args 
                SocketError = SocketError.Success;
                SocketFlags = SocketFlags.None;

                //Reset task source
                AsyncTaskCore = default;

                if(sock.AcceptAsync(this))
                {
                    //Async op pending, return the task
                    return new ValueTask<SocketError>(this, AsyncTaskCore.Version);
                }

                //Sync op completed
                return ValueTask.FromResult(SocketError);               
            }          

            /// <summary>
            /// Begins an async disconnect operation on a currentl connected socket
            /// </summary>
            /// <returns>True if the operation is pending</returns>
            public ValueTask<SocketError> DisconnectAsync(Socket serverSock)
            {
                //Clear flags
                SocketError = SocketError.Success;
                
                //Reset task source
                AsyncTaskCore = default;
                
                //accept async
                if (serverSock.DisconnectAsync(this))
                {
                    //Async disconnect
                    return new ValueTask<SocketError>(this, AsyncTaskCore.Version);
                   
                }

                return ValueTask.FromResult(SocketError);
            }
        

            public ValueTask<int> SendAsync(Socket socket, SocketFlags flags)
            {
                //Store the semaphore in the user token event args 
                SocketError = SocketError.Success;
                SocketFlags = flags;

                //Clear task source
                AsyncTaskCore = default;

                if (socket.SendAsync(this))
                {
                    //Async accept
                    return new ValueTask<int>(this, AsyncTaskCore.Version);
                }

                //clear buffer
                SetBuffer(default);

                //Sync send
                return GetSyncTxRxResult();
            }

            public ValueTask<int> ReceiveAsync(Socket socket, SocketFlags flags)
            {
                //Store the semaphore in the user token event args 
                SocketError = SocketError.Success;
                SocketFlags = flags;

                //Clear task source
                AsyncTaskCore = default;

                if (socket.ReceiveAsync(this))
                {
                    //Async accept
                    return new ValueTask<int>(this, AsyncTaskCore.Version);
                }

                //Clear buffer
                SetBuffer(default);

                return GetSyncTxRxResult();
            }

            private ValueTask<int> GetSyncTxRxResult()
            {
                //Sync send
                return SocketError switch
                {
                    SocketError.Success => ValueTask.FromResult(BytesTransferred),
                    _ => ValueTask.FromException<int>(new SocketException((int)SocketError))
                };
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
