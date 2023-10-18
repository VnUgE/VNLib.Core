/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IEpProcessingOptions.cs 
*
* IEpProcessingOptions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
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
using System.Collections.Generic;

namespace VNLib.Plugins.Essentials
{
    /// <summary>
    /// Provides an interface for <see cref="EventProcessor"/>
    /// security options
    /// </summary>
    public interface IEpProcessingOptions
    {

        /// <summary>
        /// The name of a default file to search for within a directory if no file is specified (index.html).
        /// This array should be ordered.
        /// </summary>
        IReadOnlyCollection<string> DefaultFiles { get; }

        /// <summary>
        /// File extensions that are denied from being read from the filesystem
        /// </summary>
        IReadOnlySet<string> ExcludedExtensions { get; }

        /// <summary>
        /// File attributes that must be matched for the file to be accessed
        /// </summary>
        FileAttributes AllowedAttributes { get; }

        /// <summary>
        /// Files that match any attribute flag set will be denied
        /// </summary>
        FileAttributes DissallowedAttributes { get; }      
        
        /// <summary>
        /// A table of known downstream servers/ports that can be trusted to proxy connections
        /// </summary>
        IReadOnlySet<IPAddress> DownStreamServers { get; }

        /// <summary>
        /// A <see cref="TimeSpan"/> for how long a connection may remain open before all operations are cancelled
        /// </summary>
        TimeSpan ExecutionTimeout { get; }
    }
}