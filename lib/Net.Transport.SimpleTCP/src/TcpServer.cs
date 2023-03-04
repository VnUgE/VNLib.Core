/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: TcpServer.cs 
*
* TcpServer.cs is part of VNLib.Net.Transport.SimpleTCP which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.SimpleTCP is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.SimpleTCP is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Security;
using System.Threading;
using System.Net.Sockets;
using System.Net.Security;
using System.IO.Pipelines;
using System.Threading.Tasks;
using System.Security.Authentication;
using System.Runtime.CompilerServices;

using VNLib.Utils.Async;
using VNLib.Utils.Logging;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Transport.Tcp
{
    /// <summary>
    /// <para>
    /// Provides a simple, high performance, single process, low/no allocation,
    /// asynchronous, TCP socket server. 
    /// </para>
    /// <para>
    /// IO operations are full duplex so pipe-lining reused 
    /// connections is expected. This class cannot be inherited
    /// </para>
    /// </summary>
    public sealed class TcpServer : ICacheHolder
    {
        /// <summary>
        /// The current <see cref="TcpServer"/> configuration
        /// </summary>
        public TCPConfig Config { get; }

        private readonly ObjectRental<VnSocketAsyncArgs> SockAsyncArgPool;
        private readonly PipeOptions PipeOptions;
        private readonly bool _usingTls;

        /// <summary>
        /// Initializes a new <see cref="TcpServer"/> with the specified <see cref="TCPConfig"/>
        /// </summary>
        /// <param name="config">Configuration to inalize with</param>
        /// <param name="pipeOptions">Optional <see cref="PipeOptions"/> otherwise uses default</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public TcpServer(TCPConfig config, PipeOptions? pipeOptions = null)
        {
            Config = config;
            //Check config
            _ = config.BufferPool ?? throw new ArgumentException("Buffer pool argument cannot be null");
            _ = config.Log ?? throw new ArgumentException("Log argument is required");
            
            if (config.MaxRecvBufferData < 4096)
            {
                throw new ArgumentException("MaxRecvBufferData size must be at least 4096 bytes to avoid data pipeline pefromance issues");
            }
            if(config.AcceptThreads < 1)
            {
                throw new ArgumentException("Accept thread count must be greater than 0");
            }
            if(config.AcceptThreads > Environment.ProcessorCount)
            {
                config.Log.Debug("Suggestion: Setting accept threads to {pc}", Environment.ProcessorCount);
            }
            //Cache pipe options
            PipeOptions = pipeOptions ?? new(
                config.BufferPool,
                readerScheduler:PipeScheduler.ThreadPool, 
                writerScheduler:PipeScheduler.ThreadPool, 
                pauseWriterThreshold: config.MaxRecvBufferData, 
                minimumSegmentSize: 8192,
                useSynchronizationContext:false
                );
            //store tls value
            _usingTls = Config.AuthenticationOptions != null;

            SockAsyncArgPool = ObjectRental.CreateReusable(ArgsConstructor, Config.CacheQuota);
        }

        ///<inheritdoc/>
        public void CacheClear() => SockAsyncArgPool.CacheClear();
        ///<inheritdoc/>
        public void CacheHardClear() => SockAsyncArgPool.CacheHardClear();
      
        private AsyncQueue<VnSocketAsyncArgs>? WaitingSockets;
        private Socket? ServerSock;
        private bool _canceledFlag;

        /// <summary>
        /// Begins listening for incoming TCP connections on the configured socket
        /// </summary>
        /// <param name="token">A token that is used to abort listening operations and close the socket</param>
        /// <exception cref="SocketException"></exception>
        /// <exception cref="SecurityException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void Start(CancellationToken token)
        {
            //If the socket is still listening
            if (ServerSock != null)
            {
                throw new InvalidOperationException("The server thread is currently listening and cannot be re-started");
            }

            //make sure the token isnt already canceled
            if (token.IsCancellationRequested)
            {
                throw new ArgumentException("Token is already canceled", nameof(token));
            }
            
            //Configure socket on the current thread so exceptions will be raised to the caller
            ServerSock = new(Config.LocalEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //Bind socket
            ServerSock.Bind(Config.LocalEndPoint);
            //Begin listening
            ServerSock.Listen(Config.BackLog);
            
            //See if keepalive should be used
            if (Config.TcpKeepalive)
            {               
                //Setup socket keepalive from config
                ServerSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                ServerSock.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, Config.KeepaliveInterval);
                ServerSock.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, Config.TcpKeepAliveTime);
            }

            //Invoke socket created callback
            Config.OnSocketCreated?.Invoke(ServerSock);

            //Init waiting socket queue
            WaitingSockets = new(false, true);

            //Clear canceled flag
            _canceledFlag = false;

            //Start listening for connections
            for (int i = 0; i < Config.AcceptThreads; i++)
            {
                AcceptConnection();
            }

            //Cleanup callback
            static void cleanup(object? state)
            {
                TcpServer server = (TcpServer)state!;

                //Set canceled flag
                server._canceledFlag = true;
                
                //Clean up socket
                server.ServerSock!.Dispose();
                server.ServerSock = null;

                server.SockAsyncArgPool.CacheHardClear();

                //Dispose any queued sockets
                while (server.WaitingSockets!.TryDequeue(out VnSocketAsyncArgs? args))
                {
                    args.Dispose();
                }
            }
            
            //Register cleanup
            _ = token.Register(cleanup, this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReturnCb(VnSocketAsyncArgs args)
        {
            //If the server has exited, dispose the args and dont return to pool
            if (_canceledFlag)
            {
                args.Dispose();
            }
            else
            {
                SockAsyncArgPool.Return(args);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VnSocketAsyncArgs ArgsConstructor()
        {
            //Socket args accept callback functions for this 
            VnSocketAsyncArgs args = new(AcceptCompleted, ReturnCb, PipeOptions);
            return args;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AcceptConnection()
        {
            //Make sure cancellation isnt pending
            if (_canceledFlag)
            {
                return;
            }

            //Rent new args
            VnSocketAsyncArgs acceptArgs = SockAsyncArgPool!.Rent();

            //Accept another socket
            if (!acceptArgs.BeginAccept(ServerSock!))
            {
                //Completed synchronously
                AcceptCompleted(acceptArgs);
            }
            //Completed async
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AcceptCompleted(VnSocketAsyncArgs args)
        {
            //Examine last op for aborted error, if aborted, then the listening socket has exited
            if (args.SocketError == SocketError.OperationAborted)
            {
                //Dispose args since server is exiting
                args.Dispose();
                return;
            }
            //Check for error on accept, and if no error, enqueue the socket, otherwise disconnect the socket
            if (!args.EndAccept() || !WaitingSockets!.TryEnque(args))
            {
                //Disconnect the socket (will return the args to the pool)
                args.Disconnect();
            }
            //Accept a new connection
            AcceptConnection();
        }

        
        /// <summary>
        /// Retreives a connected socket from the waiting queue
        /// </summary>
        /// <returns>The context of the connect</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async ValueTask<TransportEventContext> AcceptAsync(CancellationToken cancellation)
        {
            _ = WaitingSockets ?? throw new InvalidOperationException("Server is not listening");
            //Args is ready to use
            VnSocketAsyncArgs args = await WaitingSockets.DequeueAsync(cancellation);
            //See if tls is enabled, if so, start tls handshake
            if (_usingTls)
            {
                //Begin authenication and make sure the socket stream is closed as its required to cleanup
                SslStream stream = new(args.Stream, false);
                try
                {
                    //auth the new connection
                    await stream.AuthenticateAsServerAsync(Config.AuthenticationOptions!, cancellation);
                    return new(args, stream);
                }
                catch(Exception ex)
                {
                    await stream.DisposeAsync();

                    //Disconnect socket 
                    args.Disconnect();

                    throw new AuthenticationException("Failed client/server TLS authentication", ex);
                }
            }
            else
            {
                return new(args, args.Stream);
            }
        }
    }
}