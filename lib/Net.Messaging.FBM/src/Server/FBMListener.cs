/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;

using VNLib.Utils.IO;
using VNLib.Utils.Async;
using VNLib.Utils.Memory;
using VNLib.Utils.Memory.Caching;
using VNLib.Plugins.Essentials;

namespace VNLib.Net.Messaging.FBM.Server
{

    /// <summary>
    /// A FBM protocol listener. Listens for messages on a <see cref="WebSocketSession"/>
    /// and raises events on requests.
    /// </summary>
    public class FBMListener
    {     

        public const int SEND_SEMAPHORE_TIMEOUT_MS = 10 * 1000;

        private readonly IFBMMemoryManager MemoryManger;

        /// <summary>
        /// Creates a new <see cref="FBMListener"/> instance ready for 
        /// processing connections
        /// </summary>
        /// <param name="heap">The heap to alloc buffers from</param>
        /// <exception cref="ArgumentNullException"></exception>
        public FBMListener(IFBMMemoryManager heap) => MemoryManger = heap ?? throw new ArgumentNullException(nameof(heap));

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

        /// <summary>
        /// Begins listening for requests on the current websocket until 
        /// a close message is received or an error occurs
        /// </summary>
        /// <param name="wss">The <see cref="WebSocketSession"/> to receive messages on</param>
        /// <param name="handler">The callback method to handle incoming requests</param>
        /// <param name="args">The arguments used to configured this listening session</param>
        /// <returns>A <see cref="Task"/> that completes when the connection closes</returns>
        public async Task ListenAsync(WebSocketSession wss, IFBMServerMessageHandler handler, FBMListenerSessionParams args)
        {
            _ = wss ?? throw new ArgumentNullException(nameof(wss));
            _ = handler ?? throw new ArgumentNullException(nameof(handler));

            ListeningSession session = new(wss, handler, in args, MemoryManger);

            //Init new queue for dispatching work
            AsyncQueue<VnMemoryStream> workQueue = new(true, true);
            
            //Start a task to process the queue
            Task queueWorker = QueueWorkerDoWork(workQueue, session);

            //Alloc buffer
            IFBMMemoryHandle memHandle = MemoryManger.InitHandle();
            MemoryManger.AllocBuffer(memHandle, args.RecvBufferSize);

            try
            {
                if(!MemoryManger.TryGetHeap(out IUnmangedHeap? heap))
                {
                    throw new NotSupportedException("The memory manager must export an unmanaged heap");
                }

                Memory<byte> recvBuffer = memHandle.GetMemory();

                //Listen for incoming messages
                while (true)
                {
                    //Receive a message
                    ValueWebSocketReceiveResult result = await wss.ReceiveAsync(recvBuffer);
                    //If a close message has been received, we can gracefully exit
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        //Return close message
                        await wss.CloseSocketAsync(WebSocketCloseStatus.NormalClosure, "Goodbye");
                        //break listen loop
                        break;
                    }

                    //create buffer for storing data, pre alloc with initial data
                    VnMemoryStream request = new(heap, recvBuffer[..result.Count]);

                    //Streaming read
                    while (!result.EndOfMessage)
                    {
                        //Read more data
                        result = await wss.ReceiveAsync(recvBuffer);
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
                        request.Write(memHandle.GetSpan()[..result.Count]);
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
                    Exception cause = new FBMException($"Invalid messageid {context.Request.MessageId}, message length {data.Length}");
                    _ = session.Handler.OnInvalidMessage(context, cause);
                    return; //Cannot continue on invalid message id
                }
                
                //Check parse status flags
                if ((context.Request.ParseStatus & HeaderParseError.HeaderOutOfMem) > 0)
                {
                    Exception cause = new FBMException("Packet received with not enough space to store headers");
                    if(!session.Handler.OnInvalidMessage(context, cause))
                    {
                        return;
                    }
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
                    await session.Handler.HandleMessage(context, session.CancellationToken);
                }

                //Get response data reader
                await using IAsyncMessageReader messageEnumerator = context.Response.GetResponseData();

                //Load inital segment
                if (await messageEnumerator.MoveNextAsync() && !session.CancellationToken.IsCancellationRequested)
                {                  

                    //Syncrhonize access to send data because we may need to stream data to the client
                    await session.ResponseLock.WaitAsync(SEND_SEMAPHORE_TIMEOUT_MS);

                    try
                    {
                        do
                        {
                            //Send current segment
                            await session.Socket.SendAsync(messageEnumerator.Current, WebSocketMessageType.Binary, !messageEnumerator.DataRemaining);

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

                        } while (true);
                    }
                    finally
                    {
                        //release semaphore
                        session.ResponseLock.Release();
                    }
                }

                //No data to send
            }
            catch (Exception ex)
            {
                session.Handler.OnProcessError(ex);
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

#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

        private sealed class ListeningSession
        {
            private readonly ObjectRental<FBMContext> CtxStore;
            private readonly CancellationTokenSource Cancellation;
            private readonly CancellationTokenRegistration Registration;
            private readonly FBMListenerSessionParams Params;
            private readonly IFBMMemoryManager MemManager;


            public readonly SemaphoreSlim ResponseLock;

            public readonly WebSocketSession Socket;

            public readonly IFBMServerMessageHandler Handler;

            public CancellationToken CancellationToken => Cancellation.Token;

            public ListeningSession(WebSocketSession session, IFBMServerMessageHandler handler, in FBMListenerSessionParams args, IFBMMemoryManager memManager)
            {
                Params = args;
                Socket = session;
                Handler = handler;
                MemManager = memManager;

                //Create cancellation and register for session close
                Cancellation = new();
                Registration = session.Token.Register(Cancellation.Cancel);

                ResponseLock = new(1);
                CtxStore = ObjectRental.CreateReusable(ContextCtor);
            }

            private FBMContext ContextCtor() => new(Params.MaxHeaderBufferSize, Params.ResponseBufferSize, Params.HeaderEncoding, MemManager);

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
    }
}
