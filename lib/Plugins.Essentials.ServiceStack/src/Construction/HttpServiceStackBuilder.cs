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

using VNLib.Net.Http;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Essentials.ServiceStack.Plugins;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{

    /// <summary>
    /// A data structure used to build/create a <see cref="HttpServiceStack"/>
    /// around a <see cref="ServiceDomain"/>
    /// </summary>
    public sealed class HttpServiceStackBuilder
    {
        private readonly ServiceBuilder _serviceBuilder = new();

        /// <summary>
        /// Initializes a new <see cref="HttpServiceStack"/> that will 
        /// generate servers to listen for services exposed by the 
        /// specified host context
        /// </summary>
        public HttpServiceStackBuilder()
        { }

        internal ServiceBuilder ServiceBuilder => _serviceBuilder;

        private Action<ServiceBuilder>? _hostBuilder;
        private Func<IReadOnlyCollection<ServiceGroup>, IHttpServer[]>? _getServers;
        private Func<IPluginStack>? _getPlugins;
        private IManualPlugin[]? manualPlugins;
        private bool loadConcurrently;

        /// <summary>
        /// Uses the supplied callback to get a collection of virtual hosts
        /// to build the current domain with
        /// </summary>
        /// <param name="hostBuilder">The callback method to build virtual hosts</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithDomain(Action<ServiceBuilder> hostBuilder)
        {
            _hostBuilder = hostBuilder;
            return this;
        }

        /// <summary>
        /// Spcifies a callback function that builds <see cref="IHttpServer"/> instances from the hosts
        /// </summary>
        /// <param name="getServers">A callback method that gets the http server implementation for the service group</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithHttp(Func<IReadOnlyCollection<ServiceGroup>, IHttpServer[]> getServers)
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
        /// <param name="getTransports">The transport builder callback function</param>
        /// <param name="config">The http configuration structure used to initalize servers</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithBuiltInHttp(Func<IReadOnlyCollection<ServiceGroup>, HttpTransportMapping[]> getTransports, HttpConfig config) 
            => WithBuiltInHttp(getTransports, _ => config);

        /// <summary>
        /// Configures the stack to use the built-in http server implementation
        /// </summary>
        /// <param name="getBindings">A callback function that gets transport bindings for servie groups</param>
        /// <param name="configCallback">The http configuration builder callback method</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder WithBuiltInHttp(
            Func<IReadOnlyCollection<ServiceGroup>, HttpTransportMapping[]> getBindings,
            Func<IReadOnlyCollection<ServiceGroup>, HttpConfig> configCallback
        ) => WithHttp((sgs) => {

            HttpTransportBinding[] vhBindings = getBindings(sgs)
                .Select(s =>
                {
                    IEnumerable<IWebRoot> procs = s.Hosts.Select(static s => s.Processor);
                    return new HttpTransportBinding(s.Transport, procs);
                })
                .ToArray();

            // A single built-in http server can service an entire domain
            return [ new HttpServer(configCallback(sgs), vhBindings) ];
        });

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
        /// Sets the load concurrency flag for the plugin stack
        /// </summary>
        /// <param name="value">True to enable concurrent loading, false for serial loading</param>
        /// <returns>The current instance for chaining</returns>
        public HttpServiceStackBuilder LoadPluginsConcurrently(bool value)
        {
            loadConcurrently = value;
            return this;
        }

        /// <summary>
        /// Builds the new <see cref="HttpServiceStack"/> from the configured callbacks
        /// </summary>
        /// <returns>The newly constructed <see cref="HttpServiceStack"/> that may be used to manage your http services</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public HttpServiceStack Build()
        {
            _ = _getServers ?? throw new ArgumentNullException("WithHttp", "You have not configured a IHttpServer configuration callback");

            //Host builder callback is optional
            _hostBuilder?.Invoke(ServiceBuilder);

            //Inint the service domain
            ServiceDomain sd = new();

            sd.BuildDomain(_serviceBuilder);

            //Get http servers from the user callback for the service domain, let the caller decide how to route them
            IHttpServer[] servers = _getServers.Invoke(sd.ServiceGroups);

            if (servers.Length == 0)
            {
                throw new ArgumentException("No service hosts were configured. You must define at least one virtual host for the domain");
            }

            if(servers.Any(servers => servers is null))
            {
                throw new ArgumentException("One or more servers were not initialized correctly. Check the server configuration callback");
            }

            return new(servers, sd, GetPluginStack(sd));
        }

        private PluginStackInitializer GetPluginStack(ServiceDomain domain)
        {
            //Always init manual array
            manualPlugins ??= [];

            //Only load plugins if the callback is configured
            IPluginStack? plugins = _getPlugins?.Invoke();

#pragma warning disable CA2000 // Dispose objects before losing scope
            plugins ??= new EmptyPluginStack();
#pragma warning restore CA2000 // Dispose objects before losing scope

            return new (domain.GetListener(), plugins, manualPlugins, loadConcurrently);
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
