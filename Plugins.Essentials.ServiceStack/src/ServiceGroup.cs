/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Essentials.Content;
using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// Represents a collection of virtual hosts that share a 
    /// common transport (interface, port, and SSL status)
    /// and may be loaded by a single server instance.
    /// </summary>
    public sealed class ServiceGroup 
    {
        private readonly LinkedList<IServiceHost> _vHosts;
        private readonly ConditionalWeakTable<RuntimePluginLoader, IEndpoint[]> _endpointsForPlugins;

        /// <summary>
        /// The <see cref="IPEndPoint"/> transport endpoint for all loaded service hosts
        /// </summary>
        public IPEndPoint ServiceEndpoint { get; }

        /// <summary>
        /// The collection of hosts that are loaded by this group
        /// </summary>
        public IReadOnlyCollection<IServiceHost> Hosts => _vHosts;

        /// <summary>
        /// Initalizes a new <see cref="ServiceGroup"/> of virtual hosts
        /// with common transport
        /// </summary>
        /// <param name="serviceEndpoint">The <see cref="IPEndPoint"/> to listen for connections on</param>
        /// <param name="hosts">The hosts that share a common interface endpoint</param>
        public ServiceGroup(IPEndPoint serviceEndpoint, IEnumerable<IServiceHost> hosts)
        {
            _endpointsForPlugins = new();
            _vHosts = new(hosts);
            ServiceEndpoint = serviceEndpoint;
        }

        /// <summary>
        /// Sets the specified page rotuer for all virtual hosts
        /// </summary>
        /// <param name="router">The page router to user</param>
        internal void UpdatePageRouter(IPageRouter router) => _vHosts.TryForeach(v => v.Processor.SetPageRouter(router));
        /// <summary>
        /// Sets the specified session provider for all virtual hosts
        /// </summary>
        /// <param name="current">The session provider to use</param>
        internal void UpdateSessionProvider(ISessionProvider current) => _vHosts.TryForeach(v => v.Processor.SetSessionProvider(current));

        /// <summary>
        /// Adds or updates all endpoints exported by all plugins
        /// within the specified loader. All endpoints exposed
        /// by a previously loaded instance are removed and all
        /// currently exposed endpoints are added to all virtual 
        /// hosts
        /// </summary>
        /// <param name="loader">The plugin loader to get add/update endpoints from</param>
        internal void AddOrUpdateEndpointsForPlugin(RuntimePluginLoader loader)
        {
            //Get all new endpoints for plugin
            IEndpoint[] newEndpoints = loader.LivePlugins.SelectMany(static pl => pl.Plugin!.GetEndpoints()).ToArray();

            //See if 
            if(_endpointsForPlugins.TryGetValue(loader, out IEndpoint[]? oldEps))
            {
                //Remove old endpoints
                _vHosts.TryForeach(v => v.Processor.RemoveEndpoint(oldEps));
            }

            //Add endpoints to dict
            _endpointsForPlugins.AddOrUpdate(loader, newEndpoints);

            //Add endpoints to hosts
            _vHosts.TryForeach(v => v.Processor.AddEndpoint(newEndpoints));
        }

        /// <summary>
        /// Unloads all previously stored endpoints, router, session provider, and 
        /// clears all internal data structures
        /// </summary>
        internal void UnloadAll()
        {
            //Remove all loaded endpoints
            _vHosts.TryForeach(v => _endpointsForPlugins.TryForeach(eps => v.Processor.RemoveEndpoint(eps.Value)));

            //Remove all routers
            _vHosts.TryForeach(static v => v.Processor.SetPageRouter(null));
            //Remove all session providers
            _vHosts.TryForeach(static v => v.Processor.SetSessionProvider(null));

            //Clear all hosts
            _vHosts.Clear();
            //Clear all endpoints
            _endpointsForPlugins.Clear();
        }
    }
}
