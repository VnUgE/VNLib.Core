/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMClientConfig.cs 
*
* FBMClientConfig.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Text;

using VNLib.Utils.Logging;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// A structure that defines readonly constants for the <see cref="FBMClient"/> to use 
    /// </summary>
    public readonly record struct FBMClientConfig
    {
        /// <summary>
        /// The size (in bytes) of the internal buffer used to buffer incomming messages,
        /// this value will also be sent to the server to synchronous recv buffer sizes
        /// </summary>
        public readonly int RecvBufferSize { get; init; }
        /// <summary>
        /// The size (in bytes) of the <see cref="FBMRequest"/> internal buffer size, when requests are rented from the client
        /// </summary>
        /// <remarks>
        /// This is the entire size of the request buffer including headers and payload data, unless 
        /// data is streamed to the server
        /// </remarks>
        public readonly int MessageBufferSize { get; init; }
        /// <summary>
        /// The size (in bytes) of the client/server message header buffer
        /// </summary>
        public readonly int MaxHeaderBufferSize { get; init; }
        /// <summary>
        /// The maximum size (in bytes) of messages sent or recieved from the server
        /// </summary>
        public readonly int MaxMessageSize { get; init; }
        /// <summary>
        /// The heap to allocate internal (and message) buffers from
        /// </summary>
        public readonly IFBMMemoryManager MemoryManager { get; init; }
        /// <summary>
        /// The websocket keepalive interval to use (leaving this property default disables keepalives)
        /// </summary>
        public readonly TimeSpan KeepAliveInterval { get; init; }

        /// <summary>
        /// If configured, configures a maximum request timout
        /// </summary>
        public readonly TimeSpan RequestTimeout { get; init; }
        /// <summary>
        /// The websocket sub-protocol to use
        /// </summary>
        public readonly string? SubProtocol { get; init; }
        /// <summary>
        /// The encoding instance used to encode header values
        /// </summary>
        public readonly Encoding HeaderEncoding { get; init; }

        /// <summary>
        /// An optional log provider to write debug logs to. If this propery is not null,
        /// debugging information will be logged with the debug log-level
        /// </summary>
        public readonly ILogProvider? DebugLog { get; init; }
    }
}
