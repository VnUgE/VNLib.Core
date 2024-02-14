/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Security.Cryptography.X509Certificates;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{

    /// <summary>
    /// 
    /// </summary>
    public static class SsBuilderExtensions
    {

        /// <summary>
        /// Creates a new <see cref="IDomainBuilder"/> instance to define your 
        /// virtual hosts using a built-in event processor type
        /// </summary>
        /// <param name="stack"></param>
        /// <returns>The <see cref="IDomainBuilder"/> used to define your service domain</returns>
        public static IDomainBuilder WithDomain(this HttpServiceStackBuilder stack) => WithDomain(stack, vhc => FromVirtualHostConfig(vhc.Clone()));

        /// <summary>
        /// Creates a new <see cref="IDomainBuilder"/> instance to define your
        /// virtual hosts with the supplied callback method
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="domainBuilder">The callback function to pass the domain builder to</param>
        /// <returns>The service stack builder instance</returns>
        public static HttpServiceStackBuilder WithDomain(this HttpServiceStackBuilder stack, Action<IDomainBuilder> domainBuilder)
        {
            domainBuilder(stack.WithDomain());
            return stack;
        }

        /// <summary>
        /// Creates a new <see cref="IDomainBuilder"/> with your custom <see cref="EventProcessor"/> type
        /// that will be wrapped for runtime processing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stack"></param>
        /// <param name="callback">The custom event processor type</param>
        /// <returns></returns>
        public static IDomainBuilder WithDomain<T>(this HttpServiceStackBuilder stack, Func<VirtualHostConfiguration, T> callback) 
            where T : EventProcessor, IRuntimeServiceInjection
        {
            List<VirtualHostConfiguration> configs = new();
            DomainBuilder domains = new(configs, stack);

            //Add callback to capture this collection of configs when built
            stack.AddHosts(() => configs.Select(c => new CustomServiceHost<T>(c.Clone(), callback(c))).ToArray());

            return domains;
        }

        /// <summary>
        /// Adds a single <see cref="IHttpMiddleware"/> instance to the virtual host
        /// </summary>
        /// <param name="vhBuilder"></param>
        /// <param name="middleware">The middleware instance to add</param>
        /// <returns></returns>
        public static IVirtualHostBuilder WithMiddleware(this IVirtualHostBuilder vhBuilder, IHttpMiddleware middleware)
        {
            vhBuilder.WithOption(c => c.CustomMiddleware.Add(middleware));
            return vhBuilder;
        }

        /// <summary>
        /// Adds multiple <see cref="IHttpMiddleware"/> instances to the virtual host
        /// </summary>
        /// <param name="vhBuilder"></param>
        /// <param name="middleware">The array of middleware instances to add to the collection</param>
        /// <returns></returns>
        public static IVirtualHostBuilder WithMiddleware(this IVirtualHostBuilder vhBuilder, params IHttpMiddleware[] middleware)
        {
            vhBuilder.WithOption(c => Array.ForEach(middleware, m => c.CustomMiddleware.Add(m)));
            return vhBuilder;
        }


        public static IVirtualHostBuilder WithLogger(this IVirtualHostBuilder vhBuilder, ILogProvider logger)
        {
            vhBuilder.WithOption(c => c.LogProvider = logger);
            return vhBuilder;
        }

        public static IVirtualHostBuilder WithEndpoint(this IVirtualHostBuilder vhBuilder, IPEndPoint endpoint)
        {
            vhBuilder.WithOption(c => c.TransportEndpoint = endpoint);
            return vhBuilder;
        }

        public static IVirtualHostBuilder WithTlsCertificate(this IVirtualHostBuilder vhBuilder, X509Certificate? cert)
        {
            vhBuilder.WithOption(c => c.Certificate = cert);
            return vhBuilder;
        }

        public static IVirtualHostBuilder WithHostname(this IVirtualHostBuilder virtualHostBuilder, string hostname)
        {
            virtualHostBuilder.WithOption(c => c.Hostname = hostname);
            return virtualHostBuilder;
        }

        public static IVirtualHostBuilder WithDefaultFiles(this IVirtualHostBuilder vhBuidler, params string[] defaultFiles)
        {
            return vhBuidler.WithDefaultFiles((IReadOnlyCollection<string>)defaultFiles);
        }

        public static IVirtualHostBuilder WithDefaultFiles(this IVirtualHostBuilder vhBuidler, IReadOnlyCollection<string> defaultFiles)
        {
            vhBuidler.WithOption(c => c.DefaultFiles = defaultFiles);
            return vhBuidler;
        }

        public static IVirtualHostBuilder WithExcludedExtensions(this IVirtualHostBuilder vhBuilder, params string[] excludedExtensions)
        {
            return vhBuilder.WithExcludedExtensions(new HashSet<string>(excludedExtensions));
        }

        public static IVirtualHostBuilder WithExcludedExtensions(this IVirtualHostBuilder vhBuilder, IReadOnlySet<string> excludedExtensions)
        {
            vhBuilder.WithOption(c => c.ExcludedExtensions = excludedExtensions);
            return vhBuilder;
        }

        public static IVirtualHostBuilder WithAllowedAttributes(this IVirtualHostBuilder vhBuilder, FileAttributes attributes)
        {
            vhBuilder.WithOption(c => c.AllowedAttributes = attributes);
            return vhBuilder;
        }

        public static IVirtualHostBuilder WithDisallowedAttributes(this IVirtualHostBuilder vhBuilder, FileAttributes attributes)
        {
            vhBuilder.WithOption(c => c.DissallowedAttributes = attributes);
            return vhBuilder;
        }

        public static IVirtualHostBuilder WithDownstreamServers(this IVirtualHostBuilder vhBuilder, IReadOnlySet<IPAddress> addresses)
        {
            vhBuilder.WithOption(c => c.DownStreamServers = addresses);
            return vhBuilder;
        }

        /// <summary>
        /// Adds an array of IP addresses to the downstream server collection. This is a security 
        /// features that allows event handles to trust connections/ipaddresses that originate from
        /// trusted downstream servers
        /// </summary>
        /// <param name="vhBuilder"></param>
        /// <param name="addresses">The collection of IP addresses to set as trusted servers</param>
        /// <returns></returns>
        public static IVirtualHostBuilder WithDownstreamServers(this IVirtualHostBuilder vhBuilder, params IPAddress[] addresses)
        {
            vhBuilder.WithOption(c => c.DownStreamServers = new HashSet<IPAddress>(addresses));
            return vhBuilder;
        }

        private static BasicVirtualHost FromVirtualHostConfig(VirtualHostConfiguration configuration)
        {
            /*
             * Event processors configurations are considered immutable. That is, 
             * top-level elements are not allowed to be changed after the processor
             * has been created. Some properties/containers are allowed to be modified
             * such as middleware chains, and the service pool.
             */

            EventProcessorConfig conf = new(
                configuration.RootDir.FullName, 
                configuration.Hostname, 
                configuration.LogProvider, 
                configuration)
            {
                AllowedAttributes = configuration.AllowedAttributes,
                DissallowedAttributes = configuration.DissallowedAttributes,
                DefaultFiles = configuration.DefaultFiles,
                ExecutionTimeout = configuration.ExecutionTimeout,

                //Frozen sets are required for the event processor, for performance reasons
                DownStreamServers = configuration.DownStreamServers.ToFrozenSet(),
                ExcludedExtensions = configuration.ExcludedExtensions.ToFrozenSet(),
            };

            //Add all pre-configured middleware to the chain
            configuration.CustomMiddleware.ForEach(conf.MiddlewareChain.Add);

            return new(configuration.EventHooks, conf);
        }


        private static void AddHosts(this HttpServiceStackBuilder stack, Func<IServiceHost[]> hosts) 
            => stack.WithDomain(p => Array.ForEach(hosts(), h => p.Add(h)));

        private static void OnPluginServiceEvent<T>(this IManagedPlugin plugin, Action<T> loader)
        {
            if (plugin.Services.GetService(typeof(T)) is T s)
            {
                loader(s);
            }
        }

        private sealed record class DomainBuilder(List<VirtualHostConfiguration> Configs, HttpServiceStackBuilder Stack) : IDomainBuilder
        {
            ///<inheritdoc/>
            public IVirtualHostBuilder WithVirtualHost(DirectoryInfo rootDirectory, IVirtualHostHooks hooks, ILogProvider logger)
            {
                //Create new config instance and add to list
                VirtualHostConfiguration config = new()
                {
                    EventHooks = hooks,
                    RootDir = rootDirectory,
                    LogProvider = logger
                };
                Configs.Add(config);
                return new VHostBuilder(config);
            }

            ///<inheritdoc/>
            public IDomainBuilder WithVirtualHost(VirtualHostConfiguration config)
            {
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

        private sealed class CustomServiceHost<T>(IHostTransportInfo Config, T Instance) : IServiceHost 
            where T : EventProcessor, IRuntimeServiceInjection
        {
            ///<inheritdoc/>
            public IWebRoot Processor => Instance;

            ///<inheritdoc/>
            public IHostTransportInfo TransportInfo => Config;

            ///<inheritdoc/>
            void IServiceHost.OnRuntimeServiceAttach(IManagedPlugin plugin, IEndpoint[] endpoints)
            {
                //Add endpoints to service
                Instance.Options.EndpointTable.AddEndpoint(endpoints);
                
                //Inject services into the event processor service pool
                Instance.AddServices(plugin.Services);

                //Add all exposed middleware to the chain
                plugin.OnPluginServiceEvent<ICollection<IHttpMiddleware>>(p => p.TryForeach(Instance.Options.MiddlewareChain.Add));
            }

            ///<inheritdoc/>
            void IServiceHost.OnRuntimeServiceDetach(IManagedPlugin plugin, IEndpoint[] endpoints)
            {
                //Remove endpoints
                Instance.Options.EndpointTable.RemoveEndpoint(endpoints);
                Instance.RemoveServices(plugin.Services);

                //Remove all middleware from the chain
                plugin.OnPluginServiceEvent<ICollection<IHttpMiddleware>>(p => p.TryForeach(Instance.Options.MiddlewareChain.Remove));
            }
        }


        private sealed class BasicVirtualHost(IVirtualHostHooks Hooks, EventProcessorConfig config) : EventProcessor(config), IRuntimeServiceInjection
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
            public override bool ErrorHandler(HttpStatusCode errorCode, IHttpEvent entity) => Hooks.ErrorHandler(errorCode, entity);

            ///<inheritdoc/>
            public override void PreProcessEntity(HttpEntity entity, out FileProcessArgs preProcArgs) => Hooks.PreProcessEntityAsync(entity, out preProcArgs);

            ///<inheritdoc/>
            public override void PostProcessEntity(HttpEntity entity, ref FileProcessArgs chosenRoutine) => Hooks.PostProcessFile(entity, ref chosenRoutine);

            ///<inheritdoc/>
            public override string TranslateResourcePath(string requestPath) => Hooks.TranslateResourcePath(requestPath);

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
