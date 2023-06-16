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
using System.Threading.Tasks;

namespace VNLib.Net.Transport.Tcp
{

    /// <summary>
    /// Represents the context of a transport connection. It includes the active socket 
    /// and a stream representing the active transport. 
    /// </summary>
    public readonly record struct TransportEventContext
    {  
        private readonly ITcpConnectionDescriptor _descriptor;

        /// <summary>
        /// A copy of the local endpoint of the listening socket
        /// </summary>
        public readonly IPEndPoint LocalEndPoint;

        /// <summary>
        /// The <see cref="IPEndPoint"/> representing the client's connection information
        /// </summary>
        public readonly IPEndPoint RemoteEndpoint;

        /// <summary>
        /// The transport stream that wraps the connection
        /// </summary>
        public readonly Stream ConnectionStream;
      
      
        /// <summary>
        /// Creates a new <see cref="TransportEventContext"/> wrapper for the given connection descriptor
        /// and captures the default stream from the descriptor.
        /// </summary>
        /// <param name="descriptor">The connection to wrap</param>
        public TransportEventContext(ITcpConnectionDescriptor descriptor):this(descriptor, descriptor.GetStream())
        { }

        /// <summary>
        /// Creates a new <see cref="TransportEventContext"/> wrapper for the given connection descriptor
        /// and your custom stream implementation.
        /// </summary>
        /// <param name="descriptor">The connection descriptor to wrap</param>
        /// <param name="customStream">Your custom stream wrapper around the transport stream</param>
        public TransportEventContext(ITcpConnectionDescriptor descriptor, Stream customStream)
        {
            _descriptor = descriptor;
            ConnectionStream = customStream;

            //Call once and store locally
            LocalEndPoint = (descriptor.Socket.LocalEndPoint as IPEndPoint)!;
            RemoteEndpoint = (descriptor.Socket.RemoteEndPoint as IPEndPoint)!;
        }

        /// <summary>
        /// Cleans up the stream and closes the connection descriptor
        /// </summary>
        /// <returns>A value-task that completes when the resources have been cleaned up</returns>
        public readonly async ValueTask CloseConnectionAsync()
        {
            try
            {
                //dispose the stream and wait for buffered data to be sent
                await ConnectionStream.DisposeAsync();
            }
            finally
            {
                //Disconnect
                _descriptor.CloseConnection();
            }
        }
    }
}