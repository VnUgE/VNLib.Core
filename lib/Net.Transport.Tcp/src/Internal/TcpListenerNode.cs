/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: TcpListenerNode.cs 
*
* TcpListenerNode.cs is part of VNLib.Net.Transport.Tcp which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.Tcp is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.Tcp is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using System.IO.Pipelines;

using VNLib.Utils.Async;
using VNLib.Utils.Logging;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Transport.Tcp.Internal
{
    internal sealed class TcpListenerNode : ITcpListener
    {
        public readonly TcpConfig Config;
        public readonly Socket ServerSocket;
        public readonly ObjectRental<SocketIoManager> SockAsyncArgPool;
        public readonly AsyncQueue<SocketIoManager> WaitingSockets;

        // Caches for system socket buffer sizes to avoid syscalls
        private readonly int _recvBufferSize;
        private readonly int _sendBufferSize;
        private readonly bool _isWindows;

        // Tracks when the server is required cancelled.
        private bool _isCancelled;
       
        private Task _onExitTask;

        //A reference counter for tracking accept threads
        private uint _acceptThreadsActive;

        /// <summary>
        /// Gets a value that determines if the server was cancelled.
        /// </summary>
        public bool IsCancelled => _isCancelled;

        /// <summary>
        /// Initializes a new listener around a bound and listening socket. This constructor does not start any accept threads, 
        /// so the caller must call StartWorkers to begin accepting connections.
        /// </summary>
        /// <param name="config">The configuration for the TCP listener.</param>
        /// <param name="serverSocket">The bound and listening socket.</param>
        /// <param name="pipeOptions">The pipe options for the listener.</param>
        public TcpListenerNode(in TcpConfig config, Socket serverSocket, PipeOptions pipeOptions)
        {
            Config = config;
            ServerSocket = serverSocket;

            //Cache socket buffer sizes to avoid system calls
            _recvBufferSize = ServerSocket.ReceiveBufferSize;
            _sendBufferSize = ServerSocket.SendBufferSize;
            _isWindows = OperatingSystem.IsWindows();

            // Ensure the server guards protected from unsupported socket reuse
            Debug.Assert(config.ReuseSocket == false || OperatingSystem.IsWindows(), "Socket reuse is only supported on Windows platforms.");

            //Arguments constructor
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            SocketIoManager ArgsConstructor() => new(Config.ReuseSocket, pipeOptions);

            SockAsyncArgPool = ObjectRental.CreateReusable(ArgsConstructor, config.CacheQuota);

            /*
             *  Prepare the waiting queue for accept sockets. It's maximum value will be used to
             *  apply backpressure when the queue fills faster than workers can process incoming
             *  connections. 
             *  
             *  - Always assume that multiple threads will be dequeuing work.
             *  - If only one accept thread is used, optimize the queue for single writer
             */
            WaitingSockets = new(
                singleWriter: config.AcceptThreads == 1,
                singleReader: false,
                capacity: config.MaxConnections
            );

            _onExitTask = Task.CompletedTask;
        }

        /// <summary>
        /// Cleans up and disposes internal state. This should only be called once 
        /// all accept threads have exited to ensure
        /// </summary>
        private void Cleanup()
        {
            SockAsyncArgPool.Dispose();

            //Dispose any queued client sockets that need to exit
            while (WaitingSockets!.TryDequeue(out SocketIoManager? args))
            {
                args.Dispose();
            }

            Config.Log.Debug("Listener for {socket} destroyed", Config.LocalEndPoint);
        }      

        private void OnAcceptThreadStart()
            => Interlocked.Increment(ref _acceptThreadsActive);

        private void OnAcceptThreadExit()
        {
            // Clean up state once all threads exit
            if (Interlocked.Decrement(ref _acceptThreadsActive) == 0)
            {
                Cleanup();
            }
        }

        private SocketIoManager PrepNewConnection(AwaitableValueSocketEventArgs acceptArgs)
        {
            SocketIoManager? newConnection = SockAsyncArgPool.Rent();

            // Windows is the only platform that supports receive during accept async
            if (_isWindows)
            {
                Memory<byte> prereadBuffer = newConnection.GetPrereadBuffer(_recvBufferSize);
                acceptArgs.SetBuffer(prereadBuffer);
            }

            return newConnection;
        }

        private async Task ExecAcceptAsync()
        {
            Debug.Assert(!_isCancelled, "Expected a valid canceled flag instance");

            OnAcceptThreadStart();
            
            int listenerId = Random.Shared.Next();
          
            AwaitableValueSocketEventArgs acceptArgs = new();

            /*
             * Main accept work loop entrypoint. This function is reentrant and expected
             * to be called by multiple threads during normal operation. All function
             * calls must be thread-safe or synchronized.
             * 
             * In addition, library function calls should NEVER raise exceptions during normal
             * operation. Exceptions will cause an accept thread to exit. 
             * 
             * The accept args are used for every accept operations for this worker task. After a successful
             * accept, the socket is passed off to the new connection descriptor object and the args are returned
             * to a clean state before the next accept.
             */

            try
            {
                do
                {
                    acceptArgs.Prepare();                 

                    SocketIoManager? newConnection = PrepNewConnection(acceptArgs);

                     /*
                    * The new connection may have a valid socket instance if socket object reuse 
                    * is allowed.
                    */
                    acceptArgs.AcceptSocket = newConnection.Socket;

                    //Accept new connection
                    SocketError err = await AwaitableValueSocketEventArgs.AcceptAsync(acceptArgs, ServerSocket)
                                        .ConfigureAwait(false);

                    //Check canceled flag before proceeding
                    if (_isCancelled)
                    {
                        newConnection.Dispose();

                        Config.Log.Verbose("Accept thread {id} aborted for {socket}", listenerId, Config.LocalEndPoint);
                    }
                    else if (err == SocketError.Success)
                    {
                        newConnection.AcceptedSocket(
                            acceptArgs.AcceptSocket!, 
                            acceptArgs.BytesTransferred
                        );                      

                        /*
                         * Always try to enqueue the socket on the queue synchronously.
                         * 
                         * If the queue is full, apply backpressure by waiting on the queue async 
                         * instead of dropping back into an accept. 
                         */
                        if (WaitingSockets!.TryEnqueue(newConnection))
                        {                            
                            PrintConnectionInfo(newConnection, SocketAsyncOperation.Accept);
                        }
                        else
                        {
                            // Apply backpressure by waiting
                            await WaitingSockets.EnqueueAsync(newConnection)
                                .ConfigureAwait(false);
                        }

                        // Connection was enqueued successfully, prepare for next accept
                        newConnection = null; 
                    }
                    else
                    {                       
                        Config.Log.Debug("Accept thread {id}: Socket accept failed with error code {ec}", listenerId, err);

                        //Safe to return args to the pool as long as the server is listening
                        SockAsyncArgPool.Return(newConnection);
                        
                        newConnection = null;
                    }

                    // Cleans up any linger locals/fields
                    acceptArgs.Release();                  

                } while (!_isCancelled);
            }
            catch (Exception ex)
            {
                Config.Log.Fatal("Accept thread {id} failed with exception\n{ex}", listenerId, ex);
            }
            finally
            {
                OnAcceptThreadExit();
                acceptArgs.Dispose();
            }
        }

        [Conditional("DEBUG")]
        private void PrintConnectionInfo(ITcpConnectionDescriptor con, SocketAsyncOperation operation)
        {
            if (!Config.DebugTcpLog)
            {
                return;
            }

            con.GetEndpoints(out IPEndPoint local, out IPEndPoint remote);

            switch (operation)
            {
                default:
                    Config.Log.Verbose("Socket {operation} on {local} -> {remote}", operation, local, remote);
                    break;
            }
        }

        internal void StartWorkers()
        {
            Task[] acceptWorkers = new Task[Config.AcceptThreads];

            //Start listening for connections
            for (int i = 0; i < Config.AcceptThreads; i++)
            {
                acceptWorkers[i] = Task.Run(ExecAcceptAsync);
            }

            _onExitTask = Task.WhenAll(acceptWorkers);
        }

        ///<inheritdoc/>
        public void Close()
        {
            _isCancelled = true;

            /*
             * Disposing the server socket will cause all accept 
             * operations to fail and allow accept threads to exit
             */
            ServerSocket.Dispose();
        }

        ///<inheritdoc/>
        public void CacheClear() => SockAsyncArgPool.CacheClear();

        ///<inheritdoc/>
        public void CacheHardClear() => SockAsyncArgPool.CacheHardClear();

        ///<inheritdoc/>
        public Task WaitForExitAsync() => _onExitTask;

        ///<inheritdoc/>
        public async ValueTask<ITcpConnectionDescriptor> AcceptConnectionAsync(CancellationToken cancellation)
        {
            SocketIoManager desc = await WaitingSockets!.DequeueAsync(cancellation)
                .ConfigureAwait(false);

            // Start the pipeline worker tasks now that the app is ready to use the socket
            desc.StartPipeline(_recvBufferSize, _sendBufferSize);

            return desc;
        }

        ///<inheritdoc/>
        public async ValueTask CloseConnectionAsync(ITcpConnectionDescriptor descriptor, bool reuse)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            //Recover args
            SocketIoManager args = (SocketIoManager)descriptor;

            PrintConnectionInfo(args, SocketAsyncOperation.Disconnect);

            //Close the socket and cleanup resources
            SocketError err = await args.CloseConnectionAsync()
                                .ConfigureAwait(false);

            if (err != SocketError.Success)
            {
                Config.Log.Verbose("Socket disconnect failed with error code {ec}.", err);
            }

            //Can only reuse if the server is still listening
            reuse &= !_isCancelled;

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

    }
}