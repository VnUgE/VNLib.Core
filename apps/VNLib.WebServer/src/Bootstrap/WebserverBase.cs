/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: WebserverBase.cs 
*
* WebserverBase.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Essentials.ServiceStack;
using VNLib.Plugins.Essentials.ServiceStack.Construction;

using VNLib.WebServer.Config;
using VNLib.WebServer.Transport;
using VNLib.WebServer.RuntimeLoading;

namespace VNLib.WebServer.Bootstrap
{

    internal abstract class WebserverBase(ServerLogger logger, IServerConfig config, ProcessArguments procArgs) 
        : VnDisposeable
    {

        protected readonly ProcessArguments procArgs = procArgs;
        protected readonly IServerConfig config = config;
        protected readonly ServerLogger logger = logger;
        protected readonly TcpServerLoader TcpConfig = new(config, procArgs, logger.SysLog);

        private HttpServiceStack? _serviceStack;

        /// <summary>
        /// Gets the internal <see cref="HttpServiceStack"/> this 
        /// controller is managing
        /// </summary>
        public HttpServiceStack ServiceStack
        {
            get
            {
                if (_serviceStack is null)
                {
                    throw new InvalidOperationException("Service stack has not been configured yet");
                }

                return _serviceStack;
            }
        }

        /// <summary>
        /// Configures the http server for the application so
        /// its ready to start
        /// </summary>
        public virtual void Configure()
        {
            _serviceStack = ConfiugreServiceStack();
        }

        protected virtual HttpServiceStack ConfiugreServiceStack()
        {
            bool loadPluginsConcurrently = !procArgs.HasArgument("--sequential-load");

            JsonElement conf = config.GetDocumentRoot();

            HttpConfig http = GetHttpConfig();

            VirtualHostConfig[] virtualHosts = GetAllVirtualHosts();

            PluginStackBuilder? plugins = ConfigurePlugins();

            HttpServiceStackBuilder builder = new HttpServiceStackBuilder()
                                    .LoadPluginsConcurrently(loadPluginsConcurrently)
                                    .WithBuiltInHttp(TcpConfig.ReduceBindingsForGroups, http)
                                    .WithDomain(domain =>
                                    {
                                        domain.WithServiceGroups(vh =>
                                        {
                                            /*
                                             * Must pass the virtual host configuration as the state object
                                             * so transport providers can be loaded from a given virtual host
                                             */
                                            virtualHosts.ForEach(vhConfig => vh.WithVirtualHost(vhConfig, vhConfig));
                                        });
                                    });

            if (plugins != null)
            {
                builder.WithPluginStack(plugins.ConfigureStack);
            }

            PrintLogicalRouting(virtualHosts);

            return builder.Build();
        }

        protected abstract VirtualHostConfig[] GetAllVirtualHosts();

        protected abstract HttpConfig GetHttpConfig();

        protected abstract PluginStackBuilder? ConfigurePlugins();

        /// <summary>
        /// Starts the server and returns immediately 
        /// after server start listening
        /// </summary>
        public void Start()
        {
            /* Since this api is uses internally, knowing the order of operations is a bug, not a rumtime accident */
            Debug.Assert(Disposed == false, "Server was disposed");
            Debug.Assert(_serviceStack != null, "Server was not configured");

            //Attempt to load plugins before starting server
            _serviceStack.LoadPlugins(logger.AppLog);

            _serviceStack.StartServers();
        }

        /// <summary>
        /// Stops the server and waits for all connections to close and
        /// servers to fully shut down
        /// </summary>
        public void Stop()
        {
            Debug.Assert(Disposed == false, "Server was disposed");
            Debug.Assert(_serviceStack != null, "Server was not configured");

            //Stop the server and wait synchronously
            _serviceStack.StopAndWaitAsync()
                .GetAwaiter()
                .GetResult();
        }

        private void PrintLogicalRouting(VirtualHostConfig[] hosts)
        {
            const string header =@" 
===================================================
          --- HTTP Service Domain ---

    {enabledRoutes} routes enabled  
";

            VariableLogFormatter sb = new(logger.AppLog, Utils.Logging.LogLevel.Information);
            sb.AppendFormat(header, hosts.Length);

            foreach (VirtualHostConfig host in hosts)
            {
                sb.AppendLine();

                sb.AppendFormat("Virtual Host: {hostnames}\n", (object)host.Hostnames);
                sb.AppendFormat(" Root directory {rdir}\n", host.RootDir);
                sb.AppendLine();

                //Print interfaces
               
                string[] interfaces = host.Transports
                    .Select(i =>$" - {i.Address}:{i.Port} TLS: {i.Ssl}, Client cert: {i.ClientCertRequired}, OS Ciphers: {i.UseOsCiphers}")
                    .ToArray();

                sb.AppendLine(" Interfaces:");
                sb.AppendFormat("{interfaces}", string.Join("\n", interfaces));
                sb.AppendLine();

                sb.AppendLine(" Options:");
                sb.AppendFormat(" - Whitelist: {wl}\n", host.WhiteList);
                sb.AppendFormat(" - Blacklist: {bl}\n", host.BlackList);
                sb.AppendFormat(" - Path filter: {filter}\n", host.PathFilter);
                sb.AppendFormat(" - Cache default time: {cache}\n", host.CacheDefault);
                sb.AppendFormat(" - Cached error files: {files}\n", host.FailureFiles.Select(static p => (int)p.Key));
                sb.AppendFormat(" - Downstream servers: {dsServers}\n", host.DownStreamServers);
                sb.AppendFormat(" - Middlewares loaded {mw}\n", host.CustomMiddleware.Count);
                sb.AppendLine();

                sb.Flush();
            }
        }
       

        ///<inheritdoc/>
        protected override void Free() => _serviceStack?.Dispose();
    }
}
