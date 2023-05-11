/*
* Copyright (c) 2023 Vaughn Nugent
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

using System;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Authentication;

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
     
        private readonly ITransportProvider Transport;
        private readonly IReadOnlyDictionary<string, IWebRoot> ServerRoots;

        #region caches
        /// <summary>
        /// The cached HTTP1/1 keepalive timeout header value
        /// </summary>
        private readonly string KeepAliveTimeoutHeaderValue;
        /// <summary>
        /// Reusable store for obtaining <see cref="HttpContext"/> 
        /// </summary>
        private readonly ObjectRental<HttpContext> ContextStore;
        /// <summary>
        /// The cached header-line termination value
        /// </summary>
        private readonly ReadOnlyMemory<byte> HeaderLineTermination;
        #endregion

        /// <summary>
        /// The <see cref="HttpConfig"/> for the current server
        /// </summary>
        public HttpConfig Config { get; }

        /// <summary>
        /// Gets a value indicating whether the server is listening for connections
        /// </summary>
        public bool Running { get; private set; }

        private CancellationTokenSource? StopToken;

        /// <summary>
        /// Creates a new <see cref="HttpServer"/> with the specified configration copy (using struct).
        /// Immutable data structures are initialzed.
        /// </summary>
        /// <param name="config">The configuration used to create the instance</param>
        /// <param name="transport">The transport provider to listen to connections from</param>
        /// <param name="sites">A collection of <see cref="IWebRoot"/>s that route incomming connetctions</param>
        /// <exception cref="ArgumentException"></exception>
        public HttpServer(HttpConfig config, ITransportProvider transport, IEnumerable<IWebRoot> sites)
        {
            //Validate the configuration
            ValidateConfig(in config);

            Config = config;
            //Configure roots and their directories
            ServerRoots = sites.ToDictionary(static r => r.Hostname, static tv => tv, StringComparer.OrdinalIgnoreCase);
            //Compile and store the timeout keepalive header
            KeepAliveTimeoutHeaderValue = $"timeout={(int)Config.ConnectionKeepAlive.TotalSeconds}";
            //Store termination for the current instance
            HeaderLineTermination = config.HttpEncoding.GetBytes(HttpHelpers.CRLF);
            //Create a new context store
            ContextStore = ObjectRental.CreateReusable(() => new HttpContext(this));
            //Setup config copy with the internal http pool
            Transport = transport;
        }

        private static void ValidateConfig(in HttpConfig conf)
        {
            _ = conf.HttpEncoding ?? throw new ArgumentException("HttpEncoding cannot be null", nameof(conf));
            _ = conf.ServerLog ?? throw new ArgumentException("ServerLog cannot be null", nameof(conf));
            _ = conf.MemoryPool ?? throw new ArgumentNullException(nameof(conf));

            if (conf.ActiveConnectionRecvTimeout < -1)
            {
                throw new ArgumentException("ActiveConnectionRecvTimeout cannot be less than -1", nameof(conf));
            }

            //Chunked data accumulator must be at least 64 bytes (arbinrary value)
            if (conf.BufferConfig.ChunkedResponseAccumulatorSize < 64 || conf.BufferConfig.ChunkedResponseAccumulatorSize == int.MaxValue)
            {
                throw new ArgumentException("ChunkedResponseAccumulatorSize cannot be less than 64 bytes", nameof(conf));
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

            if (conf.BufferConfig.DiscardBufferSize < 64)
            {
                throw new ArgumentException("DiscardBufferSize cannot be less than 64 bytes", nameof(conf));
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
            //Start servers with the new token source
            Transport.Start(StopToken.Token);
            //Start the listen task
            return Task.Run(ListenWorkerDoWork, cancellationToken);
        }

        /*
        * An SslStream may throw a win32 exception with HRESULT 0x80090327
        * when processing a client certificate (I believe anyway) only 
        * an issue on some clients (browsers)
        */

        private const int UKNOWN_CERT_AUTH_HRESULT = unchecked((int)0x80090327);

        /// <summary>
        /// An invlaid frame size may happen if data is recieved on an open socket
        /// but does not contain valid SSL handshake data
        /// </summary>
        private const int INVALID_FRAME_HRESULT = unchecked((int)0x80131620);

        /*
         * A worker task that listens for connections from the transport
         */
        private async Task ListenWorkerDoWork()
        {
            //Set running flag
            Running = true;
            
            Config.ServerLog.Information("HTTP server {hc} listening for connections", GetHashCode());

            //Listen for connections until canceled
            while (true)
            {
                try
                {
                    //Listen for new connection 
                    ITransportContext ctx = await Transport.AcceptAsync(StopToken!.Token);

                    //Try to dispatch the recieved event
                    _ = DataReceivedAsync(ctx).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //Closing, exit loop
                    break;
                }
                catch (AuthenticationException ae)
                {
                    Config.ServerLog.Error(ae);
                }
                catch (Exception ex)
                {
                    Config.ServerLog.Error(ex);
                }
            }

            //Clear all caches
            CacheHardClear();

            //Clear running flag
            Running = false;
            Config.ServerLog.Information("HTTP server {hc} exiting", GetHashCode());
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
                    return;
                case SocketError.ConnectionReset:
                    Config.ServerLog.Debug("Connecion reset by client");
                    return;
                case SocketError.TimedOut:
                    Config.ServerLog.Debug("Socket operation timed out");
                    return;
                default:
                    Config.ServerLog.Information(se);
                    break;
            }
        }
    }
}