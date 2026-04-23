/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: SocketIoManager.cs 
*
* SocketIoManager.cs is part of VNLib.Net.Transport.Tcp which 
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.Tcp is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.Tcp is distributed in the hope that it will be useful,
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
using System.Runtime.InteropServices;

using VNLib.Utils;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Transport.Tcp.Internal
{
    /// <summary>
    /// A reusable socket IO connection manager and public connection 
    /// descriptor. Provides high performance life cycle management of socket
    /// lifecycle, memory management, and IO processing.
    /// </summary>
    internal sealed class SocketIoManager :
        VnDisposeable,
        ITcpConnectionDescriptor,
        ISocketIo,
        IReusable
    {        

        public readonly SocketDataProcessor SocketWorker;        

        private readonly bool _reuseSocket;
        private readonly AwaitableValueSocketEventArgs _recvArgs = new();
        private readonly AwaitableValueSocketEventArgs _sendArgs = new();

        private Socket? _socket;

        private Task _sendTask = Task.CompletedTask;
        private Task _recvTask = Task.CompletedTask;       

        public SocketIoManager(bool reuseSocket, PipeOptions options)
        {
            _reuseSocket = reuseSocket;
            SocketWorker = new(options);

            //Set reuse flags now
            _recvArgs.DisconnectReuseSocket = _reuseSocket;
            _sendArgs.DisconnectReuseSocket = _reuseSocket;
        }

        /// <inheritdoc/>
        public void Prepare()
        {
            Debug.Assert(_socket == null || _reuseSocket, "Expected stale socket to be NULL on when socket reuse is not supported platform");

            _sendArgs.Prepare();
            _recvArgs.Prepare();
            SocketWorker.Prepare();
        }

        /// <inheritdoc/>
        public bool Release()
        {
            //Release should never be called before the pipeline is complete
            Debug.Assert(_sendTask.IsCompleted, "Socket was released before send task completed");
            Debug.Assert(_recvTask.IsCompleted, "Socket was released before recv task completed");

            _sendArgs.Release();
            _recvArgs.Release();

            /*
             * Sockets may be reused on some platforms, by the Accept() loop if desired. Our 
             * job is to hold on to it if were allowed. Otherwise this is the escape hatch to 
             * clean up any invalid state.
             * 
             * If the socket is somehow still "connected" then we cannot reuse it as it's an error
             * and we need to clean it up. Otherwise if the socket is not-null and we aren't allowed 
             * to reuse it, then we must get dispose of it.
             */
            if (_socket?.Connected == true || !_reuseSocket)
            {
                _socket?.Dispose();
                _socket = null;
            }

            return SocketWorker.Release();
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            //Dispose the socket if its set
            _socket?.Dispose();
            _socket = null;

            _sendArgs.Dispose();
            _recvArgs.Dispose();

            //Cleanup socket worker
            SocketWorker.DisposeInternal();
        }

        /// <summary>
        /// The socket accepted by the server, which will be used for the lifetime of the connection. 
        /// </summary>
        public Socket? Socket => _socket;

        /// <summary>
        /// Gets a buffer of the desired size to be used for prereading data from the 
        /// socket after an accept operation. You must remember to call AcceptedSocket 
        /// with the number of bytes transferred during the accept operation so that the 
        /// pre-read data can be published to the pipeline.
        /// </summary>
        /// <param name="bufferSize">The size of the buffer to allocate for prereading data.</param>
        /// <returns>A memory buffer that can be used for prereading data from the socket.</returns>
        public Memory<byte> GetPrereadBuffer(int bufferSize) 
            => SocketWorker.Receiver.ReceiveBuffer.GetMemory(bufferSize);

        /// <summary>
        /// Configures the 
        /// </summary>
        /// <param name="sock"></param>
        /// <param name="bytesTransferred"></param>
        public void AcceptedSocket(Socket sock, int bytesTransferred)
        {
            /*
             * Expected to be called from an internal api only, no need 
             * for a runtime check.
             */          
            Debug.Assert(bytesTransferred >= 0, "Bytes transferred cannot be negative");

            _socket = sock;

            /*
             * Advance the buffer if any data was written. When the worker task is started
             * it will first flush those bytes to the consumer.
             */
            SocketWorker.Receiver.ReceiveBuffer.Advance(bytesTransferred);
        }
      
        /// <summary>
        /// Begins the background send and receive loops for the accepted socket. This should only be 
        /// called once per accept, and only after a successful accept operation. 
        /// </summary>
        /// <param name="recvBuffSize">A hint of the internal socket's receive buffer size</param>
        /// <param name="sendBuffSize">A hint to the pipeline for the socket's send buffer size</param>
        public void StartPipeline(int recvBuffSize, int sendBuffSize)
        {
            /*
             * If this method is called, we can assume that a previous accept operation has succeeded 
             * and the AcceptSocket is still valid on the accept args. If so, store the socket for 
             * usage, and clear it's socket value for use within the pipeline.
             */
            Debug.Assert(_socket != null, "Socket is not connected");

            //It is safe to start the pipeline now
            _sendTask = SocketWorker.Sender.DoWorkAsync(this, sendBuffSize);

            /*
             * Passing the number of transferred bytes to the recv task will cause accepted 
             * data to be published (if zero thats fine too)
             */
            _recvTask = SocketWorker.Receiver.DoWorkAsync(this, recvBuffSize);
        }

        /// <summary>
        /// Stops the pipeline and attempts to gracefully disconnect the socket. If the disconnect
        /// fails and socket reuse is not enabled, the socket will be disposed. If reuse is enabled, 
        /// the socket will be left open for reuse by future accepts, but any failure to disconnect will 
        /// cause the socket to be disposed to prevent reuse of a potentially bad socket.
        /// </summary>
        /// <returns>A task that represents the asynchronous close operation. The value of the TResult parameter contains the socket error. </returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async ValueTask<SocketError> CloseConnectionAsync()
        {
            _ = _socket ?? throw new InvalidOperationException("Socket is not connected");

            // Complete pipelines to stop any pending sends/receives and prevent new ones from starting
            SocketWorker.ShutDownClientPipe();

            //Wait for the send task to complete sending data before issuing a disconnect
            await _sendTask.ConfigureAwait(false);

            //Disconnect the socket
            SocketError error = await AwaitableValueSocketEventArgs.DisconnectAsync(_sendArgs, _socket)
                                    .ConfigureAwait(false);

            //Wait for recv to complete
            await _recvTask.ConfigureAwait(false);

            /*
             * Sockets can be reused as much as possible on Windows. If the socket
             * fails to disconnect cleanly, the release function won't clean it up
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

            return _sendArgs.SendAsync(_socket, asMemory, socketFlags);
        }

        ///<inheritdoc/>
        ValueTask<int> ISocketIo.ReceiveAsync(Memory<byte> buffer, SocketFlags socketFlags)
        {
            //Socket must always be defined as this function is called from the pipeline
            Debug.Assert(_socket != null, "Socket is not connected");

            return _recvArgs.ReceiveAsync(_socket, buffer, socketFlags);
        }


        ///<inheritdoc/>
        Stream ITcpConnectionDescriptor.GetStream() => SocketWorker.NetworkStream;

        ///<inheritdoc/>
        void ITcpConnectionDescriptor.GetEndpoints(out IPEndPoint localEndpoint, out IPEndPoint remoteEndpoint)
        {
            localEndpoint = (_socket!.LocalEndPoint as IPEndPoint)!;
            remoteEndpoint = (_socket!.RemoteEndPoint as IPEndPoint)!;
        }
    }       
}
