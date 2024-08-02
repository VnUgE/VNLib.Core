/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: VirtualHostConfig.cs 
*
* VirtualHostConfig.cs is part of VNLib.WebServer which is part of the larger 
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

using System;
using System.IO;
using System.Net;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.ServiceStack.Construction;

using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer
{
    /// <summary>
    /// Implementation of <see cref="IEpProcessingOptions"/>
    /// with <see cref="VirtualHostHooks"/> extra processing options
    /// </summary>
    internal sealed class VirtualHostConfig : VirtualHostConfiguration, IEpProcessingOptions
    {
        public VirtualHostConfig()
        {
            //Update file attributes
            AllowedAttributes = FileAttributes.Archive | FileAttributes.Compressed | FileAttributes.Normal | FileAttributes.ReadOnly;
            DissallowedAttributes = FileAttributes.Device
                | FileAttributes.Directory
                | FileAttributes.Encrypted
                | FileAttributes.Hidden
                | FileAttributes.IntegrityStream
                | FileAttributes.Offline
                | FileAttributes.ReparsePoint
                | FileAttributes.System;
        }

        /// <summary>
        /// A regex filter instance to filter incoming filesystem paths
        /// </summary>
        public Regex? PathFilter { get; init; }

        /// <summary>
        /// The default response entity cache value
        /// </summary>
        public required TimeSpan CacheDefault { get; init; }

        /// <summary>
        /// A collection of in-memory files to send in response to processing error
        /// codes.
        /// </summary>
        public FrozenDictionary<HttpStatusCode, FileCache> FailureFiles { get; init; } = new Dictionary<HttpStatusCode, FileCache>().ToFrozenDictionary();

        /// <summary>
        /// Allows config to specify contant additional headers
        /// </summary>
        public KeyValuePair<string, string>[] AdditionalHeaders { get; init; } = Array.Empty<KeyValuePair<string, string>>();

        /// <summary>
        /// Contains internal headers used for specific purposes, cherrypicked from the config headers 
        /// </summary>
        public FrozenDictionary<string, string> SpecialHeaders { get; init; } = new Dictionary<string, string>().ToFrozenDictionary();

        /// <summary>
        /// The array of interfaces the host wishes to listen on
        /// </summary>
        internal required TransportInterface[] Transports { get; init; }

        /// <summary>
        /// An optional whitelist set of ipaddresses that are allowed to make connections to this site
        /// </summary>
        internal required FrozenSet<IPAddress>? WhiteList { get; init; }

        /// <summary>
        /// An optional blacklist set of ipaddresses that are not allowed to make connections to this site
        /// </summary>
        internal required FrozenSet<IPAddress>? BlackList { get; init; }

    }
}
