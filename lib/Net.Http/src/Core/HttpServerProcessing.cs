/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpServerProcessing.cs 
*
* HttpServerProcessing.cs is part of VNLib.Net.Http which is part 
* of the larger VNLib collection of libraries and utilities.
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

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Net.Http.Core;
using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http
{
    public sealed partial class HttpServer
    {

        private int OpenConnectionCount;

        //Event handler method for processing incoming data events
        private async Task DataReceivedAsync(ITransportContext transportContext)
        {
            //Increment open connection count
            Interlocked.Increment(ref OpenConnectionCount);
            
            //Rent a new context object to reuse
            HttpContext? context = ContextStore.Rent();
            
            try
            {
                //Set write timeout
                transportContext.ConnectionStream.WriteTimeout = Config.SendTimeout;

                //Init stream
                context.InitializeContext(transportContext);
                
                //Keep the transport open and listen for messages as long as keepalive is enabled
                do
                {
                    //Set rx timeout low for initial reading
                    transportContext.ConnectionStream.ReadTimeout = Config.ActiveConnectionRecvTimeout;
                    
                    //Process the request
                    bool keepAlive = await ProcessHttpEventAsync(transportContext, context);

                    //If not keepalive, exit the listening loop
                    if (!keepAlive)
                    {
                        break;
                    }

                    //Set inactive keeaplive timeout
                    transportContext.ConnectionStream.ReadTimeout = (int)Config.ConnectionKeepAlive.TotalMilliseconds;
                    
                    //"Peek" or wait for more data to begin another request (may throw timeout exception when timmed out)
                    await transportContext.ConnectionStream.ReadAsync(Memory<byte>.Empty, StopToken!.Token);
                    
                } while (true);

                //Check if an alternate protocol was specified
                if (context.AlternateProtocol != null)
                {
                    //Save the current ap
                    IAlternateProtocol ap = context.AlternateProtocol;

                    //Release the context before listening to free it back to the pool
                    ContextStore.Return(context);
                    context = null;

                    //Remove transport timeouts
                    transportContext.ConnectionStream.ReadTimeout = Timeout.Infinite;
                    transportContext.ConnectionStream.WriteTimeout = Timeout.Infinite;

                    //Listen on the alternate protocol
                    await ap.RunAsync(transportContext.ConnectionStream, StopToken!.Token).ConfigureAwait(false);
                }
            }
            //Catch wrapped socket exceptions
            catch(IOException ioe) when(ioe.InnerException is SocketException se)
            {
                WriteSocketExecption(se);
            }
            catch(SocketException se)
            {
                WriteSocketExecption(se);
            }
            catch(OperationCanceledException oce)
            {
                Config.ServerLog.Debug("Failed to receive transport data within a timeout period {m}, connection closed", oce.Message);
            }
            catch(Exception ex)
            {
                Config.ServerLog.Error(ex);
            }
            
            //Dec open connection count
            Interlocked.Decrement(ref OpenConnectionCount);
            
            //Return the context for normal operation
            if(context != null)
            {
                //Return context to store
                ContextStore.Return(context);
            }
            
            //Close the transport async
            try
            {
                await transportContext.CloseConnectionAsync();
            }
            catch(Exception ex)
            {
                Config.ServerLog.Error(ex);
            }
        }


        /// <summary>
        /// Main event handler for all incoming connections
        /// </summary>
        /// <param name="transportContext">The <see cref="ITransportContext"/> describing the incoming connection</param>
        /// <param name="context">Reusable context object</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private async Task<bool> ProcessHttpEventAsync(ITransportContext transportContext, HttpContext context)
        {
            //Prepare http context to process a new message
            context.BeginRequest();
            
            try
            {
                //Try to parse the http request (may throw exceptions, let them propagate to the transport layer)
                int status = (int)ParseRequest(transportContext, context);

                //Check status code for socket error, if so, return false to close the connection
                if (status >= 1000)
                {
                    return false;
                }

                //process the request
                bool keepalive = await ProcessRequestAsync(context, (HttpStatusCode)status);

#if DEBUG
                //Write debug response log
                if(Config.RequestDebugLog != null)
                {
                    WriteConnectionDebugLog(context);
                }
#endif

                //Close the response
                await context.WriteResponseAsync(StopToken!.Token);
                
                /*
                 * If an alternate protocol was specified, we need to break the keepalive loop
                 */

                return keepalive & context.AlternateProtocol == null;
            }
            finally
            {
                //Clean end request
                context.EndRequest();
            }
        }

        private void WriteConnectionDebugLog(HttpContext context)
        {
            //Alloc debug buffer
            using IMemoryHandle<char> debugBuffer = MemoryUtil.SafeAlloc<char>(16 * 1024);

            ForwardOnlyWriter<char> writer = new (debugBuffer.Span);

            //Request
            context.Request.Compile(ref writer);

            //newline
            writer.Append("\r\n");

            //Response
            context.Response.Compile(ref writer);

            Config.RequestDebugLog!.Verbose("\r\n{dbg}", writer.ToString());
        }

        /// <summary>
        /// Reads data synchronously from the transport and attempts to parse an HTTP message and 
        /// built a request. 
        /// </summary>
        /// <param name="transport"></param>
        /// <param name="ctx"></param>
        /// <returns>0 if the request was successfully parsed, the <see cref="HttpStatusCode"/> 
        /// to return to the client because the entity could not be processed</returns>
        /// <remarks>
        /// <para>
        /// This method is synchronous for multiple memory optimization reasons,
        /// and performance is not expected to be reduced as the transport layer should 
        /// <br></br>
        /// only raise an event when a socket has data available to be read, and entity
        /// header sections are expected to fit within a single TCP buffer. 
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private HttpStatusCode ParseRequest(ITransportContext transport, HttpContext ctx)
        {
            //Get the parse buffer
            IHttpHeaderParseBuffer parseBuffer = ctx.Buffers.RequestHeaderParseBuffer;

            //Init parser
            TransportReader reader = new (transport.ConnectionStream, parseBuffer, Config.HttpEncoding, HeaderLineTermination);
           
            try
            {
                //Get the char span
                Span<char> lineBuf = parseBuffer.GetCharSpan();
                
                Http11ParseExtensions.Http1ParseState parseState = new();
                
                //Parse the request line 
                HttpStatusCode code = ctx.Request.Http1ParseRequestLine(ref parseState, ref reader, lineBuf);
                
                if (code > 0)
                {
                    return code;
                }

                //Parse the headers
                code = ctx.Request.Http1ParseHeaders(ref parseState, ref reader, Config, lineBuf);
                if (code > 0)
                {
                    return code;
                }

                //Prepare entity body for request
                code = ctx.Request.Http1PrepareEntityBody(ref parseState, ref reader, Config);
                if (code > 0)
                {
                    return code;
                }

                //Success!
                return 0;
            }
            //Catch exahusted buffer request
            catch (OutOfMemoryException)
            {
                return HttpStatusCode.RequestHeaderFieldsTooLarge;
            }
            catch (UriFormatException)
            {
                return HttpStatusCode.BadRequest;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private async ValueTask<bool> ProcessRequestAsync(HttpContext context, HttpStatusCode status)
        {
            //Check status
            if (status != 0)
            {
                /*
                * If the status of the parsing was not successfull the transnport is considered 
                * an unknowns state and could still have data which could corrupt communications 
                * or worse, contatin an attack. I am choosing to drop the transport and close the 
                * connection if parsing the request fails
                */
                //Close the connection when we exit
                context.Response.Headers[HttpResponseHeader.Connection] = "closed";
                //Return status code, if the the expect header was set, return expectation failed, otherwise return the result status code
                context.Respond(context.Request.Expect ? HttpStatusCode.ExpectationFailed : status);
                //exit and close connection (default result will close the context)
                return false;
            }

            //We only support version 1 and 1/1
            if ((context.Request.HttpVersion & (HttpVersion.Http11 | HttpVersion.Http1)) == 0)
            {
                //Close the connection when we exit
                context.Response.Headers[HttpResponseHeader.Connection] = "closed";
                context.Respond(HttpStatusCode.HttpVersionNotSupported);
                return false;
            }

            //Check open connection count (not super accurate, or might not be atomic)
            if (OpenConnectionCount > Config.MaxOpenConnections)
            {
                //Close the connection and return 503
                context.Response.Headers[HttpResponseHeader.Connection] = "closed";
                context.Respond(HttpStatusCode.ServiceUnavailable);
                return false;
            }
            
            //Store keepalive value from request, and check if keepalives are enabled by the configuration
            bool keepalive = context.Request.KeepAlive & Config.ConnectionKeepAlive > TimeSpan.Zero;
            
            //Set connection header (only for http1 and 1.1)
            if (keepalive)
            {
                context.Response.Headers[HttpResponseHeader.Connection] = "keep-alive";
                context.Response.Headers[HttpResponseHeader.KeepAlive] = KeepAliveTimeoutHeaderValue;
            }
            else
            {
                //Set connection closed
                context.Response.Headers[HttpResponseHeader.Connection] = "closed";
            }

            //Get the server root for the specified location
            if (!ServerRoots.TryGetValue(context.Request.Location.DnsSafeHost, out IWebRoot? root) && !ServerRoots.TryGetValue(WILDCARD_KEY, out root))
            {
                context.Respond(HttpStatusCode.NotFound);
                //make sure control leaves
                return keepalive;
            }

            //check for redirects
            if (root.Redirects.TryGetValue(context.Request.Location.LocalPath, out Redirect? r))
            {
                //301
                context.Redirect301(r.RedirectUrl);
                //Return keepalive
                return keepalive;
            }

            //Check the expect header and return an early status code
            if (context.Request.Expect)
            {
                //send a 100 status code
                await context.Response.SendEarly100ContinueAsync();
            }

            /*
             * Initialze the request body state, which may read/buffer the request
             * entity body. When doing so, the only exceptions that should be 
             * generated are IO, OutOfMemory, and Overflow. IOE should 
             * be raised to the transport as it will only be thrown if the transport
             * is in an unusable state.
             * 
             * OOM and Overflow should only be raised if an over-sized entity
             * body was allowed to be read in. The Parse method should have guarded
             * form data size so oom or overflow would be bugs, and we can let 
             * them get thrown
             */

            await context.InitRequestBodyAsync();

            try
            {
                await ProcessAsync(root, context);
                return keepalive;
            }
            //The user-code requested termination of the connection
            catch (TerminateConnectionException tce)
            {
                //Log the event as a debug so user can see the result
                Config.ServerLog.Debug(tce, "User-code requested a connection termination");
                //See if the exception requested an error code response
                if (tce.Code > 0)
                {
                    //close response with status code
                    context.Respond(tce.Code);
                }
                else
                {
                    //Clear any currently set headers since no response is requested
                    context.Response.Headers.Clear();
                }
            }
            return false;
        }

        /// <summary>
        /// Processes a client connection after pre-processing has completed
        /// </summary>
        /// <param name="root">The <see cref="IWebRoot"/> to process the event on</param>
        /// <param name="ctx">The <see cref="HttpContext"/> to process</param>
        /// <returns>A task that resolves when the user-code has completed processing the entity</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="TerminateConnectionException"></exception>
        private static async ValueTask ProcessAsync(IWebRoot root, HttpContext ctx)
        {
            /*
             * The event object should be cleared when it is no longer in use, IE before 
             * this procedure returns. 
             */
            HttpEvent ev = new (ctx);
            try
            {
                await root.ClientConnectedAsync(ev);
            }
            //User code requested exit, elevate the exception
            catch (TerminateConnectionException)
            {
                throw;
            }
            //Transport exception
            catch(IOException ioe) when (ioe.InnerException is SocketException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ctx.ParentServer.Config.ServerLog.Warn(ex, "Unhandled exception during application code execution.");
            }
            finally
            {
                ev.Clear();
            }
        }
 
    }
}