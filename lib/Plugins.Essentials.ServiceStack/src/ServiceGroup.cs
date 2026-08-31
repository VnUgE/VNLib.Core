/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: ServiceGroup.cs 
*
* ServiceGroup.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
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

using System.Collections.Generic;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// Represents a collection of virtual hosts that share a 
    /// common transport (interface, port, and SSL status)
    /// and may be loaded by a single server instance.
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="ServiceGroup"/> of virtual hosts
    /// with common transport
    /// </remarks>
    /// <param name="hosts">The hosts that share a common interface endpoint</param>
    public sealed class ServiceGroup(IEnumerable<IServiceHost> hosts)
    {
        private readonly IServiceHost[] _vHosts = [..hosts];

        /// <summary>
        /// The collection of hosts that are loaded by this group
        /// </summary>
        public IReadOnlyCollection<IServiceHost> Hosts => _vHosts;  
    }
}
