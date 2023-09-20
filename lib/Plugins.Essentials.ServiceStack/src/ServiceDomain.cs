/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Net;
using System.Linq;
using System.Collections.Generic;

using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// Represents a domain of services and thier dynamically loaded plugins 
    /// that will be hosted by an application service stack
    /// </summary>
    public sealed class ServiceDomain
    {
        private readonly LinkedList<ServiceGroup> _serviceGroups;
      
        /// <summary>
        /// Gets all service groups loaded in the service manager
        /// </summary>
        public IReadOnlyCollection<ServiceGroup> ServiceGroups => _serviceGroups;

        /// <summary>
        /// Initializes a new empty <see cref="ServiceDomain"/>
        /// </summary>
        public ServiceDomain() => _serviceGroups = new();

        /// <summary>
        /// Uses the supplied callback to get a collection of virtual hosts
        /// to build the current domain with
        /// </summary>
        /// <param name="hostBuilder">The callback method to build virtual hosts</param>
        /// <returns>A value that indicates if any virtual hosts were successfully loaded</returns>
        public bool BuildDomain(Action<ICollection<IServiceHost>> hostBuilder)
        {
            //LL to store created hosts
            LinkedList<IServiceHost> hosts = new();

            //build hosts
            hostBuilder.Invoke(hosts);

            return FromExisting(hosts);
        }

        /// <summary>
        /// Builds the domain from an existing enumeration of virtual hosts
        /// </summary>
        /// <param name="hosts">The enumeration of virtual hosts</param>
        /// <returns>A value that indicates if any virtual hosts were successfully loaded</returns>
        public bool FromExisting(IEnumerable<IServiceHost> hosts)
        {
            //Get service groups and pass service group list
            CreateServiceGroups(_serviceGroups, hosts);
            return _serviceGroups.Any();
        }
       
        private static void CreateServiceGroups(ICollection<ServiceGroup> groups, IEnumerable<IServiceHost> hosts)
        {
            //Get distinct interfaces
            IPEndPoint[] interfaces = hosts.Select(static s => s.TransportInfo.TransportEndpoint).Distinct().ToArray();

            //Select hosts of the same interface to create a group from
            foreach (IPEndPoint iface in interfaces)
            {
                IEnumerable<IServiceHost> groupHosts = hosts.Where(host => host.TransportInfo.TransportEndpoint.Equals(iface));

                //Find any duplicate hostnames for the same service gorup
                IServiceHost[] overlap = groupHosts.Where(vh => groupHosts.Select(static s => s.Processor.Hostname).Count(hostname => vh.Processor.Hostname == hostname) > 1).ToArray();

                if(overlap.Length > 0)
                {
                    throw new ArgumentException($"The hostname '{overlap.Last().Processor.Hostname}' is already in use by another virtual host");
                }

                //init new service group around an interface and its roots
                ServiceGroup group = new(iface, groupHosts);

                groups.Add(group);
            }
        }

        /// <summary>
        /// Tears down the service domain by destroying all <see cref="ServiceGroup"/>s. This instance may be rebuilt 
        /// if this method returns successfully.
        /// </summary>
        public void TearDown()
        {
            //Manually cleanup if unload missed data
            _serviceGroups.TryForeach(static sg => sg.UnloadAll());
            //empty service groups
            _serviceGroups.Clear();
        }
    }
}
