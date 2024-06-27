/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpServerBase.cs 
*
* HttpServerBase.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

/*
 *  This file is the base of the HTTP server class that provides
 *  consts, statics, fields, and properties of the HttpServer class.
 *  
 *  Processing of HTTP connections and entities is contained in the 
 *  processing partial file.
 *  
 *  Processing is configured to be asynchronous, utilizing .NETs 
 *  asynchronous compilation services. To facilitate this but continue
 *  to use object caching, reusable stores must be usable across threads
 *  to function safely with async programming practices.
 */

/*
 * 6-26-2024
 * 
 * Server has been transformed to ultilse a single configuration to listen 
 * on a map of transport servers and isolate those connections to individual 
 * virtual hosts. It allows multiple virtual hosts to be mapped to a single 
 * transport server, but also allow a many-to-many relationship between
 * transport servers and virtual hosts.
 * 
 * The reason for this is HTTP server resource efficiency. A single HTTP server
 * isolates its caching and memory pools. By sharing caches across transport 
 * bindings, we can still have the security isolation of transport : virtual host
 * but share the resources of the server.
 */

using System;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Frozen;
using System.Collections.Generic;

using VNLib.Utils.Logging;
using VNLib.Utils.Memory.Caching;

using VNLib.Net.Http.Core;

namespace VNLib.Net.Http
{

    /// <summary>
    /// Provides a resource efficient, high performance, single library HTTP(s) server, 
    /// with extensable processors and transport providers.
    /// This class cannot be inherited
    /// </summary>
    public sealed partial class HttpServer : ICacheHolder, IHttpServer
    {
        /// <summary>
        /// The host key that determines a "wildcard" host, meaning the 
        /// default connection handler when an incomming connection has 
        /// not specific route
        /// </summary>
        public const string WILDCARD_KEY = "*";

        /// <summary>
        /// Gets the servers supported http versions
        /// </summary>
        public const HttpVersion SupportedVersions = HttpVersion.Http09 | HttpVersion.Http1 | HttpVersion.Http11;

        /// <summary>
        /// Static discard buffer for destroying data. This buffer must never be read from
        /// </summary>
        internal static readonly Memory<byte> WriteOnlyScratchBuffer = new byte[64 * 1024];

        private readonly ListenerState[] Transports;

        private readonly HttpConfig _config;

        #region caches
        /// <summary>
        /// The cached HTTP1/1 keepalive timeout header value
        /// </summary>
        private readonly string KeepAliveTimeoutHeaderValue;

        /// <summary>
        /// Reusable store for obtaining <see cref="HttpContext"/> 
        /// </summary>
        private readonly ObjectRental<HttpContext> ContextStore;
      
        #endregion

        /// <summary>
        /// The <see cref="HttpConfig"/> for the current server
        /// </summary>
        public ref readonly HttpConfig Config => ref _config;

        /// <summary>
        /// Gets a value indicating whether the server is listening for connections
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Cached supported compression methods
        /// </summary>
        internal readonly CompressionMethod SupportedCompressionMethods;

        private CancellationTokenSource? StopToken;

        /// <summary>
        /// Creates a new <see cref="HttpServer"/> with the specified configration copy (using struct).
        /// Immutable data structures are initialzed.
        /// </summary>
        /// <param name="config">The configuration used to create the instance</param>
        /// <param name="bindings">One to many relational mapping between a transport provider and it's routes</param>
        /// <exception cref="ArgumentException"></exception>
        public HttpServer(HttpConfig config, IEnumerable<HttpTransportBinding> bindings)
        {
            //Validate the configuration
            ValidateConfig(in config);

            _config = config;
            
            //Compile and store the timeout keepalive header
            KeepAliveTimeoutHeaderValue = $"timeout={(int)_config.ConnectionKeepAlive.TotalSeconds}";

            //Map transport listeners to their virtual hosts
            Transports = MapListeners(bindings);

            //Cache supported compression methods, or none if compressor is null
            SupportedCompressionMethods = config.CompressorManager == null 
                ? CompressionMethod.None 
                : config.CompressorManager.GetSupportedMethods();

            //Create a new context store
            ContextStore = ObjectRental.CreateReusable(() => new HttpContext(this, SupportedCompressionMethods));
        }

        private static void ValidateConfig(in HttpConfig conf)
        {
            ArgumentNullException.ThrowIfNull(conf.HttpEncoding, nameof(conf.HttpEncoding));
            ArgumentNullException.ThrowIfNull(conf.ServerLog, nameof(conf.ServerLog));
            ArgumentNullException.ThrowIfNull(conf.MemoryPool, nameof(conf.MemoryPool));

            if (conf.ActiveConnectionRecvTimeout < -1)
            {
                throw new ArgumentException("ActiveConnectionRecvTimeout cannot be less than -1", nameof(conf));
            }

            //We only need to verify the chunk buffer size if compression is enabled, otherwise it will never be used
            if (conf.CompressorManager != null)
            {
                //Chunked data accumulator must be at least 64 bytes (arbitrary value)
                if (conf.BufferConfig.ChunkedResponseAccumulatorSize < 64 || conf.BufferConfig.ChunkedResponseAccumulatorSize == int.MaxValue)
                {
                    throw new ArgumentException("ChunkedResponseAccumulatorSize cannot be less than 64 bytes", nameof(conf));
                }
            }
            else
            {
                if(conf.BufferConfig.ChunkedResponseAccumulatorSize < 0)
                {
                    throw new ArgumentException("ChunkedResponseAccumulatorSize can never be less than 0", nameof(conf));
                }
            }

            if (conf.CompressionLimit < 0)
            {
                throw new ArgumentException("CompressionLimit cannot be less than 0, set to 0 to disable response compression", nameof(conf));
            }

            if (conf.ConnectionKeepAlive < TimeSpan.Zero)
            {
                throw new ArgumentException("ConnectionKeepAlive cannot be less than 0", nameof(conf));
            }

            if (conf.DefaultHttpVersion == HttpVersion.None)
            {
                throw new ArgumentException("DefaultHttpVersion cannot be NotSupported", nameof(conf));
            }

            if (conf.BufferConfig.FormDataBufferSize < 64)
            {
                throw new ArgumentException("FormDataBufferSize cannot be less than 64 bytes", nameof(conf));
            }

            if (conf.BufferConfig.RequestHeaderBufferSize < 128)
            {
                throw new ArgumentException("HeaderBufferSize cannot be less than 128 bytes", nameof(conf));
            }

            if (conf.MaxFormDataUploadSize < 0)
            {
                throw new ArgumentException("MaxFormDataUploadSize cannot be less than 0, set to 0 to disable form-data uploads", nameof(conf));
            }

            if (conf.MaxOpenConnections < 0)
            {
                throw new ArgumentException("MaxOpenConnections cannot be less than 0", nameof(conf));
            }

            if (conf.MaxRequestHeaderCount < 1)
            {
                throw new ArgumentException("MaxRequestHeaderCount cannot be less than 1", nameof(conf));
            }

            if (conf.MaxUploadSize < 0)
            {
                throw new ArgumentException("MaxUploadSize cannot be less than 0", nameof(conf));
            }

            if (conf.BufferConfig.ResponseBufferSize < 64)
            {
                throw new ArgumentException("ResponseBufferSize cannot be less than 64 bytes", nameof(conf));
            }

            if (conf.BufferConfig.ResponseHeaderBufferSize < 128)
            {
                throw new ArgumentException("ResponseHeaderBufferSize cannot be less than 128 bytes", nameof(conf));
            }

            if (conf.SendTimeout < 1)
            {
                throw new ArgumentException("SendTimeout cannot be less than 1 millisecond", nameof(conf));
            }
        }

        private static ListenerState[] MapListeners(IEnumerable<HttpTransportBinding> bindings)
        {
            /*
             * Transform the bindings to individual http listeners
             * which also requires a frozen mapping of hostnames to
             * virtual host
             */

            return bindings.Select(static b => new ListenerState
            {
                OriginServer = b.Transport,

                Roots = b.Roots.ToFrozenDictionary(
                    static r => r.Hostname,
                    static tv => tv,
                    StringComparer.OrdinalIgnoreCase
                ),

                //Yoink the wildcard route if it's set
                DefaultRoute = b.Roots.FirstOrDefault(static r => string.Equals(r.Hostname, WILDCARD_KEY, StringComparison.OrdinalIgnoreCase))
            }).ToArray();
        }

        /// <summary>
        /// Begins listening for connections on configured interfaces for configured hostnames.
        /// </summary>
        /// <param name="cancellationToken">A token used to stop listening for incomming connections and close all open websockets</param>
        /// <returns>A task that resolves when the server has exited</returns>
        /// <exception cref="SocketException"></exception>
        /// <exception cref="ThreadStateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public Task Start(CancellationToken cancellationToken)
        {
            StopToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            //Start servers with the new token source before listening for connections
            Array.ForEach(Transports, p => p.OriginServer.Start(StopToken.Token));

            //Listen to connections on all transports async
            IEnumerable<Task> runTasks = Transports.Select(ListenAsync);

            //Set running flag and will be reset when all listening tasks are done
            Running = true;

            //Calling WhenAll() will force the numeration and schedule listening tasks
            return Task.WhenAll(runTasks)
                .ContinueWith(
                    OnAllStopped, 
                    CancellationToken.None, 
                    TaskContinuationOptions.RunContinuationsAsynchronously, 
                    TaskScheduler.Default
                );

            //Defer listening tasks to the task scheduler to avoid blocking this thread
            Task ListenAsync(ListenerState tp) => Task.Run(() => ListenWorkerDoWork(tp), cancellationToken);

            void OnAllStopped(Task _)  => Running = false;
        }

        /*
         * A worker task that listens for connections from the transport
         */
        private async Task ListenWorkerDoWork(ListenerState state)
        {
            state.Running = true;
            
            _config.ServerLog.Information("HTTP server {hc} listening for connections", GetHashCode());

            //Listen for connections until canceled
            while (true)
            {
                try
                {
                    //Listen for new connection 
                    ITransportContext ctx = await state.OriginServer.AcceptAsync(StopToken!.Token);

                    //Try to dispatch the received event
                    _ = DataReceivedAsync(state, ctx).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //Closing, exit loop
                    break;
                }
                catch (Exception ex)
                {
                    _config.ServerLog.Error(ex);
                }
            }

            //Clear all caches before leaving to aid gc
            CacheHardClear();

            state.Running = false;
           
            _config.ServerLog.Information("HTTP server {hc} exiting", GetHashCode());
        }
       

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public void CacheClear() => ContextStore.CacheClear();

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public void CacheHardClear() => ContextStore.CacheHardClear();

        /// <summary>
        /// Writes the specialized log for a socket exception
        /// </summary>
        /// <param name="se">The socket exception to log</param>
        public void WriteSocketExecption(SocketException se)
        {
            //When clause guards nulls
            switch (se.SocketErrorCode)
            {
                //Ignore aborted messages
                case SocketError.ConnectionAborted:
                    break;
                case SocketError.ConnectionReset:
                    _config.ServerLog.Debug("Connecion reset by client");
                    break;
                case SocketError.TimedOut:
                    _config.ServerLog.Debug("Socket operation timed out");
                    break;
                default:
                    _config.ServerLog.Information(se);
                    break;
            }
        }

        private sealed class ListenerState
        {
            /*
             * Indexers ensure correct access during debug builds, but fields
             * can be used directly for tiny performance boost in release builds
             */

            public bool Running;

#if DEBUG

            public required ITransportProvider OriginServer { get; init; }

            public required FrozenDictionary<string, IWebRoot> Roots { get; init; }

            public required IWebRoot? DefaultRoute { get; init; }

#else
            public required ITransportProvider OriginServer;
            public required FrozenDictionary<string, IWebRoot> Roots;
            public required IWebRoot? DefaultRoute;
#endif

        }

    }
}