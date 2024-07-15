/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: TCPConfig.cs 
*
* TCPConfig.cs is part of VNLib.Net.Transport.SimpleTCP which is part of the larger 
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
using System.Net;
using System.Buffers;
using System.Net.Sockets;

using VNLib.Utils.Logging;

namespace VNLib.Net.Transport.Tcp
{
    /// <summary>
    /// Represents the required configuration variables for the transport
    /// </summary>
    public readonly record struct TCPConfig
    {
        /// <summary>
        /// The <see cref="IPEndPoint"/> the listening socket will bind to
        /// </summary>
        public required readonly IPEndPoint LocalEndPoint { get; init; }
        /// <summary>
        /// The log provider used to write logging information to
        /// </summary>
        public required readonly ILogProvider Log { get; init; }
        /// <summary>
        /// If TCP keepalive is enabled, the amount of time the connection is considered alive before another probe message is sent
        /// </summary>
        public required readonly int TcpKeepAliveTime { get; init; }
        /// <summary>
        /// If TCP keepalive is enabled, the amount of time the connection will wait for a keepalive message
        /// </summary>
        public required readonly int KeepaliveInterval { get; init; }
        /// <summary>
        /// Enables TCP keepalive
        /// </summary>
        public required readonly bool TcpKeepalive { get; init; }
        /// <summary>
        /// The maximum number of waiting WSA asynchronous socket accept operations
        /// </summary>
        public required readonly uint AcceptThreads { get; init; }
        /// <summary>
        /// The maximum size (in bytes) the transport will buffer in
        /// the receiving pipeline.
        /// </summary>
        public required readonly int MaxRecvBufferData { get; init; }
        /// <summary>
        /// The maximum number of allowed socket connections to this server
        /// </summary>
        public required readonly long MaxConnections { get; init; }
        /// <summary>
        /// The listener socket backlog count
        /// </summary>
        public required readonly int BackLog { get; init; }
        /// <summary>
        /// The <see cref="MemoryPool{T}"/> to allocate transport buffers from
        /// </summary>
        public required readonly MemoryPool<byte> BufferPool { get; init; }
        /// <summary>
        /// <para>
        /// The maxium number of event objects that will be cached 
        /// during normal operation
        /// </para>
        /// <para>
        /// WARNING: Setting this value too low will cause significant CPU overhead and GC load
        /// </para>
        /// </summary>
        public readonly int CacheQuota { get; init; }
        /// <summary>
        /// An optional callback invoked after the socket has been created
        /// for optional appliction specific socket configuration
        /// </summary>
        public readonly Action<Socket>? OnSocketCreated { get; init; } 
        /// <summary>
        /// Enables verbose logging of TCP operations using the <see cref="LogLevel.Verbose"/>
        /// level
        /// </summary>
        public readonly bool DebugTcpLog { get; init; }
    }
}