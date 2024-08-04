/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: TcpTransportContext.cs 
*
* TcpTransportContext.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Net.Transport.Tcp;

namespace VNLib.WebServer.Transport
{
    /// <summary>
    /// The TCP connection context
    /// </summary>
    internal class TcpTransportContext : ITransportContext
    {
        //Store static empty security info to pass in default case
        private static readonly TransportSecurityInfo? EmptySecInfo;

        protected readonly ITcpConnectionDescriptor _descriptor;

        protected readonly Stream _connectionStream;
        protected readonly IPEndPoint _localEndoint;
        protected readonly IPEndPoint _remoteEndpoint;
        protected readonly ITcpListner _server;

        public TcpTransportContext(ITcpListner server, ITcpConnectionDescriptor descriptor, Stream stream)
        {
            _descriptor = descriptor;
            _connectionStream = stream;
            _server = server;
            //Get the endpoints
            descriptor.GetEndpoints(out _localEndoint, out _remoteEndpoint);
        }

        ///<inheritdoc/>
        public virtual Stream ConnectionStream
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _connectionStream;
        }

        ///<inheritdoc/>
        public virtual IPEndPoint LocalEndPoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _localEndoint;
        }

        ///<inheritdoc/>
        public virtual IPEndPoint RemoteEndpoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _remoteEndpoint;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual async ValueTask CloseConnectionAsync()
        {
            //Close the stream before the descriptor
            await _connectionStream.DisposeAsync();
            await _server.CloseConnectionAsync(_descriptor, true);
        }

        //Ssl is not supported in this transport
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ref readonly TransportSecurityInfo? GetSecurityInfo() => ref EmptySecInfo;
    }
}
