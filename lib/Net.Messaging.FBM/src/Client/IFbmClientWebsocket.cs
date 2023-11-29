/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: IFbmClientWebsocket.cs 
*
* IFbmClientWebsocket.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;

using VNLib.Net.Http;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// Represents a client websocket
    /// </summary>
    public interface IFbmClientWebsocket: IDisposable
    {
        /// <summary>
        /// Connects the client to the remote server at the specified address,
        /// with the supplied headers
        /// </summary>
        /// <param name="address">The server address to connect to</param>
        /// <param name="headers">A header collection used when making the initial upgrade request to the server</param>
        /// <param name="cancellation">A token to cancel the connect operation</param>
        /// <returns>A task that completes when the socket connection has been established</returns>
        Task ConnectAsync(Uri address, VnWebHeaderCollection headers, CancellationToken cancellation);

        /// <summary>
        /// Cleanly disconnects the connected web socket from the 
        /// remote server.
        /// </summary>
        /// <param name="status">The websocket status to send on closure</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that completes when the operation complets</returns>
        Task DisconnectAsync(WebSocketCloseStatus status, CancellationToken cancellation);

        /// <summary>
        /// Sends the supplied memory segment to the connected server
        /// </summary>
        /// <param name="buffer">The data buffer to send to the server</param>
        /// <param name="messageType">A websocket message type</param>
        /// <param name="endOfMessage">A value that indicates if the segment is the last message in the sequence</param>
        /// <param name="cancellationToken">A token to cancel the send operation</param>
        /// <returns>A value task that resovles when the message has been sent</returns>
        ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);

        /// <summary>
        /// Receives data from the connected server and write data to the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to write data to</param>
        /// <param name="cancellationToken">A token to cancel the read operation</param>
        /// <returns>A value task that completes with the receive result</returns>
        ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    }
}
