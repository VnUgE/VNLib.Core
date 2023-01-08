/*
* Copyright (c) 2022 Vaughn Nugent
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

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// A data structure used to build/create a <see cref="HttpServiceStack"/>
    /// around a <see cref="ServiceDomain"/>
    /// </summary>
    public sealed class HttpServiceStackBuilder
    {
        private readonly LinkedList<HttpServer> _servers;

        /// <summary>
        /// The built <see cref="HttpServiceStack"/>
        /// </summary>
        public HttpServiceStack ServiceStack { get; }

        /// <summary>
        /// Gets the underlying <see cref="ServiceDomain"/>
        /// </summary>
        public ServiceDomain ServiceDomain { get; }

        /// <summary>
        /// Initializes a new <see cref="HttpServiceStack"/> that will 
        /// generate servers to listen for services exposed by the 
        /// specified host context
        /// </summary>
        public HttpServiceStackBuilder()
        {
            ServiceDomain = new();
            _servers = new();
            ServiceStack = new(_servers, ServiceDomain);
        }

        /// <summary>
        /// Builds all http servers from 
        /// </summary>
        /// <param name="config">The http server configuration to user for servers</param>
        /// <param name="getTransports">A callback method that gets the transport provider for the given host group</param>
        public void BuildServers(in HttpConfig config, Func<ServiceGroup, ITransportProvider> getTransports)
        {
            //enumerate hosts groups
            foreach (ServiceGroup hosts in ServiceDomain.ServiceGroups)
            {
                //get transport for provider
                ITransportProvider transport = getTransports.Invoke(hosts);

                //Create new server
                HttpServer server = new(config, transport, hosts.Hosts.Select(static h => h.Processor as IWebRoot));

                //Add server to internal list
                _servers.AddLast(server);
            }
        }

        /// <summary>
        /// Releases any resources that may be held by the <see cref="ServiceDomain"/>
        /// incase of an error
        /// </summary>
        public void ReleaseOnError()
        {
            ServiceStack.Dispose();
        }
    }
}
