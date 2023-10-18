/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: WebSocketSession.cs 
*
* WebSocketSession.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials
{
    /// <summary>
    /// A callback method to invoke when an HTTP service successfully transfers protocols to 
    /// the WebSocket protocol and the socket is ready to be used
    /// </summary>
    /// <param name="session">The open websocket session instance</param>
    /// <returns>
    /// A <see cref="Task"/> that will be awaited by the HTTP layer. When the task completes, the transport 
    /// will be closed and the session disposed
    /// </returns>

    public delegate Task WebSocketAcceptedCallback(WebSocketSession session);

    /// <summary>
    /// A callback method to invoke when an HTTP service successfully transfers protocols to 
    /// the WebSocket protocol and the socket is ready to be used
    /// </summary>
    /// <typeparam name="T">The type of the user state object</typeparam>
    /// <param name="session">The open websocket session instance</param>
    /// <returns>
    /// A <see cref="Task"/> that will be awaited by the HTTP layer. When the task completes, the transport 
    /// will be closed and the session disposed
    /// </returns>

    public delegate Task WebSocketAcceptedCallback<T>(WebSocketSession<T> session);

    /// <summary>
    /// Represents a <see cref="WebSocket"/> wrapper to manage the lifetime of the captured
    /// connection context and the underlying transport. This session is managed by the parent
    /// <see cref="HttpServer"/> that it was created on.
    /// </summary>
    public class WebSocketSession : AlternateProtocolBase
    {
        internal WebSocket? WsHandle;
        internal readonly WebSocketAcceptedCallback AcceptedCallback;

        /// <summary>
        /// A cancellation token that can be monitored to reflect the state 
        /// of the webscocket
        /// </summary>
        public CancellationToken Token => CancelSource.Token;

        /// <summary>
        /// Id assigned to this instance on creation
        /// </summary>
        public string SocketID { get; }

        /// <summary>
        /// Negotiated sub-protocol
        /// </summary>
        public string? SubProtocol { get; internal init; }        
       
        /// <summary>
        /// The websocket keep-alive interval
        /// </summary>
        internal TimeSpan KeepAlive { get; init; }
        
        internal WebSocketSession(string socketId, WebSocketAcceptedCallback callback)
        {
            SocketID = socketId;
            //Store the callback function
            AcceptedCallback = callback;
        }

        /// <summary>
        /// Initialzes the created websocket with the specified protocol 
        /// </summary>
        /// <param name="transport">Transport stream to use for the websocket</param>
        /// <returns>The accept callback function specified during object initialization</returns>
        protected override async Task RunAsync(Stream transport)
        {
            try
            {
                WebSocketCreationOptions ce = new()
                {
                    IsServer = true,
                    KeepAliveInterval = KeepAlive,
                    SubProtocol = SubProtocol,
                };
                
                //Create a new websocket from the context stream
                WsHandle = WebSocket.CreateFromStream(transport, ce);

                //Register token to abort the websocket so the managed ws uses the non-fallback send/recv method
                using CancellationTokenRegistration abortReg = Token.Register(WsHandle.Abort);
                
                //Return the callback function to explcitly invoke it
                await AcceptedCallback(this);
            }
            finally
            {
                WsHandle?.Dispose();
            }
        }

        /// <summary>
        /// Asynchronously receives data from the Websocket and copies the data to the specified buffer
        /// </summary>
        /// <param name="buffer">The buffer to store read data</param>
        /// <returns>A task that resolves a <see cref="WebSocketReceiveResult"/> which contains the status of the operation</returns>
        /// <exception cref="OperationCanceledException"></exception>
        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer) => WsHandle!.ReceiveAsync(buffer, CancellationToken.None);

        /// <summary>
        /// Asynchronously receives data from the Websocket and copies the data to the specified buffer
        /// </summary>
        /// <param name="buffer">The buffer to store read data</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException"></exception>
        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer) => WsHandle!.ReceiveAsync(buffer, CancellationToken.None);

        /// <summary>
        /// Asynchronously sends the specified buffer to the client of the specified type
        /// </summary>
        /// <param name="buffer">The buffer containing data to send</param>
        /// <param name="type">The message/data type of the packet to send</param>
        /// <param name="endOfMessage">A value that indicates this message is the final message of the transaction</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException"></exception>
        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage) => WsHandle!.SendAsync(buffer, type, endOfMessage, CancellationToken.None);


        /// <summary>
        /// Asynchronously sends the specified buffer to the client of the specified type
        /// </summary>
        /// <param name="buffer">The buffer containing data to send</param>
        /// <param name="type">The message/data type of the packet to send</param>
        /// <param name="endOfMessage">A value that indicates this message is the final message of the transaction</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException"></exception>
        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType type, bool endOfMessage)
        {
            //Begin receive operation only with the internal token
            return SendAsync(buffer, type, endOfMessage ? WebSocketMessageFlags.EndOfMessage : WebSocketMessageFlags.None);
        }

        /// <summary>
        /// Asynchronously sends the specified buffer to the client of the specified type
        /// </summary>
        /// <param name="buffer">The buffer containing data to send</param>
        /// <param name="type">The message/data type of the packet to send</param>
        /// <param name="flags">Websocket message flags</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException"></exception>
        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType type, WebSocketMessageFlags flags) => WsHandle!.SendAsync(buffer, type, flags, CancellationToken.None);


        /// <summary>
        /// Properly closes a currently connected websocket 
        /// </summary>
        /// <param name="status">Set the close status</param>
        /// <param name="reason">Set the close reason</param>
        /// <exception cref="ObjectDisposedException"></exception>
        public Task CloseSocketAsync(WebSocketCloseStatus status, string reason) => WsHandle!.CloseAsync(status, reason, CancellationToken.None);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        /// <param name="reason"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public Task CloseSocketOutputAsync(WebSocketCloseStatus status, string reason, CancellationToken cancellation = default)
        {
            if (WsHandle!.State == WebSocketState.Open || WsHandle.State == WebSocketState.CloseSent)
            {
                return WsHandle.CloseOutputAsync(status, reason, cancellation);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <typeparam name="T">The user-state type</typeparam>
    public sealed class WebSocketSession<T> : WebSocketSession
    {

#nullable disable
        
        /// <summary>
        /// A user-defined state object passed during socket accept handshake
        /// </summary>
        public T UserState { get; internal init; }

#nullable enable

        internal WebSocketSession(string sessionId, WebSocketAcceptedCallback<T> callback)
          : base(sessionId, (ses) => callback((ses as WebSocketSession<T>)!))
        {
            UserState = default;
        }
    }
}