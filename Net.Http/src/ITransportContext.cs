/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ITransportContext.cs 
*
* ITransportContext.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Security.Authentication;


namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents an active connection for application data processing
    /// </summary>
    public interface ITransportContext
    {
        /// <summary>
        /// The transport network stream for application data marshaling
        /// </summary>
        Stream ConnectionStream { get; }
        /// <summary>
        /// The transport security layer security protocol
        /// </summary>
        SslProtocols SslVersion { get; }
        /// <summary>
        /// A copy of the local endpoint of the listening socket
        /// </summary>
        IPEndPoint LocalEndPoint { get; }
        /// <summary>
        /// The <see cref="IPEndPoint"/> representing the client's connection information
        /// </summary>
        IPEndPoint RemoteEndpoint { get; }

        /// <summary>
        /// Closes the connection when its no longer in use and cleans up held resources.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// This method will always be called by the server when a connection is complete 
        /// regardless of the state of the trasnport
        /// </remarks>
        ValueTask CloseConnectionAsync();

        /// <summary>
        /// Attemts to get the transport security details for the connection
        /// </summary>
        /// <returns>A the <see cref="TransportSecurityInfo"/> structure if applicable, null otherwise</returns>
        TransportSecurityInfo? GetSecurityInfo();
    }
}