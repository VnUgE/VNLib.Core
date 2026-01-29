/*
* Copyright (c) 2025 Vaughn Nugent
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

using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.ServiceStack.Plugins;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// Represents a collection of virtual hosts that share a 
    /// common transport (interface, port, and SSL status)
    /// and may be loaded by a single server instance.
    /// </summary>
    /// <remarks>
    /// Initalizes a new <see cref="ServiceGroup"/> of virtual hosts
    /// with common transport
    /// </remarks>
    /// <param name="hosts">The hosts that share a common interface endpoint</param>
    public sealed class ServiceGroup(IEnumerable<IServiceHost> hosts)
    {
        private readonly LinkedList<IServiceHost> _vHosts = new(hosts);
        private readonly ConditionalWeakTable<IManagedPlugin, IEndpoint[]> _endpointsForPlugins = new();

        /// <summary>
        /// The collection of hosts that are loaded by this group
        /// </summary>
        public IReadOnlyCollection<IServiceHost> Hosts => _vHosts;

        /// <summary>
        /// Manually detatches runtime services and their loaded endpoints from all
        /// endpoints.
        /// </summary>
        internal void UnloadAll()
        {
            //Remove all loaded endpoints
            _vHosts.TryForeach(v => _endpointsForPlugins.ForEach(eps => v.OnRuntimeServiceDetach(eps.Key, eps.Value)));

            //Clear all hosts
            _vHosts.Clear();
            //Clear all endpoints
            _endpointsForPlugins.Clear();
        }

        internal void OnPluginLoaded(IManagedPlugin plugin)
        {
            //Get all new endpoints for plugin
            IEndpoint[] newEndpoints = plugin.GetEndpoints()
                .ToArray();

            //Add endpoints to dict
            _endpointsForPlugins.AddOrUpdate(plugin, newEndpoints);

            //Add endpoints to hosts
            _vHosts.TryForeach(v => v.OnRuntimeServiceAttach(plugin, newEndpoints));
        }

        internal void OnPluginUnloaded(IManagedPlugin plugin)
        {
            //Get the old endpoints from the controller reference and remove them
            if (_endpointsForPlugins.TryGetValue(plugin, out IEndpoint[]? oldEps))
            {
                //Remove the old endpoints
                _vHosts.TryForeach(v => v.OnRuntimeServiceDetach(plugin, oldEps));

                //remove controller ref
                _ = _endpointsForPlugins.Remove(plugin);
            }
        }
    }
}
