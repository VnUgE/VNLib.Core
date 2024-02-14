/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Net;
using System.Security;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using System.IO.Pipelines;

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
        private readonly TCPConfig _config;

        /// <summary>
        /// The current <see cref="TcpServer"/> configuration
        /// </summary>
        public ref readonly TCPConfig Config => ref _config;

        private readonly ObjectRental<AwaitableAsyncServerSocket> SockAsyncArgPool;
        private readonly AsyncQueue<ITcpConnectionDescriptor> WaitingSockets;

        /// <summary>
        /// Initializes a new <see cref="TcpServer"/> with the specified <see cref="TCPConfig"/>
        /// </summary>
        /// <param name="config">Configuration to inalize with</param>
        /// <param name="pipeOptions">Optional <see cref="PipeOptions"/> otherwise uses default</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public TcpServer(TCPConfig config, PipeOptions? pipeOptions = null)
        {
            //Check config
            if (pipeOptions == null)
            {
                //Pool is required when using default pipe options
                _ = config.BufferPool ?? throw new ArgumentException("Buffer pool argument cannot be null");
            }

            _ = config.Log ?? throw new ArgumentException("Log argument is required");

            if (config.MaxRecvBufferData < 4096)
            {
                throw new ArgumentException("MaxRecvBufferData size must be at least 4096 bytes to avoid data pipeline pefromance issues");
            }
            if (config.AcceptThreads < 1)
            {
                throw new ArgumentException("Accept thread count must be greater than 0");
            }
            if (config.AcceptThreads > Environment.ProcessorCount)
            {
                config.Log.Debug("Suggestion: Setting accept threads to {pc}", Environment.ProcessorCount);
            }

            _config = config;

            //Assign default pipe options
            pipeOptions ??= new(
                config.BufferPool,
                readerScheduler: PipeScheduler.ThreadPool,
                writerScheduler: PipeScheduler.ThreadPool,
                pauseWriterThreshold: config.MaxRecvBufferData,
                minimumSegmentSize: 8192,
                useSynchronizationContext: false
            );

            //Arguments constructor
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            AwaitableAsyncServerSocket ArgsConstructor() => new(pipeOptions);

            SockAsyncArgPool = ObjectRental.CreateReusable(ArgsConstructor, config.CacheQuota);

            //Init waiting socket queue, always multi-threaded
            WaitingSockets = new(false, false);
        }

        ///<inheritdoc/>
        public void CacheClear() => SockAsyncArgPool.CacheClear();

        ///<inheritdoc/>
        public void CacheHardClear() => SockAsyncArgPool.CacheHardClear();

        /// <summary>
        /// Begins listening for incoming TCP connections on the configured socket
        /// </summary>
        /// <param name="token">A token that is used to abort listening operations and close the socket</param>
        /// <returns>A task that resolves when all accept threads have exited. The task does not need to be observed</returns>
        /// <exception cref="SocketException"></exception>
        /// <exception cref="SecurityException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public Task Start(CancellationToken token)
        {
            Socket serverSock;

            //make sure the token isnt already canceled
            if (token.IsCancellationRequested)
            {
                throw new ArgumentException("Token is already canceled", nameof(token));
            }

            //Configure socket on the current thread so exceptions will be raised to the caller
            serverSock = new(_config.LocalEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //Bind socket
            serverSock.Bind(_config.LocalEndPoint);
            //Begin listening
            serverSock.Listen(_config.BackLog);
            
            //See if keepalive should be used
            if (_config.TcpKeepalive)
            {
                //Setup socket keepalive from config
                serverSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                serverSock.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _config.KeepaliveInterval);
                serverSock.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _config.TcpKeepAliveTime);
            }

            //Invoke socket created callback
            _config.OnSocketCreated?.Invoke(serverSock);

            //Clear canceled flag
            StrongBox<bool> canceledFlag = new(false);

            Task[] acceptWorkers = new Task[_config.AcceptThreads];

            //Start listening for connections
            for (int i = 0; i < _config.AcceptThreads; i++)
            {
                acceptWorkers[i] = Task.Run(() => ExecAcceptAsync(serverSock, canceledFlag), token);
            }

            CancellationTokenRegistration reg = default;

            //Cleanup callback
            void cleanup()
            {
                //Set canceled flag
                canceledFlag.Value = true;

                //Clean up socket
                serverSock.Dispose();

                //Cleanup pool
                SockAsyncArgPool.CacheHardClear();

                //Dispose any queued sockets
                while (WaitingSockets!.TryDequeue(out ITcpConnectionDescriptor? args))
                {
                    (args as IDisposable)!.Dispose();
                }

                reg.Dispose();
            }

            //Register cleanup
            reg = token.Register(cleanup, false);

            return Task.WhenAll(acceptWorkers);
        }

        private async Task ExecAcceptAsync(Socket serverSock, StrongBox<bool> canceled)
        {
            Debug.Assert(serverSock != null, "Expected not-null server socket value");
            Debug.Assert(canceled != null && !canceled.Value, "Expected a valid canceled flag instance");

            //Cache buffer sizes
            int recBufferSize = serverSock.ReceiveBufferSize;
            int sendBufferSize = serverSock.SendBufferSize;

            //Cache local endpoint for multi-server logging
            EndPoint localEndpoint = serverSock.LocalEndPoint!;

            Debug.Assert(localEndpoint != null, "Expected a socket bound to a local endpoint");

            try
            {
                while (!canceled.Value)
                {
                    //Rent new args
                    AwaitableAsyncServerSocket acceptArgs = SockAsyncArgPool.Rent();

                    //Accept new connection
                    SocketError err = await acceptArgs.AcceptAsync(serverSock, recBufferSize, sendBufferSize);

                    //Check canceled flag before proceeding
                    if (canceled.Value)
                    {
                        //dispose and bail
                        acceptArgs.Dispose();
                        _config.Log.Verbose("Accept thread aborted for {socket}", localEndpoint);
                    }
                    else if (err == SocketError.Success)
                    {
                        // Add to waiting queue
                        if (!WaitingSockets!.TryEnque(acceptArgs))
                        {
                            _ = await acceptArgs.CloseConnectionAsync();

                            /*
                             * Writing to log will likely compound resource exhaustion, but the user must be informed
                             * connections are being dropped.
                             */
                            _config.Log.Warn("Socket {e} disconnected because the waiting queue is overflowing", acceptArgs.GetHashCode());

                            //Re-eqnue
                            SockAsyncArgPool.Return(acceptArgs);
                        }

                        //Success
                        PrintConnectionInfo(acceptArgs, SocketAsyncOperation.Accept);
                    }
                    else
                    {
                        //Error
                        _config.Log.Debug("Socket accept failed with error code {ec}", err);
                        //Return args to pool
                        SockAsyncArgPool.Return(acceptArgs);
                    }
                }
            }
            catch(Exception ex)
            {
                _config.Log.Fatal(ex, "Accept thread failed with exception");
            }
        }


        /// <summary>
        /// Accepts a connection and returns the connection descriptor.
        /// </summary>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The connection descriptor</returns>
        /// <remarks>
        /// NOTE: You must always call the <see cref="CloseConnectionAsync"/> and 
        /// destroy all references to it when you are done. You must also dispose the stream returned
        /// from the <see cref="ITcpConnectionDescriptor.GetStream"/> method.
        /// </remarks>
        /// <exception cref="InvalidOperationException"></exception>
        public ValueTask<ITcpConnectionDescriptor> AcceptConnectionAsync(CancellationToken cancellation)
        {
            //Try get args from queue
            if (WaitingSockets.TryDequeue(out ITcpConnectionDescriptor? args))
            {
                return ValueTask.FromResult(args);
            }

            return WaitingSockets!.DequeueAsync(cancellation);
        }

        /// <summary>
        /// Cleanly closes an existing TCP connection obtained from <see cref="AcceptConnectionAsync(CancellationToken)"/>
        /// and returns the instance to the pool for reuse. 
        /// <para>
        /// If you set <paramref name="reuse"/> to true, the server will attempt to reuse the descriptor instance, you 
        /// must ensure that all previous references to the descriptor are destroyed. If the value is false, resources 
        /// are freed and the instance is disposed.
        /// </para>
        /// </summary>
        /// <param name="descriptor">The existing descriptor to close</param>
        /// <param name="reuse">A value that indicates if the server can safley reuse the descriptor instance</param>
        /// <returns>A task that represents the closing operations</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async ValueTask CloseConnectionAsync(ITcpConnectionDescriptor descriptor, bool reuse)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            //Recover args
            AwaitableAsyncServerSocket args = (AwaitableAsyncServerSocket)descriptor;

            PrintConnectionInfo(args, SocketAsyncOperation.Disconnect);

            //Close the socket and cleanup resources
            SocketError err = await args.CloseConnectionAsync();

            if (err != SocketError.Success)
            {
                _config.Log.Verbose("Socket disconnect failed with error code {ec}.", err);
            }

            //See if we can reuse the args
            if (reuse)
            {
                //Return to pool
                SockAsyncArgPool.Return(args);
            }
            else
            {
                //Dispose
                args.Dispose();
            }
        }


        [Conditional("DEBUG")]
        private void PrintConnectionInfo(ITcpConnectionDescriptor con, SocketAsyncOperation operation)
        {
            if (!_config.DebugTcpLog)
            {
                return;
            }

            con.GetEndpoints(out IPEndPoint local, out IPEndPoint remote);

            switch (operation)
            {
                default:
                    _config.Log.Verbose("Socket {operation} on {local} -> {remote}", operation, local, remote);
                    break;
            }
        }
    }
}