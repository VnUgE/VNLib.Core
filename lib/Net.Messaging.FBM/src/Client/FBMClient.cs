/*
* Copyright (c) 2026 Vaughn Nugent
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

        //The minimum number of bytes a valid FBM message can contain, which is the
        //message-id header byte followed by the 4 byte message-id
        private const int MIN_MESSAGE_SIZE = sizeof(int) + 1;

        /// <summary>
        /// Raised when the websocket has been closed because an error occurred.
        /// You may inspect the event args to determine the cause of the error.
        /// </summary>
        public event EventHandler<FBMClientErrorEventArgs>? ConnectionClosedOnError;

        /// <summary>
        /// Raised when the client listener operation has completed as a normal closure
        /// </summary>
        public event EventHandler? ConnectionClosed;

        private readonly SemaphoreSlim SendLock;
        private readonly ConcurrentDictionary<int, FBMRequest> ActiveRequests;
        private readonly IFBMMemoryHandle _streamBuffer;
        private readonly IFbmClientWebsocket _socket;
        private readonly FBMClientConfig _config;

        private readonly IObjectRental<FBMRequest> _requestRental;
        private readonly bool _ownsObjectRental;

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
        /// <param name="websocket">The client websocket wrapper used to communicate with an FBMServer</param>
        public FBMClient(ref readonly FBMClientConfig config, IFbmClientWebsocket websocket)
            : this(in config, websocket, requestRental: null)
        { }

        internal FBMClient(
            ref readonly FBMClientConfig config, 
            IFbmClientWebsocket websocket, 
            IObjectRental<FBMRequest>? requestRental
        )
        {
            ArgumentNullException.ThrowIfNull(websocket);
            ArgumentNullException.ThrowIfNull(config.MemoryManager, nameof(config.MemoryManager));

            _config = config;
            _socket = websocket;

            //Create new request rental if none supplied, it will have to be disposed when the client exits
            if (requestRental is null)
            {
                _ownsObjectRental = true;
                _requestRental = ObjectRental.CreateReusable(ReusableRequestConstructor, 100);
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
            int maxStreamBufSize = Math.Min(config.MaxMessageSize, MAX_STREAM_BUFFER_SIZE);
            _streamBuffer = config.MemoryManager.InitHandle();
            config.MemoryManager.AllocBuffer(_streamBuffer, maxStreamBufSize);
        }

        /// <summary>
        /// Allocates and configures a new <see cref="FBMRequest"/> message object for use within the reusable store
        /// </summary>
        /// <returns>The configured <see cref="FBMRequest"/></returns>
        protected virtual FBMRequest ReusableRequestConstructor() => new(in _config);

        private void Debug(string format, params string[] strings)
            => Config.DebugLog?.Debug($"[DEBUG] FBM Client: {format}", strings);

        private void Debug(string format, long value, long other)
            => Config.DebugLog?.Debug($"[DEBUG] FBM Client: {format}", value, other);


        /// <summary>
        /// Asynchronously opens a WebSocket connection with the specified remote server
        /// </summary>
        /// <param name="serverUri">The address of the server to connect to</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that completes when the connection has been established</returns>
        public async Task ConnectAsync(Uri serverUri, CancellationToken cancellationToken = default)
        {
            //UriBuilder to send config parameters to the server
            UriBuilder urib = new(serverUri);
            urib.Query +=
                $"{REQ_RECV_BUF_QUERY_ARG}={Config.RecvBufferSize}" +
                $"&{REQ_HEAD_BUF_QUERY_ARG}={Config.MaxHeaderBufferSize}" +
                $"&{REQ_MAX_MESS_QUERY_ARG}={Config.MaxMessageSize}";

            Debug("Connection string {con}", urib.Uri.ToString());

            //Connect to server
            await _socket.ConnectAsync(urib.Uri, Headers, cancellationToken)
                .ConfigureAwait(false);

            //Reset wait handle before return
            ConnectionStatusHandle.Reset();

            //Alloc the receive buffer on the calling thread so allocation failures are raised
            //to the caller instead of the background receive task
            IFBMMemoryHandle recvBuffer = Config.MemoryManager.InitHandle();
            Config.MemoryManager.AllocBuffer(recvBuffer, Config.RecvBufferSize);

            //Begin listening for server messages in a background task
            _ = Task.Run(
                () => ProcessContinuousRecvAsync(recvBuffer),
                cancellationToken
            );
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
                using (SemSlimReleaser releaser = await SendLock.GetReleaserAsync(cancellationToken).ConfigureAwait(false))
                {
                    //Send the data to the server
                    await _socket.SendAsync(requestData, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken)
                        .ConfigureAwait(false);
                }

                //wait for the response to be set
                await request.Waiter.GetTask(timeout, cancellationToken)
                    .ConfigureAwait(false);

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
        /// <param name="payload">The stream of data to send as the request body</param>
        /// <param name="contentType">The content type of the streamed data</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A task that resolves when the data is sent and the response is received</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FBMInvalidRequestException"></exception>
        public Task<FBMResponse> StreamDataAsync(
            FBMRequest request,
            Stream payload,
            ContentType contentType,
            CancellationToken cancellationToken = default
        )
            => StreamDataAsync(request, payload, contentType, Config.RequestTimeout, cancellationToken);

        /// <summary>
        /// Streams arbitrary binary data to the server with the initial request message
        /// </summary>
        /// <param name="request">The request message to send to the server</param>
        /// <param name="payload">The stream of data to send as the request body</param>
        /// <param name="contentType">The content type of the streamed data</param>
        /// <param name="timeout">A maximum wait timeout period. If -1 or 0 the timeout is disabled</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A task that resolves when the data is sent and the response is received</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FBMInvalidRequestException"></exception>
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
                //Write an empty body in the request so a content type header is written
                request.WriteBody(default, contentType);

                //Get the request data segment
                ReadOnlyMemory<byte> requestData = request.GetRequestData();

                Debug("Streaming {bytes} with id {id}", requestData.Length, request.MessageId);

                Memory<byte> bufferMemory = _streamBuffer.GetMemory();

                //Wait for send-lock
                using (SemSlimReleaser releaser = await SendLock.GetReleaserAsync(cancellationToken).ConfigureAwait(false))
                {
                    //Send the initial request packet
                    await _socket.SendAsync(
                        buffer: requestData, 
                        WebSocketMessageType.Binary, 
                        endOfMessage: false, 
                        cancellationToken
                    ).ConfigureAwait(false);

                    //Stream the message body, frames are never finalized here since more data may follow
                    while (true)
                    {
                        // Read data from payload stream into buffer
                        int read = await payload.ReadAsync(bufferMemory, cancellationToken)
                            .ConfigureAwait(false);

                        //A zero length read signals the end of the payload
                        if (read == 0)
                        {
                            break;
                        }

                        //Write the message chunk to the socket
                        await _socket.SendAsync(
                            buffer: bufferMemory[..read],
                            WebSocketMessageType.Binary,
                            endOfMessage: false,
                            cancellationToken
                        ).ConfigureAwait(false);
                    }

                    //Payload is exhausted, send an empty terminal frame so the server can
                    //complete the message
                    await _socket.SendAsync(
                        ReadOnlyMemory<byte>.Empty,
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        cancellationToken
                    ).ConfigureAwait(false);
                }

                //wait for the server to respond
                await request.Waiter.GetTask(timeout, cancellationToken)
                    .ConfigureAwait(false);

                Debug("Response received {size} bytes for message {id}", request.Response?.Length ?? 0, request.MessageId);

                return request.GetResponse();
            }
            catch
            {
                //Remove the request since a response will never arrive for it
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
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A task that completes when the connection has been closed</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            Check();

            //Close the connection
            await _socket.DisconnectAsync(WebSocketCloseStatus.NormalClosure, cancellationToken)
                .ConfigureAwait(false);
        }

        private void CheckOrEnqueue(FBMRequest request)
        {
            /*
           * We need to check that the request is not already queued because a wait may be pending
           * and calling SetupAsyncRequest may overwrite another wait and cause a deadlock
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

            //Length of the request must contain at least 1 int and header byte
            if (request.Length < MIN_MESSAGE_SIZE)
            {
                throw new FBMInvalidRequestException("Message is not initialized");
            }
        }

        private static async Task DiscardRecvAsync(IFbmClientWebsocket socket, Memory<byte> buffer)
        {
            ValueWebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);
        }

        /// <summary>
        /// Begins listening for messages from the server on the internal socket (must be connected),
        /// until the socket is closed, or canceled
        /// </summary>
        /// <param name="recvBuffer">
        /// The allocated buffer that individual websocket frames are received into. The buffer is
        /// owned by the receive loop and freed when the loop exits.
        /// </param>
        /// <returns>A task that completes when the receive loop has exited</returns>
        protected async Task ProcessContinuousRecvAsync(IFBMMemoryHandle recvBuffer)
        {
            Debug("Beginning receive loop");

            VnMemoryStream? responseBuffer = null;

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
                    //Listen for incoming packets with the initial data buffer
                    ValueWebSocketReceiveResult result = await _socket.ReceiveAsync(rcvMemory, CancellationToken.None)
                        .ConfigureAwait(false);

                    //If the message is a close message, it's time to exit
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // When the loop breaks, the close connection handler will be invoked
                        break;
                    }

                    /*
                     * A single frame that is too small to hold a message-id is dropped without
                     * allocating a response buffer for it. Multi frame messages must be accumulated
                     * first, because their leading frame may be smaller than a message-id.
                     */
                    if (result.EndOfMessage && result.Count < MIN_MESSAGE_SIZE)
                    {
                        Debug("Empty message received from server");
                        continue;
                    }

                    /*
                     * Guard initial frames from being too large to buffer. If the result 
                     * is the end of the message then it can simply be ignored and we can jump back
                     * to the top of the loop, if there is more data pending it needs to be completely
                     * discarded.
                     */
                    if (result.Count > Config.MaxMessageSize)
                    {
                        if (!result.EndOfMessage)
                        {
                            await DiscardRecvAsync(_socket, rcvMemory)
                              .ConfigureAwait(false);
                        }

                        Debug("Received a message that was too large, skipped");

                        continue;
                    }

                    //Alloc the data buffer and copy the initial data, most messages fit in a single
                    //websocket message so this avoids overallocating a buffer for the common path
                    responseBuffer = new(heap, recvBuffer.GetSpan()[..result.Count]);

                    //Receive frames until the end of the websocket message is reached
                    while (!result.EndOfMessage)
                    {
                        //Receive more data
                        result = await _socket.ReceiveAsync(rcvMemory, CancellationToken.None)
                            .ConfigureAwait(false);

                        //Make sure the message does not exceed the configured maximum size
                        if ((responseBuffer.Length + result.Count) > Config.MaxMessageSize)
                        {
                            Debug("Received a message that was too large, skipped");

                            /*
                             * The remaining frames of the rejected message must be consumed,
                             * otherwise they would be parsed as the start of the next message.
                             */

                            await DiscardRecvAsync(_socket, rcvMemory)
                                .ConfigureAwait(false);

                            //Jump to Skip to dispose the partial message buffer
                            goto Skip;
                        }

                        //Copy the frame data to the message buffer
                        responseBuffer.Write(recvBuffer.GetSpan()[..result.Count]);
                    }

                    //Messages that are too small to hold a message-id cannot be parsed as a response
                    if (responseBuffer.Length < MIN_MESSAGE_SIZE)
                    {
                        Debug("Empty message received from server");
                        goto Skip;
                    }

                    //Reset the buffer stream position before calling handler
                    _ = responseBuffer.Seek(0, SeekOrigin.Begin);
                    ProcessResponse(responseBuffer);
                    responseBuffer = null;

                //Skip label disposes the message buffer if it is still owned by the loop
                Skip:

                    responseBuffer?.Dispose();
                    responseBuffer = null;
                }
            }
            catch (OperationCanceledException)
            {
                //Normal closure, do nothing
            }
            catch (Exception ex)
            {
                FBMClientErrorEventArgs wsEventArgs = new()
                {
                    Cause = ex,
                    ErrorClient = this
                };

                ConnectionClosedOnError?.Invoke(this, wsEventArgs);
            }
            finally
            {
                Config.MemoryManager.FreeBuffer(recvBuffer);

                responseBuffer?.Dispose();

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
        /// Synchronously processes a buffered response packet
        /// </summary>
        /// <param name="responseMessage">The buffered response body received from the server</param>
        /// <remarks>This method blocks the listening task. So operations should be quick</remarks>
        protected virtual void ProcessResponse(VnMemoryStream responseMessage)
        {
            //read first response line
            ReadOnlySpan<byte> line = Helpers.ReadLine(responseMessage);

            //get the id of the message
            int messageId = Helpers.GetMessageId(line);

            //Finalize control frame
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
                    //Failed to complete, dispose the message data
                    responseMessage.Dispose();
                    Debug("Failed to transition waiting request {id}. Message was dropped", messageId, 0);
                }
            }
            else
            {
                //Cleanup, no pending request was waiting
                responseMessage.Dispose();

                Debug("Message {id} was not found in the waiting message queue", messageId, 0);
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
            if (_ownsObjectRental && _requestRental is IDisposable disp)
            {
                disp.Dispose();
            }
        }
    }
}
