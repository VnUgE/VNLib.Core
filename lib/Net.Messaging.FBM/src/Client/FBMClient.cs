/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMClient.cs 
*
* FBMClient.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Memory.Caching;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

namespace VNLib.Net.Messaging.FBM.Client
{

    /// <summary>
    /// A Fixed Buffer Message Protocol client. Allows for high performance client-server messaging
    /// with minimal memory overhead.
    /// </summary>
    public class FBMClient : VnDisposeable, IStatefulConnection
    {
        /// <summary>
        /// The WS connection query arguments to specify a receive buffer size
        /// </summary>
        public const string REQ_RECV_BUF_QUERY_ARG = "b";
        /// <summary>
        /// The WS connection query argument to suggest a maximum response header buffer size
        /// </summary>
        public const string REQ_HEAD_BUF_QUERY_ARG = "hb";
        /// <summary>
        /// The WS connection query argument to suggest a maximum message size
        /// </summary>
        public const string REQ_MAX_MESS_QUERY_ARG = "mx";

        public const int MAX_STREAM_BUFFER_SIZE = 128 * 1024;

        /// <summary>
        /// Raised when the websocket has been closed because an error occured.
        /// You may inspect the event args to determine the cause of the error.
        /// </summary>
        public event EventHandler<FMBClientErrorEventArgs>? ConnectionClosedOnError;

        /// <summary>
        /// Raised when the client listener operaiton has completed as a normal closure
        /// </summary>
        public event EventHandler? ConnectionClosed;

        private readonly SemaphoreSlim SendLock;
        private readonly ConcurrentDictionary<int, FBMRequest> ActiveRequests;
        private readonly IFBMMemoryHandle _streamBuffer;
        private readonly IFbmClientWebsocket _socket;
        private readonly FBMClientConfig _config;

        private readonly IObjectRental<FBMRequest> _requestRental;
        private readonly bool _ownsObjectRenal;

        /// <summary>
        /// The configuration for the current client
        /// </summary>
        public ref readonly FBMClientConfig Config => ref _config;

        /// <summary>
        /// A handle that is reset when a connection has been successfully established, and is set
        /// when the connection exists
        /// </summary>
        public ManualResetEvent ConnectionStatusHandle { get; }

        /// <summary>
        /// The client's http header collection used when making connections 
        /// </summary>
        public VnWebHeaderCollection Headers { get; }

        /// <summary>
        /// Creates an immutable FBMClient that wraps the supplied web socket using the
        /// supplied config.
        /// </summary>
        /// <param name="config">The client config</param>
        /// <param name="websocket">The websocket instance used to comunicate with an FBMServer</param>
        public FBMClient(ref readonly FBMClientConfig config, IFbmClientWebsocket websocket)
            : this(in config, websocket, requestRental: null)
        { }

        internal FBMClient(ref readonly FBMClientConfig config, IFbmClientWebsocket websocket, IObjectRental<FBMRequest>? requestRental)
        {
            ArgumentNullException.ThrowIfNull(websocket);
            ArgumentNullException.ThrowIfNull(config.MemoryManager, nameof(config.MemoryManager));

            _config = config;
            _socket = websocket;

            //Create new request rental if none supplied, it will have to be disposed when the client exits
            if (requestRental is null)
            {
                _ownsObjectRenal = true;
                _requestRental = ObjectRental.CreateReusable(ReuseableRequestConstructor, 100);
            }
            else
            {
                _requestRental = requestRental;
            }

            Headers = [];
            SendLock = new(1);
            ConnectionStatusHandle = new(true);
            ActiveRequests = new(Environment.ProcessorCount, 100);

            /*
             * We can use the pool to allocate a single stream buffer that will be shared. 
             * This is because there is only 1 thread allowed to send/copy data at a time
             * so it can be allocated once and shared
             */
            int maxStrmBufSize = Math.Min(config.MaxMessageSize, MAX_STREAM_BUFFER_SIZE);
            _streamBuffer = config.MemoryManager.InitHandle();
            config.MemoryManager.AllocBuffer(_streamBuffer, maxStrmBufSize);
        }

        /// <summary>
        /// Allocates and configures a new <see cref="FBMRequest"/> message object for use within the reusable store
        /// </summary>
        /// <returns>The configured <see cref="FBMRequest"/></returns>
        protected virtual FBMRequest ReuseableRequestConstructor() => new(in _config);

        private void Debug(string format, params string[] strings)
            => Config.DebugLog?.Debug($"[DEBUG] FBM Client: {format}", strings);

        private void Debug(string format, long value, long other)
            => Config.DebugLog?.Debug($"[DEBUG] FBM Client: {format}", value, other);


        /// <summary>
        /// Asynchronously opens a websocket connection with the specifed remote server
        /// </summary>
        /// <param name="serverUri">The address of the server to connect to</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns></returns>
        public async Task ConnectAsync(Uri serverUri, CancellationToken cancellationToken = default)
        {
            //Uribuilder to send config parameters to the server
            UriBuilder urib = new(serverUri);
            urib.Query +=
                $"{REQ_RECV_BUF_QUERY_ARG}={Config.RecvBufferSize}" +
                $"&{REQ_HEAD_BUF_QUERY_ARG}={Config.MaxHeaderBufferSize}" +
                $"&{REQ_MAX_MESS_QUERY_ARG}={Config.MaxMessageSize}";

            Debug("Connection string {con}", urib.Uri.ToString());

            //Connect to server
            await _socket.ConnectAsync(urib.Uri, Headers, cancellationToken);

            //Reset wait handle before return
            ConnectionStatusHandle.Reset();

            //Begin listeing for requests in a background task
            _ = Task.Run(ProcessContinuousRecvAsync, cancellationToken);
        }

        /// <summary>
        /// Rents a new <see cref="FBMRequest"/> from the internal <see cref="ObjectRental{T}"/>.
        /// Use <see cref="ReturnRequest(FBMRequest)"/> when request is no longer in use
        /// </summary>
        /// <returns>The configured (rented or new) <see cref="FBMRequest"/> ready for use</returns>
        public FBMRequest RentRequest() => _requestRental.Rent();

        /// <summary>
        /// Stores (or returns) the reusable request in cache for use with <see cref="RentRequest"/>
        /// </summary>
        /// <param name="request">The request to return to the store</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void ReturnRequest(FBMRequest request) => _requestRental.Return(request);

        /// <summary>
        /// Sends a <see cref="FBMRequest"/> to the connected server
        /// </summary>
        /// <param name="request">The request message to send to the server</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>When awaited, yields the server response</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FBMInvalidRequestException"></exception>
        public Task<FBMResponse> SendAsync(FBMRequest request, CancellationToken cancellationToken = default)
            => SendAsync(request, Config.RequestTimeout, cancellationToken);

        /// <summary>
        /// Sends a <see cref="FBMRequest"/> to the connected server
        /// </summary>
        /// <param name="request">The request message to send to the server</param>
        /// <param name="cancellationToken">A token to cancel the async send operation</param>
        /// <param name="timeout">
        /// A maximum time to wait for the operation to complete before the wait is cancelled. An infinite 
        /// timeout (-1) or 0 will disable the timer.
        /// </param>
        /// <returns>When awaited, yields the server response</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FBMInvalidRequestException"></exception>
        public async Task<FBMResponse> SendAsync(FBMRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Check();

            cancellationToken.ThrowIfCancellationRequested();

            ValidateRequest(request);

            CheckOrEnqueue(request);

            try
            {

                //Get the request data segment
                ReadOnlyMemory<byte> requestData = request.GetRequestData();

                Debug("Sending {bytes} with id {id}", requestData.Length, request.MessageId);

                //Wait for send-lock
                using (SemSlimReleaser releaser = await SendLock.GetReleaserAsync(cancellationToken))
                {
                    //Send the data to the server
                    await _socket.SendAsync(requestData, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
                }

                //wait for the response to be set
                await request.Waiter.GetTask(timeout, cancellationToken).ConfigureAwait(true);

                Debug("Received {size} bytes for message {id}", request.Response?.Length ?? 0, request.MessageId);

                //Get the response data
                return request.GetResponse();
            }
            catch
            {
                //Remove the request since packet was never sent
                ActiveRequests.Remove(request.MessageId, out _);
                throw;
            }
            finally
            {
                //Always cleanup waiter
                request.Waiter.OnEndRequest();
            }
        }

        /// <summary>
        /// Streams arbitrary binary data to the server with the initial request message
        /// </summary>
        /// <param name="request">The request message to send to the server</param>
        /// <param name="payload">Data to stream to the server</param>
        /// <param name="contentType">The content type of the stream of data</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A task that resolves when the data is sent and the resonse is received</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public Task<FBMResponse> StreamDataAsync(FBMRequest request, Stream payload, ContentType contentType, CancellationToken cancellationToken = default)
            => StreamDataAsync(request, payload, contentType, Config.RequestTimeout, cancellationToken);

        /// <summary>
        /// Streams arbitrary binary data to the server with the initial request message
        /// </summary>
        /// <param name="request">The request message to send to the server</param>
        /// <param name="payload">Data to stream to the server</param>
        /// <param name="contentType">The content type of the stream of data</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <param name="timeout">A maxium wait timeout period. If -1 or 0 the timeout is disabled</param>
        /// <returns>A task that resolves when the data is sent and the resonse is received</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<FBMResponse> StreamDataAsync(
            FBMRequest request,
            Stream payload,
            ContentType contentType,
            TimeSpan timeout,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(payload);

            Check();

            cancellationToken.ThrowIfCancellationRequested();

            ValidateRequest(request);

            CheckOrEnqueue(request);

            try
            {
                //Get the request data segment
                ReadOnlyMemory<byte> requestData = request.GetRequestData();

                Debug("Streaming {bytes} with id {id}", requestData.Length, request.MessageId);

                //Write an empty body in the request so a content type header is writen
                request.WriteBody(default, contentType);

                Memory<byte> bufferMemory = _streamBuffer.GetMemory();

                //Wait for send-lock
                using (SemSlimReleaser releaser = await SendLock.GetReleaserAsync(cancellationToken))
                {
                    //Send the initial request packet
                    await _socket.SendAsync(requestData, WebSocketMessageType.Binary, endOfMessage: false, cancellationToken);

                    //Stream mesage body
                    do
                    {
                        //Read data
                        int read = await payload.ReadAsync(bufferMemory, cancellationToken);

                        if (read == 0)
                        {
                            //No more data avialable
                            break;
                        }

                        //write message to socket, if the read data was smaller than the buffer, we can send the last packet
                        await _socket.SendAsync(
                            buffer: bufferMemory[..read],
                            WebSocketMessageType.Binary,
                            endOfMessage: read < bufferMemory.Length,
                            cancellationToken
                        );

                    } while (true);
                }

                //wait for the server to respond
                await request.Waiter.GetTask(timeout, cancellationToken).ConfigureAwait(true);

                Debug("Response recieved {size} bytes for message {id}", request.Response?.Length ?? 0, request.MessageId);


                return request.GetResponse();
            }
            catch
            {
                //Remove the request since packet was never sent or cancelled
                _ = ActiveRequests.TryRemove(request.MessageId, out _);
                throw;
            }
            finally
            {
                //Always cleanup waiter
                request.Waiter.OnEndRequest();
            }
        }

        /// <summary>
        /// Closes the underlying <see cref="WebSocket"/> and cancels all pending operations
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            Check();
            //Close the connection
            await _socket.DisconnectAsync(WebSocketCloseStatus.NormalClosure, cancellationToken);
        }

        private void CheckOrEnqueue(FBMRequest request)
        {
            /*
           * We need to check that the request is not already queued because a wait may be pending
           * and calling SetupAsyncRequest may overwite another wait and cause a deadlock
           */

            if (!ActiveRequests.TryAdd(request.MessageId, request))
            {
                throw new ArgumentException("Message with the same ID is already being processed");
            }

            //Configure the request/response task
            request.Waiter.OnBeginRequest();
        }

        private static void ValidateRequest(FBMRequest? request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.MessageId == 0)
            {
                throw new FBMInvalidRequestException("The request message id must NOT be 0");
            }

            //Length of the request must contains at least 1 int and header byte
            if (request.Length < 1 + sizeof(int))
            {
                throw new FBMInvalidRequestException("Message is not initialized");
            }
        }

        /// <summary>
        /// Begins listening for messages from the server on the internal socket (must be connected),
        /// until the socket is closed, or canceled
        /// </summary>
        /// <returns></returns>
        protected async Task ProcessContinuousRecvAsync()
        {
            Debug("Begining receive loop");

            //Alloc recv buffer
            IFBMMemoryHandle recvBuffer = Config.MemoryManager.InitHandle();
            Config.MemoryManager.AllocBuffer(recvBuffer, Config.RecvBufferSize);
            try
            {
                if (!Config.MemoryManager.TryGetHeap(out IUnmanagedHeap? heap))
                {
                    throw new NotSupportedException("The memory manager must support using IUnmanagedHeaps");
                }

                Memory<byte> rcvMemory = recvBuffer.GetMemory();

                //Recv event loop
                while (true)
                {
                    //Listen for incoming packets with the intial data buffer
                    ValueWebSocketReceiveResult result = await _socket.ReceiveAsync(rcvMemory, CancellationToken.None);

                    //If the message is a close message, its time to exit
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // When the loop breaks, the close connection handler will be invoked
                        break;
                    }
                    if (result.Count <= 4)
                    {
                        Debug("Empty message recieved from server");
                        continue;
                    }

                    //Alloc data buffer and write initial data
                    VnMemoryStream responseBuffer = new(heap, recvBuffer.GetSpan()[..result.Count]);

                    //Receive packets until the EOF is reached
                    while (!result.EndOfMessage)
                    {
                        //recive more data
                        result = await _socket.ReceiveAsync(rcvMemory, CancellationToken.None);

                        //Make sure the buffer is not too large
                        if ((responseBuffer.Length + result.Count) > Config.MaxMessageSize)
                        {
                            //Dispose the buffer before exiting
                            responseBuffer.Dispose();
                            Debug("Recieved a message that was too large, skipped");
                            goto Skip;
                        }

                        //Copy continuous data
                        responseBuffer.Write(recvBuffer.GetSpan()[..result.Count]);
                    }

                    //Reset the buffer stream position before calling handler
                    _ = responseBuffer.Seek(0, SeekOrigin.Begin);
                    ProcessResponse(responseBuffer);

                //Goto skip statment to cleanup resources
                Skip:
                    ;
                }
            }
            catch (OperationCanceledException)
            {
                //Normal closeure, do nothing
            }
            catch (Exception ex)
            {                
                FMBClientErrorEventArgs wsEventArgs = new()
                {
                    Cause = ex,
                    ErrorClient = this
                };
               
                ConnectionClosedOnError?.Invoke(this, wsEventArgs);
            }
            finally
            {               
                Config.MemoryManager.FreeBuffer(recvBuffer);               

                // Set all pending events
                foreach (FBMRequest request in ActiveRequests.Values)
                {
                    request.Waiter.ManualCancellation();
                }

                ActiveRequests.Clear();

                // Signal to waiters the connection is closed
                ConnectionStatusHandle.Set();               

                // Invoke connection closed
                ConnectionClosed?.Invoke(this, EventArgs.Empty);
            }

            Debug("Receive loop exited");
        }

        /// <summary>
        /// Syncrhonously processes a buffered response packet
        /// </summary>
        /// <param name="responseMessage">The buffered response body recieved from the server</param>
        /// <remarks>This method blocks the listening task. So operations should be quick</remarks>
        protected virtual void ProcessResponse(VnMemoryStream responseMessage)
        {
            //read first response line
            ReadOnlySpan<byte> line = Helpers.ReadLine(responseMessage);

            //get the id of the message
            int messageId = Helpers.GetMessageId(line);

            //Finalze control frame
            if (messageId == Helpers.CONTROL_FRAME_MID)
            {
                Debug("Control frame received");
                ProcessControlFrame(responseMessage);
                return;
            }
            else if (messageId < 0)
            {
                //Cannot process request
                responseMessage.Dispose();
                Debug("Invalid messageid");
                return;
            }

            //Search for the request that has the same id
            if (ActiveRequests.TryRemove(messageId, out FBMRequest? request))
            {
                //Set the new response message
                if (!request.Waiter.Complete(responseMessage))
                {
                    //Falied to complete, dispose the message data
                    responseMessage.Dispose();
                    Debug("Failed to transition waiting request {id}. Message was dropped", messageId, 0);
                }
            }
            else
            {
                Debug("Message {id} was not found in the waiting message queue", messageId, 0);

                //Cleanup no request was waiting
                responseMessage.Dispose();
            }
        }

        /// <summary>
        /// Processes a control frame response from the server
        /// </summary>
        /// <param name="vms">The raw response packet from the server</param>
        private void ProcessControlFrame(VnMemoryStream vms)
        {
            Debug("Client control frame received. Size: {size}", vms.Length.ToString());
            vms.Dispose();
        }

        /// <summary>
        /// Processes a control frame response from the server
        /// </summary>
        /// <param name="response">The parsed response-packet</param>
        protected virtual void ProcessControlFrame(in FBMResponse response)
        {

        }

        ///<inheritdoc/>
        protected override void Free()
        {
            //Free stream buffer
            Config.MemoryManager.FreeBuffer(_streamBuffer);

            //Dispose client buffer
            _socket.Dispose();
            SendLock.Dispose();
            ConnectionStatusHandle.Dispose();

            //Dispose object rental if we own it
            if (_ownsObjectRenal && _requestRental is IDisposable disp)
            {
                disp.Dispose();
            }
        }
    }
}
