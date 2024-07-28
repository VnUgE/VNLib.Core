/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: EventProcessorConfig.cs 
*
* EventProcessorConfig.cs is part of VNLib.Plugins.Essentials which is part 
* of the larger VNLib collection of libraries and utilities.
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
using System.Net;
using System.Collections.Frozen;
using System.Collections.Generic;

using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Essentials
{
    /// <summary>
    /// An immutable configuration object for the <see cref="EventProcessor"/> that services the 
    /// lifetieme of the processor.
    /// </summary>
    /// <param name="Directory"> The filesystem entrypoint path for the site</param>
    /// <param name="Hostname">The hostname the server will listen for, and the hostname that will identify this root when a connection requests it</param>
    /// <param name="Log">The application log provider for writing logging messages to</param>
    /// <param name="Options">Gets the EP processing options</param>
    public record class EventProcessorConfig(string Directory, string Hostname, ILogProvider Log, IEpProcessingOptions Options)
    {
        /// <summary>
        /// The table of virtual endpoints that will be used to process requests
        /// </summary>
        /// <remarks>
        /// May be overriden to provide a custom endpoint table
        /// </remarks>
        public IVirtualEndpointTable EndpointTable { get; init; } = new SemiConsistentVeTable();

        /// <summary>
        /// The middleware chain that will be used to process requests
        /// </summary>
        /// <remarks>
        /// If derrieved, may be overriden to provide a custom middleware chain
        /// </remarks>
        public IHttpMiddlewareChain MiddlewareChain { get; init; } = new SemiConistentMiddlewareChain();

        /// <summary>
        /// The name of a default file to search for within a directory if no file is specified (index.html).
        /// This array should be ordered.
        /// </summary>
        public IReadOnlyCollection<string> DefaultFiles { get; init; } = [];

        /// <summary>
        /// File extensions that are denied from being read from the filesystem
        /// </summary>
        public FrozenSet<string> ExcludedExtensions { get; init; } = FrozenSet<string>.Empty;

        /// <summary>
        /// File attributes that must be matched for the file to be accessed
        /// </summary>
        public FileAttributes AllowedAttributes { get; init; }

        /// <summary>
        /// Files that match any attribute flag set will be denied
        /// </summary>
        public FileAttributes DissallowedAttributes { get; init; }

        /// <summary>
        /// A table of known downstream servers/ports that can be trusted to proxy connections
        /// </summary>
        public FrozenSet<IPAddress> DownStreamServers { get; init; } = FrozenSet<IPAddress>.Empty;

        /// <summary>
        /// A <see cref="TimeSpan"/> for how long a connection may remain open before all operations are cancelled
        /// </summary>
        public TimeSpan ExecutionTimeout { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Enables or disables the use of the file path cache. If set to zero , the cache will be disabled,
        /// otherwise sets the maximum amount of time a file path is to be cached.
        /// </summary>
        public TimeSpan FilePathCacheMaxAge { get; init; } = TimeSpan.Zero;
    }
}