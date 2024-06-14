/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Net.Http.Core;
using VNLib.Net.Http.Core.Response;
using VNLib.Net.Http.Core.PerfCounter;

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
                Stream stream = transportContext.ConnectionStream;

                /*
                 * Write timeout is constant for the duration of an HTTP 
                 * connection. Read timeout must be set to active on initial
                 * loop because a fresh connection is assumed to have data 
                 * ready.
                 */
                stream.WriteTimeout = _config.SendTimeout;
                stream.ReadTimeout = _config.ActiveConnectionRecvTimeout;

                //Init stream
                context.InitializeContext(transportContext);
                
                //Keep the transport open and listen for messages as long as keepalive is enabled
                do
                {
                    //Attempt to buffer a new (or keepalive) connection async
                    await context.BufferTransportAsync(StopToken!.Token);

                    //Set rx timeout low for initial reading
                    stream.ReadTimeout = _config.ActiveConnectionRecvTimeout;
                    
                    //Process the request
                    bool keepAlive = await ProcessHttpEventAsync(context);

                    //If not keepalive, exit the listening loop and clean up connection
                    if (!keepAlive)
                    {
                        break;
                    }

                    //Reset inactive keeaplive timeout, when expired the following read will throw a cancealltion exception
                    stream.ReadTimeout = (int)_config.ConnectionKeepAlive.TotalMilliseconds;
                    
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
                    stream.ReadTimeout = Timeout.Infinite;
                    stream.WriteTimeout = Timeout.Infinite;

                    /*
                     * Create a transport wrapper so callers cannot take control of the transport
                     * hooks such as disposing. Timeouts are allowed to be changed, not exactly 
                     * our problem.
                     */

#pragma warning disable CA2000 // Dispose objects before losing scope
                    AlternateProtocolTransportStreamWrapper apWrapper = new(transport:stream);
#pragma warning restore CA2000 // Dispose objects before losing scope

                    //Listen on the alternate protocol
                    await ap.RunAsync(apWrapper, StopToken!.Token).ConfigureAwait(false);
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
            //Catch wrapped OC exceptions
            catch (IOException ioe) when (ioe.InnerException is OperationCanceledException oce)
            {
                _config.ServerLog.Debug("Failed to receive transport data within a timeout period {m} connection closed", oce.Message);
            }
            catch (OperationCanceledException oce)
            {
                _config.ServerLog.Debug("Failed to receive transport data within a timeout period {m} connection closed", oce.Message);
            }
            catch(Exception ex)
            {
                _config.ServerLog.Error(ex);
            }
            
            //Dec open connection count
            Interlocked.Decrement(ref OpenConnectionCount);
            
            //Return the context for normal operation (alternate protocol will return before now so it will be null)
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
                _config.ServerLog.Error(ex);
            }
        }


        /// <summary>
        /// Main event handler for all incoming connections
        /// </summary>
        /// <param name="context">Reusable context object</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private async Task<bool> ProcessHttpEventAsync(HttpContext context)
        {
            HttpPerfCounterState counter = default;

            //Prepare http context to process a new message
            context.BeginRequest();
            
            try
            {
                HttpPerfCounter.StartCounter(ref counter);

                //Try to parse the http request (may throw exceptions, let them propagate to the transport layer)
                int status = (int)ParseRequest(context);

                HttpPerfCounter.StopAndLog(ref counter, in _config, "HTTP Parse");

                //Check status code for socket error, if so, return false to close the connection
                if (status >= 1000)
                {
                    return false;
                }

                bool keepalive = true;

                //Handle an error parsing the request
                if(!PreProcessRequest(context, (HttpStatusCode)status, ref keepalive))
                {
                    return false;
                }

                //process the request
                bool processSuccess = await ProcessRequestAsync(context);

#if DEBUG
                static void WriteConnectionDebugLog(HttpServer server, HttpContext context)
                {
                    //Alloc debug buffer
                    using IMemoryHandle<char> debugBuffer = MemoryUtil.SafeAlloc<char>(16 * 1024);

                    ForwardOnlyWriter<char> writer = new (debugBuffer.Span);

                    //Request
                    context.Request.Compile(ref writer);

                    //newline
                    writer.AppendSmall("\r\n");

                    //Response
                    context.Response.Compile(ref writer);

                    server._config.RequestDebugLog!.Verbose("\r\n{dbg}", writer.ToString());
                }

                //Write debug response log
                if(_config.RequestDebugLog != null)
                {
                    WriteConnectionDebugLog(this, context);
                }
#endif

                HttpPerfCounter.StartCounter(ref counter);
               
                await context.WriteResponseAsync();

                await context.FlushTransportAsync();

                HttpPerfCounter.StopAndLog(ref counter, in _config, "HTTP Response");

                /*
                 * If an alternate protocol was specified, we need to break the keepalive loop
                 * the handler will manage the alternate protocol
                 */

                return processSuccess & keepalive & context.AlternateProtocol == null;
            }
            finally
            {
                //Clean end request
                context.EndRequest();
            }
        }

        /// <summary>
        /// Reads data synchronously from the transport and attempts to parse an HTTP message and 
        /// built a request. 
        /// </summary>
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
        private HttpStatusCode ParseRequest(HttpContext ctx)
        {
            //Get transport security info
            ref readonly TransportSecurityInfo? secInfo = ref ctx.GetSecurityInfo();

            if (secInfo.HasValue)
            {
                //TODO: future support for http2 and http3 over tls
            }

            ctx.GetReader(out TransportReader reader);

            HttpStatusCode code;

            try
            {
                //Get the char span
                Span<char> lineBuf = ctx.Buffers.RequestHeaderParseBuffer.GetCharSpan();
                
                Http11ParseExtensions.Http1ParseState parseState = new();
                
                if ((code = ctx.Request.Http1ParseRequestLine(ref parseState, ref reader, lineBuf, secInfo.HasValue)) > 0)
                {
                    return code;
                }

                //Parse the headers
                if ((code = ctx.Request.Http1ParseHeaders(ref parseState, ref reader, in _config, lineBuf)) > 0)
                {
                    return code;
                }

                //Prepare entity body for request
                if ((code = ctx.Request.Http1PrepareEntityBody(ref parseState, ref reader, in _config)) > 0)
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

        private bool PreProcessRequest(HttpContext context, HttpStatusCode status, ref bool keepalive)
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
                context.Response.Headers.Set(HttpResponseHeader.Connection, "closed");
                //Return status code, if the the expect header was set, return expectation failed, otherwise return the result status code
                context.Respond(context.Request.State.Expect ? HttpStatusCode.ExpectationFailed : status);
                //exit and close connection (default result will close the context)
                return false;
            }

            //Make sure the server supports the http version
            if ((context.Request.State.HttpVersion & SupportedVersions) == 0)
            {
                //Close the connection when we exit
                context.Response.Headers.Set(HttpResponseHeader.Connection, "closed");
                context.Respond(HttpStatusCode.HttpVersionNotSupported);
                return false;
            }

            //Check open connection count (not super accurate, or might not be atomic)
            if (OpenConnectionCount > _config.MaxOpenConnections)
            {
                //Close the connection and return 503
                context.Response.Headers.Set(HttpResponseHeader.Connection, "closed");
                context.Respond(HttpStatusCode.ServiceUnavailable);
                return false;
            }

            //Store keepalive value from request, and check if keepalives are enabled by the configuration
            keepalive = context.Request.State.KeepAlive & _config.ConnectionKeepAlive > TimeSpan.Zero;

            //Set connection header (only for http1.1)

            if (keepalive)
            {
                /*
                * Request parsing only sets the keepalive flag if the connection is http1.1
                * so we can verify this in an assert
                */
                Debug.Assert(context.Request.State.HttpVersion == HttpVersion.Http11, "Request parsing allowed keepalive for a non http1.1 connection, this is a bug");

                context.Response.Headers.Set(HttpResponseHeader.Connection, "keep-alive");
                context.Response.Headers.Set(HttpResponseHeader.KeepAlive, KeepAliveTimeoutHeaderValue);
            }
            else
            {
                //Set connection closed
                context.Response.Headers.Set(HttpResponseHeader.Connection, "closed");
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private async Task<bool> ProcessRequestAsync(HttpContext context)
        {
            //Get the server root for the specified location or fallback to a wildcard host if one is selected
            IWebRoot? root = ServerRoots!.GetValueOrDefault(context.Request.State.Location.DnsSafeHost, _wildcardRoot);
            
            if (root == null)
            {
                context.Respond(HttpStatusCode.NotFound);
                //make sure control leaves
                return true;
            }

            //Check the expect header and return an early status code
            if (context.Request.State.Expect)
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

            /*
             * The event object should be cleared when it is no longer in use, IE before 
             * this procedure returns. 
             */
            HttpEvent ev = new(context);

            try
            {
                //Enter user-code
                await root.ClientConnectedAsync(ev);
            }
            //The user-code requested termination of the connection
            catch (TerminateConnectionException tce)
            {
                //Log the event as a debug so user can see it was handled
                _config.ServerLog.Debug(tce, "User-code requested a connection termination");

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

                //Close connection
                return false;
            }
            //Transport exception
            catch (IOException ioe) when (ioe.InnerException is SocketException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _config.ServerLog.Warn(ex, "Unhandled exception during application code execution.");
            }
            finally
            {
                ev.Clear();
            }

            /*
             * The http state should still be salvagable event with a user-code failure, 
             * so we shouldnt need to terminate requests here. This may need to be changed 
             * if a bug is found and users expect the framework to handle the error.
             * The safest option would be terminate the connection, well see.
             * 
             * For now I will allow it.
             */
            return true;
        }
 
    }
}
