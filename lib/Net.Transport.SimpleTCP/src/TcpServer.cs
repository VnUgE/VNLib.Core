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
using System.Security;
using System.Net.Sockets;

using System.IO.Pipelines;

using VNLib.Utils.Logging;

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
    public sealed class TcpServer
    {
        private readonly TCPConfig _config;
        private readonly PipeOptions _pipeOptions;

        /// <summary>
        /// The current <see cref="TcpServer"/> configuration
        /// </summary>
        public ref readonly TCPConfig Config => ref _config;

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
                ArgumentNullException.ThrowIfNull(config.BufferPool);
            }

            ArgumentNullException.ThrowIfNull(config.Log, nameof(config.Log));

            ArgumentOutOfRangeException.ThrowIfLessThan(config.MaxRecvBufferData, 4096);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.AcceptThreads, 1u);

            if (config.AcceptThreads > Environment.ProcessorCount)
            {
                config.Log.Debug("Suggestion: Setting accept threads to {pc}", Environment.ProcessorCount);
            }

            _config = config;

            //Assign default pipe options
            pipeOptions ??= new(
                pool: config.BufferPool,
                readerScheduler: PipeScheduler.ThreadPool,
                writerScheduler: PipeScheduler.ThreadPool,
                pauseWriterThreshold: config.MaxRecvBufferData,
                minimumSegmentSize: 8192,
                useSynchronizationContext: false
            );

            _pipeOptions = pipeOptions;
        }

        /// <summary>
        /// Begins listening for incoming TCP connections on the configured socket
        /// </summary>
        /// <returns>
        /// A new immutable <see cref="ITcpListner"/>
        /// </returns>
        /// <exception cref="SocketException"></exception>
        /// <exception cref="SecurityException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public ITcpListner Listen()
        {
            Socket serverSock;
           
            //Configure socket on the current thread so exceptions will be raised to the caller
            serverSock = new(_config.LocalEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            serverSock.Bind(_config.LocalEndPoint);
         
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
            
            TcpListenerNode listener = new(in Config, serverSock, _pipeOptions);

            listener.StartWorkers();

            return listener;
        }
    }
}
