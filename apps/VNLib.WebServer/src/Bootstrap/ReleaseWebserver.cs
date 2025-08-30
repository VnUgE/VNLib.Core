/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: ReleaseWebserver.cs 
*
* ReleaseWebserver.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Data;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Net.Http;
using VNLib.Plugins.Runtime;

using VNLib.WebServer.Config;
using VNLib.WebServer.Config.Model;
using VNLib.WebServer.Plugins;
using VNLib.WebServer.Compression;
using VNLib.WebServer.Middlewares;
using VNLib.WebServer.RuntimeLoading;
using VNLib.WebServer.VirtualHosts;
using static VNLib.WebServer.Entry;

namespace VNLib.WebServer.Bootstrap
{

    /*
     * This class represents a normally loaded "Relase" webserver to allow 
     * for module webserver use-cases. It relies on a system configuration
     * file and command line arguments to configure the server.
     */

    internal class ReleaseWebserver(ServerLogger logger, IServerConfig config, ProcessArguments procArgs)
        : WebserverBase(logger, config, procArgs)
    {

        private const string PLUGIN_DATA_TEMPLATE =
@"
----------------------------------
 |      Plugin configuration:
 | Enabled: {enabled}
 | Directory: {dir}
 | Hot Reload: {hr}
 | Reload Delay: {delay}s
----------------------------------";

        private readonly ProcessArguments args = procArgs;

        ///<inheritdoc/>
        protected override PluginStackBuilder? ConfigurePlugins()
        {
            //do not load plugins if disabled
            if (args.HasArgument("--no-plugins"))
            {
                logger.AppLog.Information("Plugin loading disabled via command-line flag");
                return null;
            }

            ServerPluginConfig? conf = config.GetConfigProperty<ServerPluginConfig>(PLUGINS_CONFIG_PROP_NAME);
            if (conf is null)
            {
                logger.AppLog.Debug("No plugin configuration found");
                return null;
            }

            if (!conf.Enabled)
            {
                logger.AppLog.Information("Plugin loading disabled via configuration flag");
                return null;
            }

            //Init new plugin stack builder
            PluginStackBuilder pluginBuilder = PluginStackBuilder.Create()
                                    .WithDebugLog(logger.AppLog)
                                    .WithSearchDirectories([ conf.Path ])
                                    .WithLoaderFactory(PluginAsemblyLoading.Create);

            //Setup plugin config data
            if (!string.IsNullOrWhiteSpace(conf.ConfigDir))
            {
                pluginBuilder.WithJsonConfigDir(
                    hostConfig: config.GetDocumentRoot(), 
                    configDir: new (conf.ConfigDir)
                );
            }
            else
            {
                pluginBuilder.WithLocalJsonConfig(config.GetDocumentRoot());
            }

            if (conf.HotReload)
            {
                Validate.EnsureRange(conf.ReloadDelaySec, 1, 120);

                pluginBuilder.EnableHotReload(TimeSpan.FromSeconds(conf.ReloadDelaySec));
            }

            logger.AppLog.Information(
                PLUGIN_DATA_TEMPLATE,
                true,
                conf.Path,
                conf.HotReload,
                conf.ReloadDelaySec
            );

            if (conf.HotReload)
            {
                logger.AppLog.Warn("Plugin hot-reload is not recommended for production deployments!");
            }

            return pluginBuilder;
        }

        ///<inheritdoc/>
        protected override HttpConfig GetHttpConfig()
        {
            try
            {
                HttpGlobalConfig? gConf = config.GetConfigProperty<HttpGlobalConfig>("http");
                Validate.EnsureNotNull(gConf, "Missing required HTTP configuration variables");

                //Attempt to load the compressor manager, if null, compression is disabled
                IHttpCompressorManager? compressorManager = HttpCompressor.LoadOrDefaultCompressor(procArgs, gConf.Compression, config, logger.AppLog);

                IHttpMemoryPool memPool = MemoryPoolManager.GetHttpPool(procArgs.ZeroAllocations, MemoryUtil.Shared);

                HttpConfig conf = new(Encoding.ASCII)
                {
                    ActiveConnectionRecvTimeout     = gConf.RecvTimeoutMs,
                    CompressorManager               = compressorManager,
                    ConnectionKeepAlive             = TimeSpan.FromMilliseconds(gConf.KeepAliveMs),
                    CompressionLimit                = gConf.Compression.CompressionMax,                    
                    CompressionMinimum              = gConf.Compression.CompressionMin,
                    DebugPerformanceCounters        = procArgs.HasArgument("--http-counters"),
                    DefaultHttpVersion              = HttpHelpers.ParseHttpVersion(gConf.DefaultHttpVersion),
                    MaxFormDataUploadSize           = gConf.MultipartMaxSize,
                    MaxUploadSize                   = gConf.MaxEntitySize,
                    MaxRequestHeaderCount           = gConf.MaxRequestHeaderCount,                    
                    MaxOpenConnections              = gConf.MaxConnections,                    
                    MaxUploadsPerRequest            = gConf.MaxUploadsPerRequest,
                    SendTimeout                     = gConf.SendTimeoutMs,
                    ServerLog                       = logger.SysLog,
                    MemoryPool                      = memPool,

                    RequestDebugLog                 = procArgs.LogHttp ? logger.AppLog : null,

                    //Buffer config update
                    BufferConfig = new()
                    {
                        RequestHeaderBufferSize     = gConf.RequestHeaderBufSize,
                        ResponseHeaderBufferSize    = gConf.ResponseHeaderBufSize,
                        FormDataBufferSize          = gConf.MultipartMaxBufSize,

                        //Align response buffer size with transport buffer to avoid excessive copy
                        ResponseBufferSize          = TcpConfig.TcpTxBufferSize, 

                        /*
                         * Chunk buffers are written to the transport when they are fully accumulated. These buffers
                         * should be aligned with the transport sizes. It should also be large enough not to put too much
                         * back pressure on compressors. This buffer will be segmented into smaller buffers if it has to
                         * at the transport level, but we should avoid that if possible due to multiple allocations and 
                         * copies.
                         * 
                         * Aligning chunk buffer to the transport buffer size is the easiest solution to avoid excessive
                         * copyies
                         */
                        ChunkedResponseAccumulatorSize = compressorManager != null ? TcpConfig.TcpTxBufferSize : 0
                    },
                   
                };

                Validate.Assert(
                    condition: conf.DefaultHttpVersion != HttpVersion.None,
                    message: "Your default HTTP version is invalid, specify an RFC formatted http version 'HTTP/x.x'"
                );

                return conf;
            }
            catch (KeyNotFoundException kne)
            {
                logger.AppLog.Error("Missing required HTTP configuration variables {var}", kne.Message);
                throw new ServerConfigurationException("Missing required http variables. Cannot continue");
            }
        }

        ///<inheritdoc/>
        protected override VirtualHostConfig[] GetAllVirtualHosts()
        {
            ILogProvider log = logger.AppLog;

            LinkedList<VirtualHostConfig> configs = new();

            try
            {
                int index = 0;

                //Enumerate all virtual host configurations
                foreach (VirtualHostServerConfig vhConfig in GetVirtualHosts())
                {
               
                    VirtualHostConfig conf = new JsonWebConfigBuilder(vhConfig, log).GetBaseConfig();

                    //Configure event hooks
                    conf.EventHooks = new VirtualHostHooks(conf);

                    //Init middleware stack
                    conf.CustomMiddleware.Add(new MainServerMiddlware(log, conf, vhConfig.ForcePortCheck));

                    /*
                     * In benchmark mode, skip other middleware that might slow connections down
                     */
                    if (vhConfig.Benchmark?.Enabled == true)
                    {
                        conf.CustomMiddleware.Add(new BenchmarkMiddleware(vhConfig.Benchmark));
                        log.Information("BENCHMARK: Enabled for virtual host {vh}", conf.Hostnames);
                    }
                    else
                    {
                        /*
                         * We only enable cors if the configuration has a value for the allow cors property.
                         * The user may disable cors totally, deny cors requests, or enable cors with a whitelist
                         * 
                         * Only add the middleware if the confg has a value for the allow cors property
                         */
                        if (vhConfig.Cors?.Enabled == true)
                        {
                            conf.CustomMiddleware.Add(new CORSMiddleware(log, vhConfig.Cors));
                        }

                        //Add whitelist middleware if the configuration has a whitelist
                        if (conf.WhiteList != null)
                        {
                            conf.CustomMiddleware.Add(new IpWhitelistMiddleware(log, conf.WhiteList));
                        }

                        //Add blacklist middleware if the configuration has a blacklist
                        if (conf.BlackList != null)
                        {
                            conf.CustomMiddleware.Add(new IpBlacklistMiddleware(log, conf.BlackList));
                        }

                        //Add tracing middleware if enabled
                        if (vhConfig.RequestTrace)
                        {
                            conf.CustomMiddleware.Add(new ConnectionLogMiddleware(log));
                        }
                    }

                    if (!conf.RootDir.Exists)
                    {
                        conf.RootDir.Create();
                    }

                    configs.AddLast(conf);

                    index++;
                }
            }
            catch (KeyNotFoundException kne)
            {
                throw new ServerConfigurationException("Missing required configuration varaibles", kne);
            }
            catch (FormatException fe)
            {
                throw new ServerConfigurationException("Failed to parse IP address", fe);
            }

            return configs.ToArray();
        }

        private VirtualHostServerConfig[] GetVirtualHosts()
        {
            ILogProvider log = logger.AppLog;

            VirtualHostServerConfig[]? hosts = config.GetConfigProperty<VirtualHostServerConfig[]>("virtual_hosts");
            if (hosts is null)
            {
                log.Warn("No virtual hosts array was defined. Continuing without hosts");
                return [];
            }

            //Only get enabled hosts
            return hosts
                .Where(static conf => conf != null)
                .Where(static conf => conf.Enabled)
                .ToArray();
        }
    }
}
