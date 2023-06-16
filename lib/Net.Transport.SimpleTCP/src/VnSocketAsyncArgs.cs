/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: VnSocketAsyncArgs.cs 
*
* VnSocketAsyncArgs.cs is part of VNLib.Net.Transport.SimpleTCP which is part of the larger 
* VNLib collection of libraries and utilities.
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
using System.Net.Sockets;
using System.IO.Pipelines;

using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Transport.Tcp
{

    /// <summary>
    /// Reusable <see cref="SocketAsyncEventArgs"/> that manages a pipeline for sending and recieving data.
    /// on the connected socket
    /// </summary>
    internal sealed class VnSocketAsyncArgs : SocketAsyncEventArgs, ITcpConnectionDescriptor, IReusable
    {
        private readonly ISockAsyncArgsHandler _handler;

        public readonly SocketPipeLineWorker SocketWorker;

        public Stream Stream => SocketWorker.NetworkStream;
        

        public VnSocketAsyncArgs(ISockAsyncArgsHandler handler, PipeOptions options) : base()
        {
            SocketWorker = new(options);
            _handler = handler;
            //Only reuse socketes if windows
            DisconnectReuseSocket = OperatingSystem.IsWindows();
        }

        /// <summary>
        /// Begins an asynchronous accept operation on the current (bound) socket 
        /// </summary>
        /// <param name="sock">The server socket to accept the connection</param>
        /// <returns>True if the IO operation is pending</returns>
        public bool BeginAccept(Socket sock)
        {
            //Store the semaphore in the user token event args 
            SocketError = SocketError.Success;
            SocketFlags = SocketFlags.None;

            //Recv during accept is only supported on windows, this flag is true when on windows
            if (DisconnectReuseSocket)
            {
                //get buffer from the pipe to write initial accept data to
                Memory<byte> buffer = SocketWorker.GetMemory(sock.ReceiveBufferSize);
                SetBuffer(buffer);
            }

            //accept async
            return sock.AcceptAsync(this);
        }

        /// <summary>
        /// Determines if an asynchronous accept operation has completed successsfully
        /// and the socket is connected.
        /// </summary>
        /// <returns>True if the accept was successful, and the accepted socket is connected, false otherwise</returns>
        public bool EndAccept()
        {
            if(SocketError == SocketError.Success)
            {
                //remove ref to buffer
                SetBuffer(null);
                //start the socket worker
                SocketWorker.Start(AcceptSocket!, BytesTransferred);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Begins an async disconnect operation on a currentl connected socket
        /// </summary>
        /// <returns>True if the operation is pending</returns>
        public void Disconnect()
        {
            //Clear flags
            SocketError = SocketError.Success;
            //accept async
            if (!AcceptSocket!.DisconnectAsync(this))
            {
                //Invoke disconnected callback since op completed sync
                EndDisconnect();
                //Invoke disconnected callback since op completed sync
                _handler.OnSocketDisconnected(this);
            }
        }
        
        private void EndDisconnect()
        {
            //If the disconnection operation failed, do not reuse the socket on next accept
            if (SocketError != SocketError.Success)
            {
                //Dispose the socket before clearing the socket
                AcceptSocket?.Dispose();
                AcceptSocket = null;
            }
        }

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    //Invoke the accepted callback
                    _handler.OnSocketAccepted(this);
                    break;
                case SocketAsyncOperation.Disconnect:
                    EndDisconnect();
                    //Invoke disconnected callback since op completed sync
                    _handler.OnSocketDisconnected(this);
                    break;
                default:
                    throw new InvalidOperationException("Invalid socket operation");
            }
            //Clear flags/errors on completion
            SocketError = SocketError.Success;
            SocketFlags = SocketFlags.None;
        }

        ///<inheritdoc/>
        Socket ITcpConnectionDescriptor.Socket => AcceptSocket!;

        ///<inheritdoc/>
        Stream ITcpConnectionDescriptor.GetStream() => SocketWorker.NetworkStream;

        ///<inheritdoc/>
        void ITcpConnectionDescriptor.CloseConnection() => Disconnect();        


        void IReusable.Prepare()
        {
            SocketWorker.Prepare();
        }

        bool IReusable.Release()
        {
            UserToken = null;
            SocketWorker.Release();

            //if the sockeet is connected (or not windows), dispose it and clear the accept socket
            if (AcceptSocket?.Connected == true || !DisconnectReuseSocket)
            {
                AcceptSocket?.Dispose();
                AcceptSocket = null;
            }
            return true;
        }
        
        public new void Dispose()
        {
            //Dispose the base class
            base.Dispose();

            //Dispose the socket if its set
            AcceptSocket?.Dispose();
            AcceptSocket = null;

            //Cleanup socket worker
            SocketWorker.DisposeInternal();
        }
       
    }
}
