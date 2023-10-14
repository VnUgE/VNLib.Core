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
using System.Linq;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Plugins.Runtime;


namespace VNLib.Plugins.Essentials.ServiceStack.Construction
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
        { }

        private Action<ICollection<IServiceHost>>? _hostBuilder;
        private Func<ServiceGroup, IHttpServer>? _getServers;
        private Func<IPluginStack>? _getPlugins;
        private IManualPlugin[]? manualPlugins;

        /// <summary>
        /// Uses the supplied callback to get a collection of virtual hosts
        /// to build the current domain with
        /// </summary>
        /// <param name="hostBuilder">The callback method to build virtual hosts</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithDomain(Action<ICollection<IServiceHost>> hostBuilder)
        {
            _hostBuilder = hostBuilder;
            return this;
        }

        /// <summary>
        /// Spcifies a callback function that builds <see cref="IHttpServer"/> instances from the hosts
        /// </summary>
        /// <param name="getServers">A callback method that gets the http server implementation for the service group</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithHttp(Func<ServiceGroup, IHttpServer> getServers)
        {
            _getServers = getServers;
            return this;
        }

        /// <summary>
        /// Enables the stack to support plugins
        /// </summary>
        /// <param name="getStack">The callback function that returns the plugin stack when requested</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithPluginStack(Func<IPluginStack> getStack)
        {
            _getPlugins = getStack;
            return this;
        }

        /// <summary>
        /// Configures the stack to use the built-in http server implementation
        /// </summary>
        /// <param name="transport">The transport builder callback function</param>
        /// <param name="config">The http configuration structure used to initalize servers</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithBuiltInHttp(Func<ServiceGroup, ITransportProvider> transport, HttpConfig config)
        {
            return WithBuiltInHttp(transport, sg => config);
        }

        /// <summary>
        /// Configures the stack to use the built-in http server implementation
        /// </summary>
        /// <param name="transport">The transport builder callback function</param>
        /// <param name="configCallback">The http configuration builder callback method</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithBuiltInHttp(Func<ServiceGroup, ITransportProvider> transport, Func<ServiceGroup, HttpConfig> configCallback)
        {
            return WithHttp(sg => new HttpServer(configCallback(sg), transport(sg), sg.Hosts.Select(static p => p.Processor)));
        }

        /// <summary>
        /// Adds a collection of manual plugin instances to the stack. Every call 
        /// to this method will replace the previous collection.
        /// </summary>
        /// <param name="plugins">The array of plugins (or params) to add</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithManualPlugins(params IManualPlugin[] plugins)
        {
            manualPlugins = plugins;
            return this;
        }

        /// <summary>
        /// Builds the new <see cref="HttpServiceStack"/> from the configured callbacks
        /// </summary>
        /// <returns>The newly constructed <see cref="HttpServiceStack"/> that may be used to manage your http services</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public HttpServiceStack Build()
        {
            _ = _hostBuilder ?? throw new ArgumentNullException("WithDomainBuilder", "You have not configured a service domain configuration callback");
            _ = _getServers ?? throw new ArgumentNullException("WithHttp", "You have not configured a IHttpServer configuration callback");

            //Inint the service domain
            ServiceDomain sd = new();

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

            //Always init manual array
            manualPlugins ??= Array.Empty<IManualPlugin>();

            //Only load plugins if the callback is configured
            IPluginStack? plugins = _getPlugins?.Invoke();

#pragma warning disable CA2000 // Dispose objects before losing scope
            plugins ??= new EmptyPluginStack();
#pragma warning restore CA2000 // Dispose objects before losing scope

            IPluginInitializer init = new PluginStackInitializer(plugins, manualPlugins);

            return new(servers, sd, init);
        }

        /*
         * An empty plugin stack that is used when the plugin callback is not configured
         */
        private sealed class EmptyPluginStack : IPluginStack
        {
            public IReadOnlyCollection<RuntimePluginLoader> Plugins { get; } = Array.Empty<RuntimePluginLoader>();

            public void BuildStack()
            { }

            public void Dispose()
            { }
        }
    }
}
