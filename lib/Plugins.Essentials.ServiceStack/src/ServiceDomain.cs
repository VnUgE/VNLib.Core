/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: ServiceDomain.cs 
*
* ServiceDomain.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.ServiceStack.Construction;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// Represents a domain of services and thier dynamically loaded plugins 
    /// that will be hosted by an application service stack
    /// </summary>
    public sealed class ServiceDomain
    {
        private ServiceGroup[] _serviceGroups = [];
      
        /// <summary>
        /// Gets all service groups loaded in the service manager
        /// </summary>
        public IReadOnlyCollection<ServiceGroup> ServiceGroups => _serviceGroups;

        /// <summary>
        /// Uses the supplied callback to get a collection of virtual hosts
        /// to build the current domain with
        /// </summary>
        /// <param name="hostBuilder">The callback method to build virtual hosts</param>
        /// <returns>A value that indicates if any virtual hosts were successfully loaded</returns>
        public void BuildDomain(ServiceBuilder hostBuilder)
        {
            ArgumentNullException.ThrowIfNull(hostBuilder);

            FromExisting(hostBuilder.BuildGroups());
        }

        /// <summary>
        /// Builds the domain from an existing enumeration of virtual hosts
        /// </summary>
        /// <param name="hosts">The enumeration of virtual hosts</param>
        /// <returns>A value that indicates if any virtual hosts were successfully loaded</returns>
        public void FromExisting(IEnumerable<ServiceGroup> hosts)
        {
            ArgumentNullException.ThrowIfNull(hosts);

            hosts.ForEach(h => _serviceGroups = [.. _serviceGroups, h]);
        }

        /// <summary>
        /// Tears down the service domain by destroying all <see cref="ServiceGroup"/>s. This instance may be rebuilt 
        /// if this method returns successfully.
        /// </summary>
        public void TearDown()
        {
            //Manually cleanup if unload missed data
            Array.ForEach(_serviceGroups, static sg => sg.UnloadAll());
          
            Array.Clear(_serviceGroups);

            _serviceGroups = [];
        }
    }
}
