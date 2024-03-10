/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMFallbackClientWsFactory.cs 
*
* FBMFallbackClientWsFactory.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Buffers;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;

using VNLib.Net.Http;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// Creates a new <see cref="IFbmWebsocketFactory"/> that builds new client websockets 
    /// on demand using the <see cref="ClientWebSocket"/> .NET default implementation
    /// </summary>
    /// <remarks>
    /// Initalizes a new <see cref="FBMFallbackClientWsFactory"/> instance
    /// </remarks>
    /// <param name="onConfigureSocket">A callback function that allows users to configure sockets when created</param>
    public class FBMFallbackClientWsFactory(Action<ClientWebSocketOptions>? onConfigureSocket = null) : IFbmWebsocketFactory
    {
        ///<inheritdoc/>
        public IFbmClientWebsocket CreateWebsocket(in FBMClientConfig clientConfig)
        {            
            ClientWebSocket socket = new();
            
            socket.Options.KeepAliveInterval = clientConfig.KeepAliveInterval;

            //Setup the socket receive buffer
            byte[] poolBuffer = ArrayPool<byte>.Shared.Rent(clientConfig.MaxMessageSize);
            socket.Options.SetBuffer(clientConfig.MaxMessageSize, clientConfig.MaxMessageSize, poolBuffer);

            //If config specifies a sub protocol, set it
            if (!string.IsNullOrEmpty(clientConfig.SubProtocol))
            {
                socket.Options.AddSubProtocol(clientConfig.SubProtocol);
            }

            //invoke client configuration user callback
            onConfigureSocket?.Invoke(socket.Options);

            return new FBMWebsocket(socket, poolBuffer);
        }

        private sealed record class FBMWebsocket(ClientWebSocket Socket, byte[] Buffer) : IFbmClientWebsocket
        {
            ///<inheritdoc/>
            public async Task ConnectAsync(Uri address, VnWebHeaderCollection headers, CancellationToken cancellation)
            {
                //Set headers
                for (int i = 0; i < headers.Count; i++)
                {
                    string name = headers.GetKey(i);
                    string? value = headers.Get(i);
                    //Set header
                    Socket.Options.SetRequestHeader(name, value);
                }

                //Connect to server
                await Socket.ConnectAsync(address, cancellation);
            }

            ///<inheritdoc/>
            public Task DisconnectAsync(WebSocketCloseStatus status, CancellationToken cancellation)
            {
                if (Socket?.State == WebSocketState.Open || Socket?.State == WebSocketState.CloseSent)
                {
                    return Socket.CloseOutputAsync(status, "Socket disconnected", cancellation);
                }
                return Task.CompletedTask;
            }

            ///<inheritdoc/>
            public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) 
                => Socket.ReceiveAsync(buffer, cancellationToken);

            ///<inheritdoc/>
            public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) 
                => Socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

            public void Dispose()
            {
                //Remove buffer refs and return to pool
                Socket.Dispose();
                ArrayPool<byte>.Shared.Return(Buffer);
            }
        }
    }
}
