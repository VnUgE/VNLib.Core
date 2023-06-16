/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: ITcpConnectionDescriptor.cs 
*
* ITcpConnectionDescriptor.cs is part of VNLib.Net.Transport.SimpleTCP which is part of the larger 
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

using System.IO;
using System.Net.Sockets;


namespace VNLib.Net.Transport.Tcp
{
    /// <summary>
    /// An opaque TCP connection descriptor
    /// </summary>
    public interface ITcpConnectionDescriptor
    {
        internal Socket Socket { get; }

        /// <summary>
        /// Gets a stream wrapper around the connection.
        /// </summary>
        /// <remarks>
        /// You must dispose of this stream when you are done with it.
        /// </remarks>
        Stream GetStream();

        /// <summary>
        /// Closes a connection and cleans up any resources
        /// </summary>
        /// <remarks>
        /// You must destory any references to this connection descriptor after calling this method.
        /// </remarks>
        void CloseConnection();
    }
}