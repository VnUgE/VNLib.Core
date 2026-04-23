/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: AwaitableValueSocketEventArgs.cs 
*
* AwaitableValueSocketEventArgs.cs is part of VNLib.Net.Transport.Tcp which 
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
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace VNLib.Net.Transport.Tcp.Internal
{
    internal sealed class AwaitableValueSocketEventArgs :
        SocketAsyncEventArgs,
        IValueTaskSource<SocketError>,
        IValueTaskSource<int>
    {
        /// <summary>
        /// Begins an asynchronous accept operation on the current (bound) socket 
        /// </summary>
        /// <param name="args">The <see cref="AwaitableValueSocketEventArgs"/> instance to use for the operation</param>
        /// <param name="sock">The server socket to accept the connection</param>
        /// <returns>True if the IO operation is pending</returns>
        public static ValueTask<SocketError> AcceptAsync(AwaitableValueSocketEventArgs args, Socket sock)
        {
            args.OnBeforeOperation(SocketFlags.None);

            return sock.AcceptAsync(args)
                ? new ValueTask<SocketError>(args, args.AsyncTaskCore.Version)
                : ValueTask.FromResult(args.SocketError);
        }

        /// <summary>
        /// Begins an async disconnect operation on a currently connected socket
        /// </summary>
        /// <returns>True if the operation is pending</returns>
        public static ValueTask<SocketError> DisconnectAsync(AwaitableValueSocketEventArgs args, Socket serverSock)
        {
            args.OnBeforeOperation(SocketFlags.None);

            return serverSock.DisconnectAsync(args)
                ? new ValueTask<SocketError>(args, args.AsyncTaskCore.Version)
                : ValueTask.FromResult(args.SocketError);
        }


        private ManualResetValueTaskSourceCore<int> AsyncTaskCore;

        /// <inheritdoc/>
        public void Prepare()
        {
            SocketError = SocketError.Success;
            SocketFlags = SocketFlags.None;
        }

        /// <inheritdoc/>
        public void Release()
        {
            //Make sure any operation specific data is cleared
            AcceptSocket = null;
            UserToken = null;
            SetBuffer(default);
        }

        /// <inheritdoc/>
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

        private ValueTask<int> GetSyncTxRxResult()
        {
            return SocketError switch
            {
                SocketError.Success => ValueTask.FromResult(BytesTransferred),
                _ => ValueTask.FromException<int>(new SocketException((int)SocketError))
            };
        }

        public ValueTask<int> SendAsync(Socket socket, Memory<byte> buffer, SocketFlags flags)
        {
            OnBeforeOperation(flags);

            SetBuffer(buffer);

            // Send returns true when the operation is running async, false is
            // sync, so return the result immediately
            return socket.SendAsync(this)
                ? new ValueTask<int>(this, AsyncTaskCore.Version)
                : GetSyncTxRxResult();
        }

        public ValueTask<int> ReceiveAsync(Socket socket, Memory<byte> buffer, SocketFlags flags)
        {
            OnBeforeOperation(flags);

            SetBuffer(buffer);

            // Receive returns true when the operation is running async, false is
            // sync, so return the result immediately
            return socket.ReceiveAsync(this)
                ? new ValueTask<int>(this, AsyncTaskCore.Version)
                : GetSyncTxRxResult();
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
