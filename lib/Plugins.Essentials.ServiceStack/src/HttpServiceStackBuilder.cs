/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Collections.Generic;

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// A data structure used to build/create a <see cref="HttpServiceStack"/>
    /// around a <see cref="ServiceDomain"/>
    /// </summary>
    public sealed class HttpServiceStackBuilder
    {
        /// <summary>
        /// Initializes a new <see cref="HttpServiceStack"/> that will 
        /// generate servers to listen for services exposed by the 
        /// specified host context
        /// </summary>
        public HttpServiceStackBuilder()
        {}

        private Action<ICollection<IServiceHost>>? _hostBuilder;
        private Func<ServiceGroup, IHttpServer>? _getServers;

        /// <summary>
        /// Uses the supplied callback to get a collection of virtual hosts
        /// to build the current domain with
        /// </summary>
        /// <param name="hostBuilder">The callback method to build virtual hosts</param>
        /// <returns>A value that indicates if any virtual hosts were successfully loaded</returns>
        public HttpServiceStackBuilder WithDomainBuilder(Action<ICollection<IServiceHost>> hostBuilder)
        {
            _hostBuilder = hostBuilder;
            return this;
        }

        /// <summary>
        /// Spcifies a callback function that builds <see cref="IHttpServer"/> instances from the hosts
        /// </summary>
        /// <param name="getServers">A callback method that gets the http server implementation for the service group</param>
        public HttpServiceStackBuilder WithHttp(Func<ServiceGroup, IHttpServer> getServers)
        {
            _getServers = getServers;
            return this;
        }

        /// <summary>
        /// Builds the new <see cref="HttpServiceStack"/> from the configured callbacks, WITHOUT loading plugins
        /// </summary>
        /// <returns>The newly constructed <see cref="HttpServiceStack"/> that may be used to manage your http services</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public HttpServiceStack Build()
        {
            _ = _hostBuilder ?? throw new ArgumentNullException("WithDomainBuilder", "You have not configured a service domain configuration callback");
            _ = _getServers ?? throw new ArgumentNullException("WithHttp", "You have not configured a IHttpServer configuration callback");

            //Inint the service domain
            ServiceDomain sd = new();
            try
            {
                if (!sd.BuildDomain(_hostBuilder))
                {
                    throw new ArgumentException("Failed to configure the service domain, you must expose at least one service host");
                }

                LinkedList<IHttpServer> servers = new();

                //enumerate hosts groups
                foreach (ServiceGroup hosts in sd.ServiceGroups)
                {
                    //Create new server
                    IHttpServer server = _getServers.Invoke(hosts);

                    //Add server to internal list
                    servers.AddLast(server);
                }

                //Return the service stack
                return new HttpServiceStack(servers, sd);
            }
            catch
            {
                sd.Dispose();
                throw;
            }
        }
    }
}
