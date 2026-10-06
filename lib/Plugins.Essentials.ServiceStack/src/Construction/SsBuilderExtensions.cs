/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: SsBuilderExtensions.cs 
*
* SsBuilderExtensions.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Middleware;
using VNLib.Plugins.Essentials.Endpoints;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{

    /// <summary>
    /// Extension methods for building and configuring the service domain
    /// </summary>
    public static class SsBuilderExtensions
    {

        /// <summary>
        /// Creates a new <see cref="IDomainBuilder"/> instance to define your
        /// virtual hosts with the supplied callback method
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="domainBuilder">The callback function to pass the domain builder to</param>
        /// <returns>The service stack builder instance</returns>
        public static HttpServiceStackBuilder WithDomain(this HttpServiceStackBuilder stack, Action<IDomainBuilder> domainBuilder)
        {
            domainBuilder(WithDomain(stack));
            return stack;
        }

        /// <summary>
        /// Creates a new <see cref="IDomainBuilder"/> instance to define your 
        /// virtual hosts using a built-in event processor type
        /// </summary>
        /// <param name="stack"></param>
        /// <returns>The <see cref="IDomainBuilder"/> used to define your service domain</returns>
        public static IDomainBuilder WithDomain(this HttpServiceStackBuilder stack) 
            => new DomainBuilder(stack.ServiceBuilder);


        private sealed class DomainBuilder(ServiceBuilder svcBuilder) : IDomainBuilder
        {
            ///<inheritdoc/>
            public IDomainBuilder WithServiceGroups(Action<IServiceGroupBuilder> builder)
            {
                svcBuilder.AddHostCollection((col) =>
                {
                    SvGroupBuilder group = new();

                    builder(group);

                    group.Configs
                        .SelectMany(static vc => FromVirtualHostConfig(vc)
                            .Select(vh => new CustomServiceHost<BasicVirtualHost>(vh, vc.UserState)
                        ))
                        .ForEach(col.Add);  //Force enumeration
                });

                return this;
            }

            ///<inheritdoc/>
            public IDomainBuilder WithHosts(IServiceHost[] hosts)
            {
                svcBuilder.AddHostCollection(col => Array.ForEach(hosts, col.Add));
                return this;
            }

            private static IEnumerable<BasicVirtualHost> FromVirtualHostConfig(VirtualHostConfiguration configuration)
            {
                /*
                 * Configurations are allowed to define multiple hostnames for a single 
                 * virtual host. 
                 */

                return configuration.Hostnames
                    .Select<string, BasicVirtualHost>((string hostname) =>
                    {
                        /*
                         * Event processors configurations are considered immutable. That is, 
                         * top-level elements are not allowed to be changed after the processor
                         * has been created. Some properties/containers are allowed to be modified
                         * such as middleware chains, and the service pool.
                         */

                        EventProcessorConfig conf = new(
                            Directory: configuration.RootDir.FullName,
                            Hostname: hostname,
                            Log: configuration.LogProvider,
                            Options: configuration
                        )
                        {
                            FilePathCacheMaxAge = configuration.FilePathCacheMaxAge,
                        };

                        //Add all pre-configured middleware to the chain
                        configuration.CustomMiddleware.ForEach(conf.MiddlewareChain.Add);

                        return new(configuration.EventHooks, conf);
                    });
            }

            private sealed record class SvGroupBuilder : IServiceGroupBuilder
            {
                internal readonly List<VirtualHostConfiguration> Configs = new();

                ///<inheritdoc/>
                public IVirtualHostBuilder WithVirtualHost(DirectoryInfo rootDirectory, IVirtualHostHooks hooks, ILogProvider logger)
                {
                    //Create new config instance and add to list
                    VirtualHostConfiguration config = new()
                    {
                        RootDir     = rootDirectory,
                        EventHooks  = hooks,
                        LogProvider = logger
                    };
                    Configs.Add(config);
                    return new VHostBuilder(config);
                }

                ///<inheritdoc/>
                public IServiceGroupBuilder WithVirtualHost(Action<IVirtualHostBuilder> builder)
                {
                    //Create new config instance and add to list
                    VirtualHostConfiguration config = new()
                    {
                        RootDir = null!,
                        LogProvider = null!
                    };
                  
                    //Pass the builder to the callback
                    builder(new VHostBuilder(config));

                    return WithVirtualHost(config, null);
                }

                ///<inheritdoc/>
                public IServiceGroupBuilder WithVirtualHost(VirtualHostConfiguration config, object? userState)
                {
                    config.UserState = userState;
                    Configs.Add(config);
                    return this;
                }

                private sealed record class VHostBuilder(VirtualHostConfiguration Config) : IVirtualHostBuilder
                {
                    ///<inheritdoc/>
                    public IVirtualHostBuilder WithOption(Action<VirtualHostConfiguration> configCallback)
                    {
                        configCallback(Config);
                        return this;
                    }
                }
            }
        }


        /*
         * This class wraps an EventProcessor/IServiceBinder implementation to manage
         * the IWebRoot instance served by a virtual host and handle dynamic service
         * binding attach/detach operations.
         */

        private sealed class CustomServiceHost<T>(T Instance, object? userState) : IServiceHost 
            where T : EventProcessor, IServiceBinder
        {
            ///<inheritdoc/>
            public IWebRoot Processor => Instance;

            ///<inheritdoc/>
            public object? UserState => userState;

            ///<inheritdoc/>
            void IServiceHost.OnServiceAttach(IServiceBinding binding) 
                => Instance.Bind(binding);

            ///<inheritdoc/>
            void IServiceHost.OnServiceDetach(IServiceBinding binding) 
                => Instance.Unbind(binding);
        }


        private sealed class BasicVirtualHost(IVirtualHostHooks Hooks, EventProcessorConfig config) 
            : EventProcessor(config), IServiceBinder
        {
            /*
             * Runtime service injection tracks service bindings so installed services can
             * be properly removed when the service is detached. This is required to prevent stale 
             * service references to unloaded plugins.
             */
            private readonly ConditionalWeakTable<IServiceBinding, Action> _exposedTypes = [];

            ///<inheritdoc/>
            public override bool ErrorHandler(HttpStatusCode errorCode, IHttpEvent entity) 
                => Hooks.ErrorHandler(errorCode, entity);

            ///<inheritdoc/>
            public override void PreProcessEntity(HttpEntity entity, out FileProcessArgs preProcArgs) 
                => Hooks.PreProcessEntityAsync(entity, out preProcArgs);

            ///<inheritdoc/>
            public override void PostProcessEntity(HttpEntity entity, ref FileProcessArgs chosenRoutine) 
                => Hooks.PostProcessFile(entity, ref chosenRoutine);

            ///<inheritdoc/>
            public override string TranslateResourcePath(string requestPath) 
                => Hooks.TranslateResourcePath(requestPath);

            ///<inheritdoc/>
            public void Bind(IServiceBinding binding)
            {
                List<Type> exposed              = [];
                IEndpoint[] endpoints           = [];
                IHttpMiddleware[] middleware    = [];

                // Attempt to resolve and expose all services defined by
                // the service binding to the service pool
                foreach (Type type in ServicePool.Types)
                {
                    object? service = binding.Services.GetService(type);

                    if (service is not null)
                    {
                        ServicePool.SetService(type, service);
                        exposed.Add(type);
                    }
                }

                // Attempt to resolve middleware and endpoints defined by the service binding
                if (binding.Services.GetService(typeof(IHttpMiddleware[])) is IHttpMiddleware[] mwArray)
                {
                    middleware = mwArray;
                }
                else if (binding.Services.GetService(typeof(IEnumerable<IHttpMiddleware>)) is IEnumerable<IHttpMiddleware> mwEnumerable)
                {
                    middleware = [.. mwEnumerable];
                }

                // Attempts to recover the virtual endpoint definition from the service
                // collection and add its endpoints to the endpoint table
                if (binding.Services.GetService(typeof(IVirtualEndpointDefinition)) is IVirtualEndpointDefinition definition)
                {
                    endpoints = [.. definition.GetEndpoints()];
                }

                middleware.ForEach(Options.MiddlewareChain.Add);

                Options.EndpointTable.AddEndpoint(endpoints);

                // Stores an action callback to capture the types exposed by this service binding
                // and the endpoints/middleware it added so they can be cleanly removed when the
                // service is detached
                _exposedTypes.Add(binding, () =>
                {
                    exposed.ForEach(t => ServicePool.SetService(t, null));

                    middleware.ForEach(Options.MiddlewareChain.Remove);

                    Options.EndpointTable.RemoveEndpoint(endpoints);
                });
            }

            ///<inheritdoc/>
            public void Unbind(IServiceBinding binding)
            {
                if (_exposedTypes.TryGetValue(binding, out Action? unload))
                {
                    _ = _exposedTypes.Remove(binding);

                    unload();
                }
            }
        }
    }
}
