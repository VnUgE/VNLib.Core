/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.IO;
using System.Net;
using System.Linq;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Middleware;
using VNLib.Plugins.Essentials.ServiceStack.Plugins;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{

    /// <summary>
    /// 
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


        private static void OnPluginServiceEvent<T>(this IManagedPlugin plugin, Action<T> loader)
        {
            if (plugin.Services.GetService(typeof(T)) is T s)
            {
                loader(s);
            }
        }


        /*
         * The goal of this class is to added the extra service injection
         * and manage the IWebRoot instance that will be served by a 
         * webserver
         */

        private sealed class CustomServiceHost<T>(T Instance, object? userState) : IServiceHost 
            where T : EventProcessor, IRuntimeServiceInjection
        {
            ///<inheritdoc/>
            public IWebRoot Processor => Instance;

            ///<inheritdoc/>
            public object? UserState => userState;

            ///<inheritdoc/>
            void IServiceHost.OnRuntimeServiceAttach(IManagedPlugin plugin, IEndpoint[] endpoints)
            {
                //Add endpoints to service
                Instance.Options.EndpointTable.AddEndpoint(endpoints);
                
                //Inject services into the event processor service pool
                Instance.AddServices(plugin.Services);

                //Add all exposed middleware to the chain
                plugin.OnPluginServiceEvent<IEnumerable<IHttpMiddleware>>(p => p.ForEach(Instance.Options.MiddlewareChain.Add));
                plugin.OnPluginServiceEvent<IHttpMiddleware[]>(p => p.ForEach(Instance.Options.MiddlewareChain.Add));
            }

            ///<inheritdoc/>
            void IServiceHost.OnRuntimeServiceDetach(IManagedPlugin plugin, IEndpoint[] endpoints)
            {
                //Remove endpoints
                Instance.Options.EndpointTable.RemoveEndpoint(endpoints);
                Instance.RemoveServices(plugin.Services);

                //Remove all middleware from the chain
                plugin.OnPluginServiceEvent<IEnumerable<IHttpMiddleware>>(p => p.ForEach(Instance.Options.MiddlewareChain.Remove));
                plugin.OnPluginServiceEvent<IHttpMiddleware[]>(p => p.ForEach(Instance.Options.MiddlewareChain.Remove));
            }

        }


        private sealed class BasicVirtualHost(IVirtualHostHooks Hooks, EventProcessorConfig config) 
            : EventProcessor(config), IRuntimeServiceInjection
        {
            /*
             * Runtime service injection can be tricky, at least in my architecture. If all we have 
             * is am IServiceProvider instance, we cannot trust that the services availabe are 
             * exactly the same as the ones initially provided. So we can store the known types
             * that a given service container DID export, and then use that to remove the services
             * when the service provider is removed.
             */
            private readonly ConditionalWeakTable<IServiceProvider, Type[]> _exposedTypes = new();

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
            public void AddServices(IServiceProvider services)
            {
                Type[] exposedForHandler = [];

                foreach (Type type in ServicePool.Types)
                {
                    //Get exported service by the desired type
                    object? service = services.GetService(type);

                    //If its not null, then add it to the service pool
                    if (service is not null)
                    {
                        ServicePool.SetService(type, service);

                        //Add to the exposed types list
                        exposedForHandler = [.. exposedForHandler, type];
                    }
                }

                //Add to the exposed types table
                _exposedTypes.Add(services, exposedForHandler);
            }

            ///<inheritdoc/>
            public void RemoveServices(IServiceProvider services)
            {
                //Get all exposed types for this service provider
                if (_exposedTypes.TryGetValue(services, out Type[]? exposed))
                {                    
                    foreach (Type type in exposed)
                    {
                        ServicePool.SetService(type, null);
                    }

                    //Remove from the exposed types table
                    _exposedTypes.Remove(services);
                }
            }
        }
    }
}
