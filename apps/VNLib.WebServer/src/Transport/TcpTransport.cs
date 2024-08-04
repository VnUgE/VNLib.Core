/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: TcpTransport.cs 
*
* TcpTransport.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Threading;
using System.IO.Pipelines;
using System.Net.Security;
using System.Threading.Tasks;
using System.Security.Authentication;

using VNLib.Net.Http;
using VNLib.Net.Transport.Tcp;
using VNLib.Utils.Logging;

namespace VNLib.WebServer.Transport
{
    /// <summary>
    /// Creates the TCP/HTTP translation layer providers
    /// </summary>
    internal static class TcpTransport
    {
        /// <summary>
        /// Creates a new <see cref="ITransportProvider"/> that will listen for tcp connections
        /// </summary>
        /// <param name="config">The server configuration</param>
        /// <param name="inlineScheduler">Use the inline pipeline scheduler</param>
        /// <returns>The configured <see cref="ITransportProvider"/></returns>
        public static ITransportProvider CreateServer(ref readonly TCPConfig config, bool inlineScheduler)
        {
            //Create tcp server
            TcpServer server = new (config, CreateCustomPipeOptions(in config, inlineScheduler));
            //Return provider
            return new TcpTransportProvider(server);
        }

        /// <summary>
        /// Creates a new <see cref="ITransportProvider"/> that will listen for tcp connections
        /// and use SSL
        /// </summary>
        /// <param name="config"></param>
        /// <param name="ssl">The server authentication options</param>
        /// <returns>The ssl configured transport context</returns>
        public static ITransportProvider CreateServer(in TCPConfig config, SslServerAuthenticationOptions ssl)
        {
            /*
             * SSL STREAM WORKAROUND
             * 
             * The HttpServer impl calls Read() synchronously on the calling thread, 
             * it assumes that the call will make it synchronously to the underlying 
             * transport. SslStream calls ReadAsync() interally on the current 
             * synchronization context, which causes a deadlock... So the threadpool 
             * scheduler on the pipeline ensures that all continuations are run on the
             * threadpool, which fixes this issue.
             */

            //Create tcp server 
            TcpServer server = new (config, CreateCustomPipeOptions(in config, false));
            //Return provider
            return new SslTcpTransportProvider(server, ssl);
        }

        private static PipeOptions CreateCustomPipeOptions(ref readonly TCPConfig config, bool inlineScheduler)
        {
            return new PipeOptions(
                config.BufferPool,
                //Noticable performance increase when using inline scheduler for reader (handles send operations)
                readerScheduler: inlineScheduler ? PipeScheduler.Inline : PipeScheduler.ThreadPool,
                writerScheduler: inlineScheduler ? PipeScheduler.Inline : PipeScheduler.ThreadPool,
                pauseWriterThreshold: config.MaxRecvBufferData,
                minimumSegmentSize: 8192,
                useSynchronizationContext: false
            );
        }

        /// <summary>
        /// A TCP server transport provider class
        /// </summary>
        private class TcpTransportProvider(TcpServer Server) : ITransportProvider
        {
            protected ITcpListner? _listener;
            protected CancellationTokenRegistration _reg;

            ///<inheritdoc/>
            void ITransportProvider.Start(CancellationToken stopToken)
            {
                //TEMPORARY (unless it works)
                if(_listener is not null)
                {
                    throw new InvalidOperationException("The server has already been started.");
                }

                //Start the server
                _listener = Server.Listen();
                _reg = stopToken.Register(_listener.Close, false);
            }

            ///<inheritdoc/>
            public virtual async ValueTask<ITransportContext> AcceptAsync(CancellationToken cancellation)
            {
                //Wait for tcp event and wrap in ctx class
                ITcpConnectionDescriptor descriptor = await _listener!.AcceptConnectionAsync(cancellation);
               
                return new TcpTransportContext(_listener, descriptor, descriptor.GetStream());
            }

            ///<inheritdoc/>
            public override string ToString() => $"{Server.Config.LocalEndPoint} tcp/ip";
        }

        private sealed class SslTcpTransportProvider(TcpServer Server, SslServerAuthenticationOptions AuthOptions) 
            : TcpTransportProvider(Server)
        {
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
            private const int INVALID_FRAME_HRESULT = unchecked((int)0x80131501);

            public override async ValueTask<ITransportContext> AcceptAsync(CancellationToken cancellation)
            {
                //Loop to handle ssl exceptions ourself
                do
                {
                    //Wait for tcp event and wrap in ctx class
                    ITcpConnectionDescriptor descriptor = await _listener.AcceptConnectionAsync(cancellation);

                    //Create ssl stream and auth
                    SslStream stream = new(descriptor.GetStream(), false);

                    try
                    {
                        //auth the new connection
                        await stream.AuthenticateAsServerAsync(AuthOptions, cancellation);
                        return new SslTcpTransportContext(_listener, descriptor, stream);
                    }
                    catch (AuthenticationException ae) when (ae.HResult == INVALID_FRAME_HRESULT)
                    {
                        Server.Config.Log.Debug("A TLS connection attempt was made but an invalid TLS frame was received");
                        
                        await _listener.CloseConnectionAsync(descriptor, true);
                        await stream.DisposeAsync();
                        
                        //continue listening loop
                    }
                    catch
                    {
                        await _listener.CloseConnectionAsync(descriptor, true);
                        await stream.DisposeAsync();
                        throw;
                    }
                }
                while (true);
            }

            ///<inheritdoc/>
            public override string ToString() => $"{Server.Config.LocalEndPoint} tcp/ip (TLS enabled)";
        }
    }
}
