/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http.Core;
using VNLib.Net.Http.Core.PerfCounter;
using VNLib.Net.Http.Core.Request;
using VNLib.Net.Http.Core.Response;
using VNLib.Utils.Extensions;
using VNLib.Utils.Logging;
using VNLib.Utils.Memory;

namespace VNLib.Net.Http
{
    public sealed partial class HttpServer
    {

        private int OpenConnectionCount;

        //Event handler method for processing incoming data events
        private async Task DataReceivedAsync(
            ListenerState listenState,
            CancellationTokenSource stopToken,
            ITransportContext transportContext
        )
        {
            Interlocked.Increment(ref OpenConnectionCount);

            HttpContext? context = _contextStore.Rent();

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

                context.InitializeContext(transportContext);

                bool keepAlive;

                //Keepalive loop
                do
                {
                    int read = await context.BufferTransportAsync(stopToken.Token)
                                            .ConfigureAwait(false);
                    if (read == 0)
                    {
                        //Connection closed by remote
                        break;
                    }

                    stream.ReadTimeout = _config.ActiveConnectionRecvTimeout; //Return read timeout to active connection timeout after data is received

                    keepAlive = await ProcessHttpEventAsync(listenState, context);                    
                    
                    stream.ReadTimeout = _keepAliveTimeoutSeconds;  //Timeout reset to keepalive timeout waiting for more data on the transport

                } // Continue if keepalive is enabled and no alternate protocol was requested
                while (keepAlive && context.AlternateProtocol == null);

                /*
                 * If keepalive loop breaks, its possible that the connection
                 * wishes to upgrade to an alternate protocol. 
                 * 
                 * Process it here to allow freeing context related resources
                 */
                if (context.AlternateProtocol != null)
                {
                    IAlternateProtocol ap = context.AlternateProtocol;  //Save the alternate protocol so we can return the context to the pool

                    //Release the context before listening to free it back to the pool
                    _contextStore.Return(context);
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
                    AlternateProtocolTransportStreamWrapper apWrapper = new(transport: stream);
#pragma warning restore CA2000 // Dispose objects before losing scope

                    //Listen on the alternate protocol
                    await ap.RunAsync(apWrapper, stopToken.Token)
                        .ConfigureAwait(false);
                }
            }
            //Catch wrapped socket exceptions
            catch (IOException ioe) when (ioe.InnerException is SocketException se)
            {
                WriteSocketExecption(se);
            }
            catch (SocketException se)
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
            catch (Exception ex)
            {
                _config.ServerLog.Error(ex);
            }

            Interlocked.Decrement(ref OpenConnectionCount);

            //Return the context for normal operation (alternate protocol will return before now so it will be null)
            if (context != null)
            {
                _contextStore.Return(context);
            }

            //All done, time to close transport and exit
            try
            {
                await transportContext.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                _config.ServerLog.Error(ex);
            }
        }


        /*
         * Main even handler. Processes a single http request/response cycle
         * from start to finish. Returning true indicates that the connection
         * can be kept alive and reused for another request.
         */

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private async Task<bool> ProcessHttpEventAsync(ListenerState listenState, HttpContext context)
        {
            HttpPerfCounterState counter = default;

            //Prepare http context to process a new message
            context.BeginRequest();

            try
            {

                {
                    HttpPerfCounter.StartCounter(ref counter);

                    //Try to parse the http request (may throw exceptions, let them propagate to the transport layer)
                    bool parseStatus = ParseAndValidateRequest(context);

                    HttpPerfCounter.StopAndLog(ref counter, in _config, "HTTP Parse");

                    if (!parseStatus)
                    {
                        return false;
                    }
                }
               

                await ProcessRequestAsync(listenState, context);

                // Safe to set the connection header now
                SetResponseConnectionHeader(context);


                // Debug log the request if enabled, only runs on debug builds
                if (_config.RequestDebugLog != null)
                {
                    WriteConnectionDebugLog(context);
                }

                {
                    /*
                     * For security purposes the discard task has been pulled into the processing
                     * code and the parallel optimisation was removed. It was uncommon unless the wrong
                     * endpoint was hit for this optimisation to improve performance during correct operations. 
                     * 
                     * For security it's important to ensure that the request body is fully read/discarded and 
                     * the client is behaving correctly. If the client is misbehaving we want to close the connection
                     * and not allow it to be reused.
                     * 
                     * When paralleling writes and reads if the discard failed it could leave the write in 
                     * an undefined state, so this operatin needs to be done in serial before writing the response.
                     * 
                     * In the future we can consider altering the response and sending an HTTP level error 
                     * but for now we will just drop the connection and clean up.
                     * 
                     */

                    HttpPerfCounter.StartCounter(ref counter);

                    long remainingRequestInput = await context.Request.InputStream
                        .DiscardRemainingAsync()
                        .ConfigureAwait(false);

                    HttpPerfCounter.StopAndLog(ref counter, in _config, "HTTP request discard");

                    /*
                    * If data is remaining after sending, it means that the request was truncated
                    * so check if the request has any remaining data in the input stream.
                    * 
                    * Result should always be 0 unless the client is misbehaving
                    */
                    if (remainingRequestInput > 0)
                    {
                        //Log the truncated request
                        _config.ServerLog.Warn(
                            "Request body was truncated invalid input data, closing connection: {r}",
                            context.Request.State.RemoteEndPoint
                        );

                        return false; //Truncated request, close connection unsafe to reuse
                    }                  
                }

                {

                    HttpPerfCounter.StartCounter(ref counter);

                    await context.WriteAndCloseResponseAsync()
                        .ConfigureAwait(false);

                    await context.FlushTransportAsync()
                        .ConfigureAwait(false);

                    HttpPerfCounter.StopAndLog(ref counter, in _config, "HTTP Response");
                }               

                return !context.ContextFlags.IsSet(HttpControlMask.KeepAliveDisabled);
            }
            finally
            {
                //Clean end request
                context.EndRequest();
            }
        }

        /*
         * Parses an incoming http request from the transport, and validates the request
         * after basic processing. 
         * 
         * This function assumes that all request/response state has been reset to 
         * safe defaults before being called.
         */
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool ParseAndValidateRequest(HttpContext context)
        {
            HttpStatusCode code;

            try
            {
                //Get transport security info
                ref readonly TransportSecurityInfo? secInfo = ref context.GetSecurityInfo();

                if (secInfo.HasValue)
                {
                    //TODO: future support for http2 and http3 over tls               
                }

                context.GetReader(out TransportReader reader);

                /*
                 * Future http2+ support will be added here and will need to check
                 * application layer protocol negotiation from the TLS handshake to help
                 * decide which protocol to parse incoming data as.
                 */

                //Get the char span
                Span<char> lineBuf = context.Buffers.RequestHeaderParseBuffer.GetCharSpan();

                Http11Parser.Http1ParseState parseState = new();

                code = Http11Parser.ParseRequestLine(context.Request, ref parseState, ref reader, lineBuf, secInfo.HasValue);
                if (code > 0)
                {
                    goto ProcessFault;
                }

                code = Http11Parser.ParseHeaders(context.Request, ref parseState, ref reader, in _config, lineBuf);
                if (code > 0)
                {
                    goto ProcessFault;
                }

                if (parseState.ContentLength > 0)
                {
                    /*
                     * When the request has incoming entity body data, we need to prepare
                     * the input stream to handle it. The transport reader may also have buffered 
                     * some of the entity body data, so the reader will "relinquish" control of 
                     * the header buffer to the input stream to avoid a copy.
                     */

                    TransportBufferRemainder remainder = reader.ReleaseBufferRemainder();

                    context.Request.InputStream.Prepare(parseState.ContentLength, in remainder);

                    //Notify request that an entity body has been set, always true if content length > 0
                    ref HttpRequestState reqState = ref context.Request.GetMutableStateForInit();
                    reqState.HasEntityBody = true;
                }

            }
            //Catch exhausted buffer request
            catch (OutOfMemoryException)
            {
                code = HttpStatusCode.RequestHeaderFieldsTooLarge;
            }
            catch (UriFormatException)
            {
                code = HttpStatusCode.BadRequest;
            }

        ProcessFault:

            // Check for below-http level fault
            if ((int)code >= 1000)
            {
                if (_config.ServerLog.IsEnabled(LogLevel.Debug))
                {
                    _config.ServerLog.Debug("Failed to parse request, error: {s}. Force closing transport", (int)code);
                }

                return false;
            }

            /*
             * Process an http-level fault, if code is not 0
             */

            if (code != 0)
            {
                /*
                 * If the status of the parsing was not successful the transport is considered 
                 * an unknown state and could still have data which could corrupt communications 
                 * or worse, contain an attack. I am choosing to drop the transport and close the 
                 * connection if parsing the request fails
                 */

                //Close the connection when we exit
                context.Response.Headers.Set(HttpResponseHeader.Connection, "closed");

                //Return status code, if the the expect header was set, return expectation failed
                if (context.Request.State.Expect)
                {
                    code = HttpStatusCode.ExpectationFailed;                    
                }

                goto ExitFault;
            }

            /******************************
             *  
             *  ENTITY REQUEST VALIDATION
             *  
             *  The following checks are to validate the request after parsing and 
             *  are considered http-level faults and will cause a normal http error
             *  
             ******************************/

            //Make sure the server supports the http version
            if ((context.Request.State.HttpVersion & SupportedVersions) == 0)
            {
                code = HttpStatusCode.HttpVersionNotSupported;
                goto ExitFault;
            }

            //Check open connection count (not super accurate, or might not be atomic)
            if (OpenConnectionCount > _config.MaxOpenConnections)
            {
                //Close the connection and return 503
                code = HttpStatusCode.ServiceUnavailable;
                goto ExitFault;
            }

            /*
             * If the request method is GET, HEAD, or TRACE and a message body was sent
             * we need to drop the request as this is considered a bad request by 
             * the HTTP standard.
             * 
             * If status is already set, were going to exit on an existing fault, otherwise
             * check for the bad request condition.
             */

            if ((context.Request.State.Method & (HttpMethod.GET | HttpMethod.HEAD | HttpMethod.TRACE)) != 0)
            {
                if (context.Request.State.HasEntityBody)
                {
                    _config.ServerLog.Debug(
                        "Message body received from {ip} with GET, HEAD, or TRACE request, was considered an error and the request was dropped",
                        context.Request.State.RemoteEndPoint
                    );

                    // Set status to bad request
                    code = HttpStatusCode.BadRequest;
                    goto ExitFault;
                }
            }

            {
                //Check for chunked transfer encoding
                ReadOnlySpan<char> transfer = context.Request.Headers[HttpRequestHeader.TransferEncoding];

                if (!transfer.IsEmpty && transfer.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                {
                    //Not a valid http version for chunked transfer encoding
                    if (context.Request.State.HttpVersion != HttpVersion.Http11)
                    {
                        code = HttpStatusCode.BadRequest;
                        goto ExitFault;
                    }

                    /*
                     * Was a content length also specified?
                     * This is an issue and is likely an attack. I am choosing not to support 
                     * the HTTP 1.1 standard and will deny reading the rest of the data from the 
                     * transport. 
                     */
                    if (context.Request.InputStream.Length > 0)
                    {
                        _config.ServerLog.Debug(
                            "Possible attempted desync, Content length + chunked encoding specified. RemoteEP: {ip}",
                            context.Request.State.RemoteEndPoint
                        );

                        code = HttpStatusCode.BadRequest;
                        goto ExitFault;
                    }

                    //TODO: Handle chunked transfer encoding (not implemented yet)
                    code = HttpStatusCode.NotImplemented;
                    goto ExitFault;
                }
            }

            // Form-data size guard
            if (
                context.Request.State.ContentType == ContentType.MultiPart
                && context.Request.InputStream.Length > _config.MaxFormDataUploadSize
            )
            {
                code = HttpStatusCode.RequestEntityTooLarge;
                goto ExitFault;
            }

            /*
             * Set the initial keepalive state. User code and internal code will rely on
             * this flag to determine keepalive settings, during response processing.
             */

            context.ContextFlags.Set(
                HttpControlMask.KeepAliveDisabled,
                !(context.Request.State.KeepAlive & _keepAliveTimeoutSeconds > 0)
            );

            return true;

        ExitFault:

            //Close the connection when we exit
            context.Response.Headers.Set(HttpResponseHeader.Connection, "closed");
            context.Respond(code);
            return false;
        }
       

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private async Task ProcessRequestAsync(ListenerState listenState, HttpContext context)
        {
            //Get the server root for the specified location or fallback to a wildcard host if one is selected
        
            if (!listenState.Roots.TryGetValue(context.Request.State.Location.DnsSafeHost, out IWebRoot? root))
            {
                // If not found, fall back to wildcard host if one is set
                root = listenState.DefaultRoute;
            }

            // If still null, no root was found, return 404
            if (root is null)
            {
                context.Respond(HttpStatusCode.NotFound);              
                return;
            }

            //Check the expect header and return an early status code
            if (context.Request.State.Expect)
            {
                //send a 100 status code
                await context.Response.SendEarly100ContinueAsync();
            }

            /*
             * Defer reading/buffering the http entity body and query args until we know
             * the request can be hanledled by user-code. This avoids wasting resources
             * on requests that will be rejected by the server, fast path. 
             * 
             * Parsing the input stream may cause input buffering and transport reads. Exceptions
             * may be raised and we rely on http hooks to cleanup the state.
             * 
             * Think of this stage as another layer. Further processing request data from
             * headers and entity body. The entity body needs to be prepared for user code
             * but again deferred until absolutly ready to process. If an error occurs 
             * as mentioned, hooks will be responsible for cleaning up and the client
             * will be terminated.
             */

            HttpRequestHelpers.ProcessQueryArgs(context.Request);          

            if (context.Request.State.HasEntityBody)
            {
                await HttpRequestHelpers.ParseInputStream(context);
            }          

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

                // Disable keepalive
                context.ContextFlags.Set(HttpControlMask.KeepAliveDisabled);
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
             * The http state should still be salvagable even with a user-code failure, 
             * so we shouldnt need to terminate requests here. This may need to be changed 
             * if a bug is found and users expect the framework to handle the error.
             * The safest option would be terminate the connection, well see.
             * 
             * For now I will allow it.
             */           
        }       

        private void SetResponseConnectionHeader(HttpContext context)
        {
            //Set connection header (only for http1.1)

            if (context.ContextFlags.IsSet(HttpControlMask.KeepAliveDisabled))
            {
                //Set connection closed
                context.Response.Headers.Set(HttpResponseHeader.Connection, "closed");
            }
            else
            {
                /*
                * Request parsing only sets the keepalive flag if the connection is http1.1
                * so we can verify this in an assert
                */
                Debug.Assert(
                    context.Request.State.HttpVersion == HttpVersion.Http11,
                    "Request parsing allowed keepalive for a non http1.1 connection, this is a bug"
                );

                context.Response.Headers.Set(HttpResponseHeader.Connection, "keep-alive");
                context.Response.Headers.Set(HttpResponseHeader.KeepAlive, _keepAliveTimeoutHeaderValue);
            }
        }


        [Conditional("DEBUG")]
        private void WriteConnectionDebugLog(HttpContext context)
        {
#if DEBUG
            //Alloc debug buffer
            using IMemoryHandle<char> debugBuffer = MemoryUtil.SafeAlloc<char>(16 * 1024);

            ForwardOnlyWriter<char> writer = new (debugBuffer.Span);
          
            context.Request.Compile(ref writer);
           
            writer.AppendSmall("\r\n");
            
            context.Response.Compile(ref writer);

            _config.RequestDebugLog!.Verbose("\r\n{dbg}", writer.ToString());
#endif
        }
    }
}
