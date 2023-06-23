/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: ManagedClientWebSocket.cs 
*
* ManagedClientWebSocket.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Net;
using System.Threading;
using System.Net.Security;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using VNLib.Utils.Memory;

namespace VNLib.Net.Messaging.FBM.Client
{

    /// <summary>
    /// A wrapper container to manage client websockets 
    /// </summary>
    public class ManagedClientWebSocket : WebSocket
    {
        private readonly int TxBufferSize;
        private readonly int RxBufferSize;
        private readonly TimeSpan KeepAliveInterval;
        private readonly VnTempBuffer<byte> _dataBuffer;
        private readonly string? _subProtocol;

        /// <summary>
        /// A collection of headers to add to the client 
        /// </summary>
        public WebHeaderCollection Headers { get; }

        public X509CertificateCollection Certificates { get; }

        public IWebProxy? Proxy { get; set; }

        public CookieContainer? Cookies { get; set; }

        public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; set; }


        private ClientWebSocket? _socket;

        /// <summary>
        /// Initiaizes a new <see cref="ManagedClientWebSocket"/> that accepts an optional sub-protocol for connections
        /// </summary>
        /// <param name="txBufferSize">The size (in bytes) of the send buffer size</param>
        /// <param name="rxBufferSize">The size (in bytes) of the receive buffer size to use</param>
        /// <param name="keepAlive">The WS keepalive interval</param>
        /// <param name="subProtocol">The optional sub-protocol to use</param>
        public ManagedClientWebSocket(int txBufferSize, int rxBufferSize, TimeSpan keepAlive, string? subProtocol)
        {
            //Init header collection
            Headers = new();
            Certificates = new();
            //Alloc buffer
            _dataBuffer = new(rxBufferSize);
            TxBufferSize = txBufferSize;
            RxBufferSize = rxBufferSize;
            KeepAliveInterval = keepAlive;
            _subProtocol = subProtocol;
        }

        /// <summary>
        /// Asyncrhonously prepares a new client web-socket and connects to the remote endpoint
        /// </summary>
        /// <param name="serverUri">The endpoint to connect to</param>
        /// <param name="token">A token to cancel the connect operation</param>
        /// <returns>A task that compeltes when the client has connected</returns>
        public async Task ConnectAsync(Uri serverUri, CancellationToken token)
        {
            //Init new socket
            _socket = new();
            try
            {
                //Set buffer
                _socket.Options.SetBuffer(RxBufferSize, TxBufferSize, _dataBuffer);
                //Set remaining stored options
                _socket.Options.ClientCertificates = Certificates;
                _socket.Options.KeepAliveInterval = KeepAliveInterval;
                _socket.Options.Cookies = Cookies;
                _socket.Options.Proxy = Proxy;
                _socket.Options.RemoteCertificateValidationCallback = RemoteCertificateValidationCallback;
                //Specify sub-protocol
                if (!string.IsNullOrEmpty(_subProtocol))
                {
                    _socket.Options.AddSubProtocol(_subProtocol);
                }
                //Set headers
                for (int i = 0; i < Headers.Count; i++)
                {
                    string name = Headers.GetKey(i);
                    string? value = Headers.Get(i);
                    //Set header
                    _socket.Options.SetRequestHeader(name, value);
                }
                //Connect to server
                await _socket.ConnectAsync(serverUri, token);
            }
            catch
            {
                //Cleanup the socket
                Cleanup();
                throw;
            }
        }

        /// <summary>
        /// Cleans up internal resources to prepare for another connection
        /// </summary>
        public void Cleanup()
        {
            //Dispose old socket if set
            _socket?.Dispose();
            _socket = null;
        }
        ///<inheritdoc/>
        public override WebSocketCloseStatus? CloseStatus => _socket?.CloseStatus;
        ///<inheritdoc/>
        public override string CloseStatusDescription => _socket?.CloseStatusDescription ?? string.Empty;
        ///<inheritdoc/>
        public override WebSocketState State => _socket?.State ?? WebSocketState.Closed;
        ///<inheritdoc/>
        public override string SubProtocol => _subProtocol ?? string.Empty;


        ///<inheritdoc/>
        public override void Abort()
        {
            _socket?.Abort();
        }
        ///<inheritdoc/>
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return _socket?.CloseAsync(closeStatus, statusDescription, cancellationToken) ?? Task.CompletedTask;
        }
        ///<inheritdoc/>
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            if (_socket?.State == WebSocketState.Open || _socket?.State == WebSocketState.CloseSent)
            {
                return _socket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
            }
            return Task.CompletedTask;
        }
        ///<inheritdoc/>
        public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            _ = _socket ?? throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The connected socket has been disconnected");
            
            return _socket.ReceiveAsync(buffer, cancellationToken);
        }
        ///<inheritdoc/>
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            _ = _socket ?? throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The connected socket has been disconnected");
            
            return _socket.ReceiveAsync(buffer, cancellationToken);
        }
        ///<inheritdoc/>
        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            _ = _socket ?? throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The connected socket has been disconnected");
            return _socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }
        ///<inheritdoc/>
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            _ = _socket ?? throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The connected socket has been disconnected");
            return _socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        ///<inheritdoc/>
        public override void Dispose()
        {
            //Free buffer
            _dataBuffer.Dispose();
            _socket?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
