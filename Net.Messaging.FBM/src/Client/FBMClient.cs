/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Buffers;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// A Fixed Buffer Message Protocol client. Allows for high performance client-server messaging
    /// with minimal memory overhead.
    /// </summary>
    public class FBMClient : VnDisposeable, IStatefulConnection, ICacheHolder
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
        private readonly ReusableStore<FBMRequest> RequestRental;
        private readonly FBMRequest _controlFrame;
        /// <summary>
        /// The configuration for the current client
        /// </summary>
        public FBMClientConfig Config { get; }        
        /// <summary>
        /// A handle that is reset when a connection has been successfully set, and is set
        /// when the connection exists
        /// </summary>
        public ManualResetEvent ConnectionStatusHandle { get; }        
        /// <summary>
        /// The <see cref="ClientWebSocket"/> to send/recieve message on
        /// </summary>
        public ManagedClientWebSocket ClientSocket { get; }
        /// <summary>
        /// Gets the shared control frame for the current instance. The request is reset when 
        /// this property is called. (Not thread safe)
        /// </summary>
        protected FBMRequest ControlFrame
        {
            get
            {
                _controlFrame.Reset();
                return _controlFrame;
            }
        }

        /// <summary>
        /// Creates a new <see cref="FBMClient"/> in a closed state
        /// </summary>
        /// <param name="config">The client configuration</param>
        public FBMClient(FBMClientConfig config)
        {
            RequestRental = ObjectRental.CreateReusable(ReuseableRequestConstructor);
            SendLock = new(1);
            ConnectionStatusHandle = new(true);
            ActiveRequests = new(Environment.ProcessorCount, 100);

            Config = config;
            //Init control frame
            _controlFrame = new (Helpers.CONTROL_FRAME_MID, in config);
            //Init the new client socket
            ClientSocket = new(config.RecvBufferSize, config.RecvBufferSize, config.KeepAliveInterval, config.SubProtocol);
        }

        private void Debug(string format, params string[] strings)
        {
            if(Config.DebugLog != null)
            {
                Config.DebugLog.Debug($"[DEBUG] FBM Client: {format}", strings);
            }
        }
        private void Debug(string format, long value, long other)
        {
            if (Config.DebugLog != null)
            {
                Config.DebugLog.Debug($"[DEBUG] FBM Client: {format}", value, other);
            }
        }

        /// <summary>
        /// Allocates and configures a new <see cref="FBMRequest"/> message object for use within the reusable store
        /// </summary>
        /// <returns>The configured <see cref="FBMRequest"/></returns>
        protected virtual FBMRequest ReuseableRequestConstructor() => new(Config);

        /// <summary>
        /// Asynchronously opens a websocket connection with the specifed remote server
        /// </summary>
        /// <param name="address">The address of the server to connect to</param>
        /// <param name="cancellation">A cancellation token</param>
        /// <returns></returns>
        public async Task ConnectAsync(Uri address, CancellationToken cancellation = default)
        {
            //Uribuilder to send config parameters to the server
            UriBuilder urib = new(address);
            urib.Query +=
                $"{REQ_RECV_BUF_QUERY_ARG}={Config.RecvBufferSize}" +
                $"&{REQ_HEAD_BUF_QUERY_ARG}={Config.MaxHeaderBufferSize}" +
                $"&{REQ_MAX_MESS_QUERY_ARG}={Config.MaxMessageSize}";
            Debug("Connection string {con}", urib.Uri.ToString());
            //Connect to server
            await ClientSocket.ConnectAsync(urib.Uri, cancellation);
            //Reset wait handle before return
            ConnectionStatusHandle.Reset();
            //Begin listeing for requets in a background task
            _ = Task.Run(ProcessContinuousRecvAsync, cancellation);
        }

        /// <summary>
        /// Rents a new <see cref="FBMRequest"/> from the internal <see cref="ReusableStore{T}"/>.
        /// Use <see cref="ReturnRequest(FBMRequest)"/> when request is no longer in use
        /// </summary>
        /// <returns>The configured (rented or new) <see cref="FBMRequest"/> ready for use</returns>
        public FBMRequest RentRequest() => RequestRental.Rent();
        /// <summary>
        /// Stores (or returns) the reusable request in cache for use with <see cref="RentRequest"/>
        /// </summary>
        /// <param name="request">The request to return to the store</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void ReturnRequest(FBMRequest request) => RequestRental.Return(request);

        /// <summary>
        /// Sends a <see cref="FBMRequest"/> to the connected server
        /// </summary>
        /// <param name="request">The request message to send to the server</param>
        /// <param name="cancellationToken"></param>
        /// <returns>When awaited, yields the server response</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="FBMInvalidRequestException"></exception>
        public async Task<FBMResponse> SendAsync(FBMRequest request, CancellationToken cancellationToken = default)
        {
            Check();
            //Length of the request must contains at least 1 int and header byte
            if (request.Length < 1 + sizeof(int))
            {
                throw new FBMInvalidRequestException("Message is not initialized");
            }
            //Store a null value in the request queue so the response can store a buffer
            if (!ActiveRequests.TryAdd(request.MessageId, request))
            {
                throw new ArgumentException("Message with the same ID is already being processed");
            }
            try
            {
                Debug("Sending {bytes} with id {id}", request.RequestData.Length, request.MessageId);
                
                //Reset the wait handle
                request.ResponseWaitEvent.Reset();

                //Wait for send-lock
                using (SemSlimReleaser releaser = await SendLock.GetReleaserAsync(cancellationToken))
                {
                    //Send the data to the server
                    await ClientSocket.SendAsync(request.RequestData, WebSocketMessageType.Binary, true, cancellationToken);
                }

                //wait for the response to be set
                await request.WaitForResponseAsync(cancellationToken);

                Debug("Received {size} bytes for message {id}", request.Response?.Length ?? 0, request.MessageId);

                return request.GetResponse();
            }
            catch
            {
                //Remove the request since packet was never sent
                ActiveRequests.Remove(request.MessageId, out _);
                //Clear waiting flag
                request.ResponseWaitEvent.Set();
                throw;
            }
        }
        /// <summary>
        /// Streams arbitrary binary data to the server with the initial request message
        /// </summary>
        /// <param name="request">The request message to send to the server</param>
        /// <param name="payload">Data to stream to the server</param>
        /// <param name="ct">The content type of the stream of data</param>
        /// <param name="cancellationToken"></param>
        /// <returns>When awaited, yields the server response</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task StreamDataAsync(FBMRequest request, Stream payload, ContentType ct, CancellationToken cancellationToken = default)
        {
            Check();
            //Length of the request must contains at least 1 int and header byte
            if(request.Length < 1 + sizeof(int))
            {
                throw new FBMInvalidRequestException("Message is not initialized");
            }
            //Store a null value in the request queue so the response can store a buffer
            if (!ActiveRequests.TryAdd(request.MessageId, request))
            {
                throw new ArgumentException("Message with the same ID is already being processed");
            }
            try
            {
                Debug("Streaming {bytes} with id {id}", request.RequestData.Length, request.MessageId);
                //Reset the wait handle
                request.ResponseWaitEvent.Reset();
                //Write an empty body in the request
                request.WriteBody(ReadOnlySpan<byte>.Empty, ct);
                //Wait for send-lock
                using (SemSlimReleaser releaser = await SendLock.GetReleaserAsync(cancellationToken))
                {
                    //Send the initial request packet
                    await ClientSocket.SendAsync(request.RequestData, WebSocketMessageType.Binary, false, cancellationToken);
                    //Calc buffer size
                    int bufSize = (int)Math.Clamp(payload.Length, Config.MessageBufferSize, Config.MaxMessageSize);
                    //Alloc a streaming buffer
                    using IMemoryOwner<byte> buffer = Config.BufferHeap.DirectAlloc<byte>(bufSize);
                    //Stream mesage body
                    do
                    {
                        //Read data
                        int read = await payload.ReadAsync(buffer.Memory, cancellationToken);
                        if (read == 0)
                        {
                            //No more data avialable
                            break;
                        }
                        //write message to socket, if the read data was smaller than the buffer, we can send the last packet
                        await ClientSocket.SendAsync(buffer.Memory[..read], WebSocketMessageType.Binary, read < bufSize, cancellationToken);

                    } while (true);
                }
                //wait for the server to respond
                await request.WaitForResponseAsync(cancellationToken);

                Debug("Response recieved {size} bytes for message {id}", request.Response?.Length ?? 0, request.MessageId);
            }
            catch
            {
                //Remove the request since packet was never sent or cancelled
                ActiveRequests.Remove(request.MessageId, out _);
                //Clear wait lock so the request state is reset
                request.ResponseWaitEvent.Set();
                throw;
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
            IMemoryOwner<byte> recvBuffer = Config.BufferHeap.DirectAlloc<byte>(Config.RecvBufferSize);
            try
            {
                //Recv event loop
                while (true)
                {
                    //Listen for incoming packets with the intial data buffer
                    ValueWebSocketReceiveResult result = await ClientSocket.ReceiveAsync(recvBuffer.Memory, CancellationToken.None);
                    //If the message is a close message, its time to exit
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        //Notify the event handler that the connection was closed
                        ConnectionClosed?.Invoke(this, EventArgs.Empty);
                        break;
                    }
                    if (result.Count <= 4)
                    {
                        Debug("Empty message recieved from server");
                        continue;
                    }
                    //Alloc data buffer and write initial data
                    VnMemoryStream responseBuffer = new(Config.BufferHeap);
                    //Copy initial data
                    responseBuffer.Write(recvBuffer.Memory.Span[..result.Count]);
                    //Receive packets until the EOF is reached
                    while (!result.EndOfMessage)
                    {
                        //recive more data
                        result = await ClientSocket.ReceiveAsync(recvBuffer.Memory, CancellationToken.None);
                        //Make sure the buffer is not too large
                        if ((responseBuffer.Length + result.Count) > Config.MaxMessageSize)
                        {
                            //Dispose the buffer before exiting
                            responseBuffer.Dispose();
                            Debug("Recieved a message that was too large, skipped");
                            goto Skip;
                        }
                        //Copy continuous data
                        responseBuffer.Write(recvBuffer.Memory.Span[..result.Count]);
                    }
                    //Reset the buffer stream position
                    _ = responseBuffer.Seek(0, SeekOrigin.Begin);
                    ProcessResponse(responseBuffer);
                //Goto skip statment to cleanup resources
                Skip:;
                }
            }
            catch (OperationCanceledException)
            {
                //Normal closeure, do nothing
            }
            catch (Exception ex)
            {
                //Error event args
                FMBClientErrorEventArgs wsEventArgs = new()
                {
                    Cause = ex,
                    ErrorClient = this
                };
                //Invoke error handler
                ConnectionClosedOnError?.Invoke(this, wsEventArgs);
            }
            finally
            {
                //Dispose the recv buffer
                recvBuffer.Dispose();
                //Set all pending events
                foreach (FBMRequest request in ActiveRequests.Values)
                {
                    request.ResponseWaitEvent.Set();
                }
                //Clear dict
                ActiveRequests.Clear();
                //Cleanup the socket when exiting
                ClientSocket.Cleanup();
                //Set status handle as unset
                ConnectionStatusHandle.Set();
                //Invoke connection closed
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
            if(messageId == Helpers.CONTROL_FRAME_MID)
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
                request.SetResponse(responseMessage);
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
            vms.Dispose();
        }
        /// <summary>
        /// Processes a control frame response from the server
        /// </summary>
        /// <param name="response">The parsed response-packet</param>
        protected virtual void ProcessControlFrame(in FBMResponse response)
        {

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
            await ClientSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
        }
        ///<inheritdoc/>
        protected override void Free()
        {
            //Dispose socket
            ClientSocket.Dispose();
            //Dispose client buffer
            RequestRental.Dispose();
            SendLock.Dispose();
            ConnectionStatusHandle.Dispose();
        }
        ///<inheritdoc/>
        public void CacheClear() => RequestRental.CacheClear();
        ///<inheritdoc/>
        public void CacheHardClear() => RequestRental.CacheHardClear();
    }
}
