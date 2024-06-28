/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: HttpServiceStackBuilder.cs 
*
* HttpServiceStackBuilder.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.Linq;
using System.Collections.Generic;


namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{
    /// <summary>
    /// A data structure used to build groupings of service hosts used
    /// to configure http servers.
    /// </summary>
    public sealed class ServiceBuilder
    {
        private readonly List<Action<ICollection<IServiceHost>>> _callbacks = [];

        /// <summary>
        /// Adds callback function that will add a collection of service hosts
        /// and passes a state paramter to the callback
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="state">The optional state parameter</param>
        /// <param name="host">The host collection to add new service hosts to</param>
        /// <returns>The current instance for chaining</returns>
        public ServiceBuilder AddHostCollection<T>(T state, Action<ICollection<IServiceHost>, T> host) 
            => AddHostCollection(col => host.Invoke(col, state));

        /// <summary>
        /// Adds a callback function that will add a collection of service hosts
        /// </summary>
        /// <param name="host">The callback function to return the collection of hosts</param>
        /// <returns>The current instance for chaining</returns>
        public ServiceBuilder AddHostCollection(Action<ICollection<IServiceHost>> host)
        {
            _callbacks.Add(host);
            return this;
        }

        /// <summary>
        /// Builds the <see cref="ServiceGroup"/> collection from the user 
        /// defined service host arrays
        /// </summary>
        /// <returns>The numeration that builds the service groups</returns>
        internal IEnumerable<ServiceGroup> BuildGroups()
        {
            return _callbacks.Select(static cb =>
            {
                LinkedList<IServiceHost> hosts = new();

                cb.Invoke(hosts);

                return new ServiceGroup(hosts);
            });
        }
    }
}
