/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: VirtualHostConfiguration.cs 
*
* VirtualHostConfiguration.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.ServiceStack is distributed in the hope that it will be useful,
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
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{
    /// <summary>
    /// A virtual host configuration container
    /// </summary>
    public class VirtualHostConfiguration : IHostTransportInfo, IEpProcessingOptions
    {
        /// <summary>
        /// The directory that this virtual host will serve files from
        /// </summary>
        public DirectoryInfo RootDir { get; set; } = null!;

        /// <summary>
        /// The hostname, or domain name, that this virtual host will respond to
        /// <para>Default: *</para>
        /// </summary>
        public string Hostname { get; set; } = "*";

        /// <summary>
        /// The transport endpoint that this virtual host will listen on
        /// <para>Default: 0.0.0.0:80</para>
        /// </summary>
        public IPEndPoint TransportEndpoint { get; set; } = new IPEndPoint(IPAddress.Any, 80);

        /// <summary>
        /// An optional certificate to use for TLS connections
        /// </summary>
        public X509Certificate? Certificate { get; set; }

        /// <summary>
        /// A log provider to use for this virtual host
        /// </summary>
        public ILogProvider LogProvider { get; set; } = null!;

        /// <summary>
        /// The name of a default file to search for within a directory if no file is specified (index.html).
        /// This array should be ordered.
        /// <para>Default: empty set</para>
        /// </summary>
        public IReadOnlyCollection<string> DefaultFiles { get; set; } = Array.Empty<string>();

        /// <summary>
        /// File extensions that are denied from being read from the filesystem
        /// <para>Default: empty set</para>
        /// </summary>
        public IReadOnlySet<string> ExcludedExtensions { get; set; } = new HashSet<string>();

        /// <summary>
        /// File attributes that must be matched for the file to be accessed, defaults to all allowed
        /// <para> Default: 0xFFFFFFFF</para>
        /// </summary>
        public FileAttributes AllowedAttributes { get; set; } = unchecked((FileAttributes)0xFFFFFFFF);

        /// <summary>
        /// Files that match any attribute flag set will be denied
        /// <para>Default: <see cref="FileAttributes.System"/></para>
        /// </summary>
        public FileAttributes DissallowedAttributes { get; set; } = FileAttributes.System;

        /// <summary>
        /// A table of known downstream servers/ports that can be trusted to proxy connections
        /// <para>Default: empty set</para>
        /// </summary>
        public IReadOnlySet<IPAddress> DownStreamServers { get; set; } = new HashSet<IPAddress>();

        /// <summary>
        /// A <see cref="TimeSpan"/> for how long a connection may remain open before all operations are cancelled
        /// <para>Default: 60 seconds</para>
        /// </summary>
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// A <see cref="TimeSpan"/> for how long a connection may remain idle before all operations are cancelled
        /// </summary>
        public IVirtualHostHooks EventHooks { get; set; } = null!;

        /// <summary>
        /// A set of custom middleware to add to virtual host middleware pipeline
        /// </summary>
        public ICollection<IHttpMiddleware> CustomMiddleware { get; } = new List<IHttpMiddleware>();

        internal VirtualHostConfiguration Clone() => (VirtualHostConfiguration)MemberwiseClone();
    }
}
