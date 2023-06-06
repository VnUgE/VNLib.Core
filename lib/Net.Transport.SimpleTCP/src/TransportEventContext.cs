/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: TransportEventContext.cs 
*
* TransportEventContext.cs is part of VNLib.Net.Transport.SimpleTCP which is part of the larger 
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
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


namespace VNLib.Net.Transport.Tcp
{
    /// <summary>
    /// Represents the context of a transport connection. It includes the active socket 
    /// and a stream representing the active transport. 
    /// </summary>
    public readonly record struct TransportEventContext
    {  
        /// <summary>
        /// The socket referrence to the incoming connection
        /// </summary>
        private readonly Socket Socket;
        
        private readonly VnSocketAsyncArgs _socketArgs;

        /// <summary>
        /// A copy of the local endpoint of the listening socket
        /// </summary>
        public readonly IPEndPoint LocalEndPoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Socket.LocalEndPoint as IPEndPoint)!;
        }

        /// <summary>
        /// The <see cref="IPEndPoint"/> representing the client's connection information
        /// </summary>
        public readonly IPEndPoint RemoteEndpoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Socket.RemoteEndPoint as IPEndPoint)!;
        }

        /// <summary>
        /// The transport stream to be actively read
        /// </summary>
        public readonly Stream ConnectionStream;
      
      
        internal TransportEventContext(VnSocketAsyncArgs args, Stream @stream)
        {
            _socketArgs = args;
            Socket = args.AcceptSocket!;
            ConnectionStream = stream;
        }

        /// <summary>
        /// Closes a connection and cleans up any resources
        /// </summary>
        /// <returns></returns>
        public async ValueTask CloseConnectionAsync()
        {
            //dispose the stream and wait for buffered data to be sent
            await ConnectionStream.DisposeAsync();

            //Disconnect
            _socketArgs.Disconnect();
        }
    }
}