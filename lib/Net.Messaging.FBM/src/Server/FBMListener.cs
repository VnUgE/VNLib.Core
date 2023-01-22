/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMListener.cs 
*
* FBMListener.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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

using VNLib.Utils.IO;
using VNLib.Utils.Async;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Caching;
using VNLib.Plugins.Essentials;

namespace VNLib.Net.Messaging.FBM.Server
{

    /// <summary>
    /// Method delegate for processing FBM messages from an <see cref="FBMListener"/>
    /// when messages are received
    /// </summary>
    /// <param name="context">The message/connection context</param>
    /// <param name="userState">The state parameter passed on client connected</param>
    /// <param name="cancellationToken">A token that reflects the state of the listener</param>
    /// <returns>A <see cref="Task"/> that resolves when processing is complete</returns>
    public delegate Task RequestHandler(FBMContext context, object? userState, CancellationToken cancellationToken);

    /// <summary>
    /// A FBM protocol listener. Listens for messages on a <see cref="WebSocketSession"/>
    /// and raises events on requests.
    /// </summary>
    public class FBMListener
    {
        private sealed class ListeningSession
        {
            private readonly ReusableStore<FBMContext> CtxStore;
            private readonly CancellationTokenSource Cancellation;
            private readonly CancellationTokenRegistration Registration;
            private readonly FBMListenerSessionParams Params;
            

            public readonly object? UserState;

            public readonly SemaphoreSlim ResponseLock;

            public readonly WebSocketSession Socket;

            public readonly RequestHandler OnRecieved;

            public CancellationToken CancellationToken => Cancellation.Token;


            public ListeningSession(WebSocketSession session, RequestHandler onRecieved, in FBMListenerSessionParams args, object? userState)
            {
                Params = args;
                Socket = session;
                UserState = userState;
                OnRecieved = onRecieved;

                //Create cancellation and register for session close
                Cancellation = new();
                Registration = session.Token.Register(Cancellation.Cancel);


                ResponseLock = new(1);
                CtxStore = ObjectRental.CreateReusable(ContextCtor);
            }

            private FBMContext ContextCtor() => new(Params.MaxHeaderBufferSize, Params.ResponseBufferSize, Params.HeaderEncoding);

            /// <summary>
            /// Cancels any pending opreations relating to the current session
            /// </summary>
            public void CancelSession()
            {
                Cancellation.Cancel();

                //If dispose happens without any outstanding requests, we can dispose the session
                if (_counter == 0)
                {
                    CleanupInternal();
                }
            }

            private void CleanupInternal()
            {
                Registration.Dispose();
                CtxStore.Dispose();
                Cancellation.Dispose();
                ResponseLock.Dispose();
            }


            private uint _counter;

            /// <summary>
            /// Rents a new <see cref="FBMContext"/> instance from the pool
            /// and increments the counter
            /// </summary>
            /// <returns>The rented instance</returns>
            /// <exception cref="ObjectDisposedException"></exception>
            public FBMContext RentContext()
            {
                
                if (Cancellation.IsCancellationRequested)
                {
                    throw new ObjectDisposedException("The instance has been disposed");
                }

                //Rent context
                FBMContext ctx = CtxStore.Rent();
                //Increment counter
                Interlocked.Increment(ref _counter);

                return ctx;
            }

            /// <summary>
            /// Returns a previously rented context to the pool
            /// and decrements the counter. If the session has been
            /// cancelled, when the counter reaches 0, cleanup occurs
            /// </summary>
            /// <param name="ctx">The context to return</param>
            public void ReturnContext(FBMContext ctx)
            {
                //Return the context
                CtxStore.Return(ctx);

                uint current = Interlocked.Decrement(ref _counter);

                //No more contexts in use, dispose internals
                if (Cancellation.IsCancellationRequested && current == 0)
                {
                    CleanupInternal();
                }
            }
        }

        public const int SEND_SEMAPHORE_TIMEOUT_MS = 10 * 1000;

        private readonly IUnmangedHeap Heap;

        /// <summary>
        /// Raised when a response processing error occured
        /// </summary>
        public event EventHandler<Exception>? OnProcessError;

        /// <summary>
        /// Creates a new <see cref="FBMListener"/> instance ready for 
        /// processing connections
        /// </summary>
        /// <param name="heap">The heap to alloc buffers from</param>
        public FBMListener(IUnmangedHeap heap)
        {
            Heap = heap;
        }

        /// <summary>
        /// Begins listening for requests on the current websocket until 
        /// a close message is received or an error occurs
        /// </summary>
        /// <param name="wss">The <see cref="WebSocketSession"/> to receive messages on</param>
        /// <param name="handler">The callback method to handle incoming requests</param>
        /// <param name="args">The arguments used to configured this listening session</param>
        /// <param name="userState">A state parameter</param>
        /// <returns>A <see cref="Task"/> that completes when the connection closes</returns>
        public async Task ListenAsync(WebSocketSession wss, RequestHandler handler, FBMListenerSessionParams args, object? userState)
        {
            ListeningSession session = new(wss, handler, args, userState);
            //Alloc a recieve buffer
            using IMemoryOwner<byte> recvBuffer = Heap.DirectAlloc<byte>(args.RecvBufferSize);

            //Init new queue for dispatching work
            AsyncQueue<VnMemoryStream> workQueue = new(true, true);
            
            //Start a task to process the queue
            Task queueWorker = QueueWorkerDoWork(workQueue, session);
            
            try
            {
                //Listen for incoming messages
                while (true)
                {
                    //Receive a message
                    ValueWebSocketReceiveResult result = await wss.ReceiveAsync(recvBuffer.Memory);
                    //If a close message has been received, we can gracefully exit
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        //Return close message
                        await wss.CloseSocketAsync(WebSocketCloseStatus.NormalClosure, "Goodbye");
                        //break listen loop
                        break;
                    }
                    //create buffer for storing data
                    VnMemoryStream request = new(Heap);
                    //Copy initial data
                    request.Write(recvBuffer.Memory.Span[..result.Count]);
                    //Streaming read
                    while (!result.EndOfMessage)
                    {
                        //Read more data
                        result = await wss.ReceiveAsync(recvBuffer.Memory);
                        //Make sure the request is small enough to buffer
                        if (request.Length + result.Count > args.MaxMessageSize)
                        {
                            //dispose the buffer
                            request.Dispose();
                            //close the socket with a message too big
                            await wss.CloseSocketAsync(WebSocketCloseStatus.MessageTooBig, "Buffer space exceeded for message. Goodbye");
                            //break listen loop
                            goto Exit;
                        }
                        //write to buffer
                        request.Write(recvBuffer.Memory.Span[..result.Count]);
                    }
                    //Make sure data is available
                    if (request.Length == 0)
                    {
                        request.Dispose();
                        continue;
                    }
                    //reset buffer position
                    _ = request.Seek(0, SeekOrigin.Begin);
                    //Enqueue the request
                    await workQueue.EnqueueAsync(request);
                }

            Exit:
                ;
            }
            finally
            {
                session.CancelSession();
                await queueWorker.ConfigureAwait(false);
            }
        }

        private async Task QueueWorkerDoWork(AsyncQueue<VnMemoryStream> queue, ListeningSession session)
        {
            try
            {
                while (true)
                {
                    //Get work from queue
                    VnMemoryStream request = await queue.DequeueAsync(session.CancellationToken);
                    //Process request without waiting
                    _ = ProcessAsync(request, session).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            { }
            finally
            {
                //Cleanup any queued requests
                while (queue.TryDequeue(out VnMemoryStream? stream))
                {
                    stream.Dispose();
                }
            }
        }

        private async Task ProcessAsync(VnMemoryStream data, ListeningSession session)
        {
            //Rent a new request object
            FBMContext context = session.RentContext();
            try
            {
                //Prepare the request/response
                context.Prepare(data, session.Socket.SocketID);

                if ((context.Request.ParseStatus & HeaderParseError.InvalidId) > 0)
                {
                    OnProcessError?.Invoke(this, new FBMException($"Invalid messageid {context.Request.MessageId}, message length {data.Length}"));
                    return;
                }
                
                //Check parse status flags
                if ((context.Request.ParseStatus & HeaderParseError.HeaderOutOfMem) > 0)
                {
                    OnProcessError?.Invoke(this, new FBMException("Packet received with not enough space to store headers"));
                }
                //Determine if request is an out-of-band message
                else if (context.Request.MessageId == Helpers.CONTROL_FRAME_MID)
                {
                    //Process control frame
                    await ProcessOOBAsync(context);
                }
                else
                {
                    //Invoke normal message handler
                    await session.OnRecieved.Invoke(context, session.UserState, session.CancellationToken);
                }

                //Get response data
                await using IAsyncMessageReader messageEnumerator = await context.Response.GetResponseDataAsync(session.CancellationToken);

                //Load inital segment
                if (await messageEnumerator.MoveNextAsync() && !session.CancellationToken.IsCancellationRequested)
                {
                    ValueTask sendTask;

                    //Syncrhonize access to send data because we may need to stream data to the client
                    await session.ResponseLock.WaitAsync(SEND_SEMAPHORE_TIMEOUT_MS);
                    try
                    {
                        do
                        {
                            bool eof = !messageEnumerator.DataRemaining;
                            
                            //Send first segment
                            sendTask = session.Socket.SendAsync(messageEnumerator.Current, WebSocketMessageType.Binary, eof);

                            /* 
                             * WARNING!
                             * this code relies on the managed websocket impl that the websocket will read 
                             * the entire buffer before returning. If this is not the case, this code will
                             * overwrite the memory buffer on the next call to move next.
                             */

                            //Move to next segment
                            if (!await messageEnumerator.MoveNextAsync())
                            {
                                break;
                            }
                            
                            //Await previous send
                            await sendTask;

                        } while (true);
                    }
                    finally
                    {
                        //release semaphore
                        session.ResponseLock.Release();
                    }

                    await sendTask;
                }

                //No data to send
            }
            catch (Exception ex)
            {
                OnProcessError?.Invoke(this, ex);
            }
            finally
            {
                session.ReturnContext(context);
            }
        }

        /// <summary>
        /// Processes an out-of-band request message (internal communications)
        /// </summary>
        /// <param name="outOfBandContext">The <see cref="FBMContext"/> containing the OOB message</param>
        /// <returns>A <see cref="Task"/> that completes when the operation completes</returns>
        protected virtual Task ProcessOOBAsync(FBMContext outOfBandContext)
        {
            return Task.CompletedTask;
        }
    }
}
