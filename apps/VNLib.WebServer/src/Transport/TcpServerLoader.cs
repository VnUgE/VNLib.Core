/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: TcpServerLoader.cs 
*
* TcpServerLoader.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

using VNLib.Net.Http;
using VNLib.Net.Transport.Tcp;
using VNLib.Plugins.Essentials.ServiceStack;
using VNLib.Plugins.Essentials.ServiceStack.Construction;
using VNLib.Utils.Logging;
using VNLib.Utils.Memory;
using VNLib.Utils.Resources;
using VNLib.WebServer.Bootstrap;
using VNLib.WebServer.Config;
using VNLib.WebServer.Config.Model;
using VNLib.WebServer.RuntimeLoading;
using VNLib.WebServer.VirtualHosts;

namespace VNLib.WebServer.Transport
{
    internal sealed class TcpServerLoader(IServerConfig hostConfig, ProcessArguments args, ILogProvider tcpLogger)
    {
        const int CacheQuotaDefault = 0;    //Disable cache quota by default, allows unlimited cache


        private readonly LazyInitializer<TcpConfigJson> _conf = new(() =>
        {
            TcpConfigJson conf = hostConfig.GetConfigProperty<TcpConfigJson>(Entry.TCP_CONF_PROP_NAME) ?? new TcpConfigJson();

            conf.ReuseAddress |= args.HasArgument("--reuse-address");
            conf.ReusePort    |= args.HasArgument("--reuse-port");          //TODO: currently not supported
            conf.ReuseSocket  |= !args.HasArgument("--no-reuse-socket");    //Always attempt to reuse socket unless explicitly disabled

            string? ThreadCountArg = args.GetArgument("-t") ?? args.GetArgument("--threads");
            if (uint.TryParse(ThreadCountArg, out uint threadCount))
            {
                conf.AcceptThreads = threadCount;
            }

            return conf;
        });

        private readonly bool UseInlineScheduler        = args.HasArgument("--inline-scheduler");
        private readonly bool EnableTransportLogging    = args.HasArgument("--log-transport");

        /// <summary>
        /// The user confiugred TCP transmission buffer size
        /// </summary>
        public int TcpTxBufferSize => _conf.Instance.TcpSendBufferSize;

        /// <summary>
        /// The user-conifuigred TCP receive buffer size
        /// </summary>
        public int TcpRxBufferSize => _conf.Instance.TcpRecvBufferSize;

        public HttpTransportMapping[] ReduceBindingsForGroups(IReadOnlyCollection<ServiceGroup> groups)
        {
            /*
             * All transports can be reduced by their endpoints to reduce the number of 
             * TCP server instances that need to be created. 
             * 
             * The following code attempts to reorder the many-to-many set mapping of 
             * transports to virtual hosts into a set of one-to-many http bingings.
             * 
             * Example: 
             * 
             *     Virtual hosts            Transports
             *     ex.com                   0.0.0.0:80 (no ssl)
             *     ex.com                   0.0.0.0:443 (ssl)   (shared)
             *     
             *     ex1.com                  192.168.1.6:80 (no ssl)
             *     ex1.com                  0.0.0.0:443 (ssl)   (shared)
             */

            Dictionary<TransportInterface, ITransportProvider> transportMap = groups
                .SelectMany(static g => g.Hosts)
                .Select(static c => (VirtualHostConfig)c.UserState!)
                .SelectMany(static c => c.Transports)
                .DistinctBy(static t => t.GetHashCode())
                .ToDictionary(static k => k, GetProviderForInterface);

            /*
             * The following code groups virtual hosts that share the same transport 
             * interface and creates a new HttpTransportMapping instance for each
             * group of shared interfaces, pulling the transport provider from the
             * transport map above.
             */

            HttpTransportMapping[] bindings = groups
                .SelectMany(static s => s.Hosts)
                .SelectMany(static host =>
                {
                    //The vhost config is stored as a user-object  on the service host
                    VirtualHostConfig config = (VirtualHostConfig)host.UserState!;

                    return config.Transports.Select(iface => new OneToOneHostMapping(host, iface));
                })
                .GroupBy(static m => m.Interface.GetHashCode())
                .Select(otoMap =>
                {
                    IServiceHost[] sharedTransportHosts = otoMap
                        .Select(static m => m.Host)
                        .ToArray();

                    TransportInterface sharedInterface = otoMap.First().Interface;

                    //Find any duplicate hostnames that share the same transport interface and raise validation exception
                    string[] sharedHostErrors = sharedTransportHosts.GroupBy(static h => h.Processor.Hostname)
                        .Where(static g => g.Count() > 1)
                        .Select(duplicteGroup =>
                        {
                            string hostnames = string.Join(", ", duplicteGroup.Select(h => h.Processor.Hostname));

                            return $"Duplicate hostnames: {hostnames} share the same transport interface {sharedInterface}";
                        })
                        .ToArray();

                    //If any duplicate hostnames are found, raise a validation exception
                    if (sharedHostErrors.Length > 0)
                    {
                        throw new ServerConfigurationException(string.Join('\n', sharedHostErrors));
                    }

                    ITransportProvider mappedTransport = transportMap[sharedInterface];

                    return new HttpTransportMapping(sharedTransportHosts, mappedTransport);
                })
                .ToArray();

            return bindings;
        }
      
        sealed record class OneToOneHostMapping(IServiceHost Host, TransportInterface Interface);
        
        private ITransportProvider GetProviderForInterface(TransportInterface iface)
        {
            if (!uint.TryParse(ThreadCountArg, out uint threadCount))
            {
                threadCount = (uint)Environment.ProcessorCount;
            }

            TcpConfigJson baseConfig = _conf.Instance;
           
            TCPConfig tcpConf = new()
            {
                LocalEndPoint           = iface.GetEndpoint(),
                AcceptThreads           = baseConfig.AcceptThreads,
                CacheQuota              = CacheQuotaDefault,
                Log                     = tcpLogger,
                DebugTcpLog             = EnableTransportLogging,           //Only available in debug logging
                BackLog                 = baseConfig.BackLog,
                MaxConnections          = baseConfig.MaxConnections,
                TcpKeepAliveTime        = baseConfig.TcpKeepAliveTime,
                KeepaliveInterval       = baseConfig.KeepaliveInterval,
                MaxRecvBufferData       = baseConfig.MaxRecvBufferData,
                ReuseSocket             = baseConfig.ReuseSocket,
                OnSocketCreated         = OnSocketConfiguring(iface),
                BufferPool              = MemoryPoolManager.GetTcpPool(args.ZeroAllocations, MemoryUtil.Shared)
            };

            //Print warning message, since inline scheduler is an avanced feature
            if (iface.Ssl && UseInlineScheduler)
            {
                tcpLogger.Debug("[WARN]: Inline scheduler is not available on server {server} when using TLS", tcpConf.LocalEndPoint);
            }

            tcpLogger.Verbose(TransportLogTemplate,
                iface,
                baseConfig.TcpRecvBufferSize,
                baseConfig.TcpSendBufferSize,
                iface.Ssl,
                threadCount,
                tcpConf.MaxConnections,
                tcpConf.BackLog,
                tcpConf.TcpKeepAliveTime > 0 ? $"{tcpConf.TcpKeepAliveTime} sec" : "Disabled"
            );

            //Init new tcp server with/without ssl
            return iface.Ssl
                ? TcpTransport.CreateServer(in tcpConf, ssl: new HostAwareServerSslOptions(iface))
                : TcpTransport.CreateServer(in tcpConf, UseInlineScheduler);

        }


        private Action<Socket> OnSocketConfiguring(TransportInterface iface)
        {
            TcpConfigJson baseConf = _conf.Instance;

            //NoDelay is not supported on TLS connections and may be enabled with global config or local config
            bool noDelay = iface.TcpNoDelay ?? baseConf.NoDelay; //Use the global config value

            //If tls and nodelay warn the user
            if (iface.Ssl && noDelay)
            {
                tcpLogger.Warn("TCP_NODELAY was enabled for {iface} but is not recommended for SSL connections", iface);                
            }

            return serverSock =>
            {
                //Set the socket options for the server socket
                serverSock.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, noDelay);
                serverSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, baseConf.TcpSendBufferSize);
                serverSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, baseConf.TcpRecvBufferSize);
                serverSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, ReuseAddress);
            };
        }
    }
}
