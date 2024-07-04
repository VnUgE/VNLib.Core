/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: TcpListenerNode.cs 
*
* TcpListenerNode.cs is part of VNLib.Net.Transport.SimpleTCP which is part 
* of the larger VNLib collection of libraries and utilities.
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
    internal sealed class TcpListenerNode : ITcpListner
    {
        public readonly TCPConfig Config;
        public readonly Socket ServerSocket;
        public readonly ObjectRental<AwaitableAsyncServerSocket> SockAsyncArgPool;
        public readonly AsyncQueue<ITcpConnectionDescriptor> WaitingSockets;       

        private readonly int _recvBufferSize;
        private readonly int _sendBufferSize;

        public bool IsCancelled;
        private Task _onExitTask;

        public TcpListenerNode(in TCPConfig config, Socket serverSocket, PipeOptions pipeOptions)
        {
            Config = config;
            ServerSocket = serverSocket;

            //Cache socket buffer sizes to avoid system calls
            _recvBufferSize = ServerSocket.ReceiveBufferSize;
            _sendBufferSize = ServerSocket.SendBufferSize;

            //Arguments constructor
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            AwaitableAsyncServerSocket ArgsConstructor() => new(pipeOptions);

            SockAsyncArgPool = ObjectRental.CreateReusable(ArgsConstructor, config.CacheQuota);

            //Init waiting socket queue, always multi-threaded
            WaitingSockets = new(singleWriter: false, singleReader: false);

            _onExitTask = Task.CompletedTask;
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
            IsCancelled = true;

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

       
        public ValueTask<ITcpConnectionDescriptor> AcceptConnectionAsync(CancellationToken cancellation)
        {
            //Try get args from queue
            if (WaitingSockets.TryDequeue(out ITcpConnectionDescriptor? args))
            {
                return ValueTask.FromResult(args);
            }

            return WaitingSockets!.DequeueAsync(cancellation);
        }

      
        public async ValueTask CloseConnectionAsync(ITcpConnectionDescriptor descriptor, bool reuse)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            //Recover args
            AwaitableAsyncServerSocket args = (AwaitableAsyncServerSocket)descriptor;

            PrintConnectionInfo(args, SocketAsyncOperation.Disconnect);

            /*
             * Technically a user can mess this counter up by continually calling 
             * this function even if the connection is already closed.
             */
            OnClientDisconnected();

            //Close the socket and cleanup resources
            SocketError err = await args.CloseConnectionAsync();

            if (err != SocketError.Success)
            {
                Config.Log.Verbose("Socket disconnect failed with error code {ec}.", err);
            }

            //Can only reuse if the server is still listening
            reuse &= !IsCancelled;

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

        internal async Task ExecAcceptAsync()
        {
            Debug.Assert(!IsCancelled, "Expected a valid canceled flag instance");

            OnAcceptThreadStart();

            try
            {
                do
                {
                    //Rent new args
                    AwaitableAsyncServerSocket acceptArgs = SockAsyncArgPool.Rent();

                    //Accept new connection
                    SocketError err = await acceptArgs.AcceptAsync(ServerSocket, _recvBufferSize, _sendBufferSize);

                    //Check canceled flag before proceeding
                    if (IsCancelled)
                    {
                        //dispose and bail
                        acceptArgs.Dispose();
                        Config.Log.Verbose("Accept thread aborted for {socket}", Config.LocalEndPoint);
                    }
                    else if (err == SocketError.Success)
                    {
                        bool maxConsReached = _connectedClients > Config.MaxConnections;

                        //Add to waiting queue
                        if (maxConsReached || !WaitingSockets!.TryEnque(acceptArgs))
                        {
                            /*
                             * If max connections are reached or the queue is overflowing, 
                             * connections must be dropped
                             */

                            _ = await acceptArgs.CloseConnectionAsync();

                            /*
                             * Writing to log will likely compound resource exhaustion, but the user must be informed
                             * connections are being dropped.
                             */
                            Config.Log.Warn("Socket {e} disconnected because the waiting queue is overflowing", acceptArgs.GetHashCode());

                            //Re-eqnue
                            SockAsyncArgPool.Return(acceptArgs);
                        }
                        else
                        {
                            //Success
                            PrintConnectionInfo(acceptArgs, SocketAsyncOperation.Accept);

                            OnClientConnected();
                        }
                    }
                    else
                    {
                        //Error
                        Config.Log.Debug("Socket accept failed with error code {ec}", err);
                        //Safe to return args to the pool as long as the server is listening
                        SockAsyncArgPool.Return(acceptArgs);
                    }
                } while (!IsCancelled);
            }
            catch (Exception ex)
            {
                Config.Log.Fatal(ex, "Accept thread failed with exception");
            }
            finally
            {
                OnAcceptThreadExit();
            }
        }

        [Conditional("DEBUG")]
        internal void PrintConnectionInfo(ITcpConnectionDescriptor con, SocketAsyncOperation operation)
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


        /*
         * A reference counter for tracking 
         * accept threads
         */
        private uint _acceptThreadsActive;
        private long _connectedClients;

        private void OnClientConnected() => Interlocked.Increment(ref _connectedClients);

        private void OnClientDisconnected() => Interlocked.Decrement(ref _connectedClients);

        private void OnAcceptThreadStart() => Interlocked.Increment(ref _acceptThreadsActive);

        private void OnAcceptThreadExit()
        {
            /*
             * Track active threads. When the last thread exists
             * we can cleanup internal state
             */

            if (Interlocked.Decrement(ref _acceptThreadsActive) == 0)
            {
                Cleanup();
            }
        }

      
        private void Cleanup()
        {
            SockAsyncArgPool.Dispose();

            //Dispose any queued client sockets that need to exit
            while (WaitingSockets!.TryDequeue(out ITcpConnectionDescriptor? args))
            {
                (args as IDisposable)!.Dispose();
            }
        }
    }
}