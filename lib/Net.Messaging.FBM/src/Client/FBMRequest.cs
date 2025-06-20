/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMRequest.cs 
*
* FBMRequest.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Text;
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Client
{

    /// <summary>
    /// <para>
    /// A reusable Fixed Buffer Message request container. This class is not thread-safe
    /// </para>
    /// <para>
    /// The internal buffer is used for storing headers, body data (unless streaming)
    /// </para>
    /// </summary>
    public sealed class FBMRequest : VnDisposeable, IReusable, IFBMMessage, IStringSerializeable
    {
        /*
         * Important impl notes.
         * 
         * In order to conserve memory and types, the FBMRequest stores all state information
         * and memory required for an FBM transaction. That is, the request headers, the 
         * message waiting state (the wait handles for async/await), and the response message
         * headers and body.
         * 
         * Okay, the buffer is used for 3 purposes. 
         *      - Store request headers
         *      - Store request body if not streaming
         *      - Store response headers once message has been sent
         * 
         * Since a request is no longer needed when a response is received, it's buffer is used 
         * to store response header data. (it becomes tri-use).
         * 
         * During response header parsing, FBMMessageHeader structures are stored in the 
         * ResponseHeaderList field that are simply pointers to consecutive memory locations
         * in the buffer. This is done to avoid allocating multiple memory segments for each 
         * header key-value pair, and internal copy overhead. 
         */


        private readonly FBMReusableRequestStream _buffer;
        private readonly Encoding HeaderEncoding;

        /*
         * Local list stores processed headers for response messages
         * which are structures and will be allocted in the list.
         * FBMMessagesHeader's are essentially pointers to locations
         * in the reused buffer (in response "mode") cast to a 
         * character buffer.
         */
        private readonly List<FBMMessageHeader> ResponseHeaderList = [];


        /// <summary>
        /// The size (in bytes) of the request message
        /// </summary>
        public int Length => _buffer.AccumulatedSize;

        /// <summary>
        /// The id of the current request message
        /// </summary>
        public int MessageId { get; }
    
        /// <summary>
        /// Gets the request message waiter
        /// </summary>
        internal IFBMMessageWaiter Waiter { get; }

        internal VnMemoryStream? Response { get; private set; }

        /// <summary>
        /// Initializes a new <see cref="FBMRequest"/> with the sepcified message buffer size,
        /// and a random messageid
        /// </summary>
        /// <param name="config">The fbm client config storing required config variables</param>
        public FBMRequest(ref readonly FBMClientConfig config) : this(Helpers.RandomMessageId, in config)
        { }

        /// <summary>
        /// Initializes a new <see cref="FBMRequest"/> with the sepcified message buffer size and a custom MessageId
        /// </summary>
        /// <param name="messageId">The custom message id</param>
        /// <param name="config">The fbm client config storing required config variables</param>
        public FBMRequest(int messageId, ref readonly FBMClientConfig config)
            :this(messageId, config.MemoryManager, config.MessageBufferSize, config.HeaderEncoding)
        { }

        /// <summary>
        /// Initializes a new <see cref="FBMRequest"/> with the sepcified message buffer size and a custom MessageId
        /// </summary>
        /// <param name="messageId">The custom message id</param>
        /// <param name="manager">The memory manager used to allocate the internal buffers</param>
        /// <param name="bufferSize">The size of the internal buffer</param>
        /// <param name="headerEncoding">The encoding instance used for header character encoding</param>
        public FBMRequest(int messageId, IFBMMemoryManager manager, int bufferSize, Encoding headerEncoding)
        {
            MessageId = messageId;
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(headerEncoding);

            HeaderEncoding = headerEncoding;

            //Configure waiter
            Waiter = new FBMMessageWaiter(this);
           
            _buffer = new(manager, bufferSize);

            //Prepare the message incase the request is fresh
            _buffer.Prepare();
            Reset();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteHeader(HeaderCommand header, ReadOnlySpan<char> value) 
            => WriteHeader((byte)header, value);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteHeader(byte header, ReadOnlySpan<char> value) 
            => Helpers.WriteHeader(_buffer, header, value, Helpers.DefaultEncoding);

        ///<inheritdoc/>
        public void WriteBody(ReadOnlySpan<byte> body, ContentType contentType = ContentType.Binary)
        {
            //Write content type header
            WriteHeader(HeaderCommand.ContentType, HttpHelpers.GetContentTypeString(contentType));

            // Writing the message body directly to the stream is the most
            // efficient
            _buffer.Write(body);
        }

        /// <summary>
        /// Returns buffer writer for writing the body data to the internal message buffer
        /// </summary>
        /// <returns>A <see cref="IBufferWriter{T}"/> to write message body to</returns>
        /// <remarks>Calling this method ends the headers section of the request</remarks>
        public IBufferWriter<byte> GetBodyWriter()
        {
            //Write the trailing termination header to the stream
            Helpers.WriteTermination(_buffer);
            return _buffer;
        }

        /// <summary>
        /// Gets a specialized stream of the request message body, this stream is used to 
        /// write the body data to the request
        /// </summary>
        /// <returns>A <see cref="Stream"/> to write the request body to</returns>
        public Stream GetBodyStream()
        {
            _ = GetBodyWriter(); //Ensure the body writer is initialized and headers are ended

            //Return the stream for writing
            return _buffer;
        }

        /// <summary>
        /// The request message packet, this may cause side effects
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetRequestData() => _buffer.GetWrittenMemory();

        /// <summary>
        /// Resets the internal buffer and allows for writing a new message with
        /// the same message-id
        /// </summary>
        public void Reset()
        {
            //Reset request header accumulator when complete
            _buffer.Reset();

            //Write message id to accumulator, it should already be reset
            Helpers.WriteMessageid(_buffer, MessageId);
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            ResponseHeaderList.Clear();

            Response?.Dispose();

            // Calling release multiple times is safe, and will release any 
            // held resources
            _buffer.Release();            

            //Dispose waiter
            (Waiter as FBMMessageWaiter)!.Dispose();
        }

        void IReusable.Prepare()
        {
            //MUST BE CALLED FIRST!
            _buffer.Prepare();
            Reset();
        }

        bool IReusable.Release()
        {
            //Make sure response header list is clear
            ResponseHeaderList.Clear();

            //Clear old response data if error occured
            Response?.Dispose();
            Response = null;

            //Free buffer
            return _buffer.Release();
        }

        #region Response 
        
        /// <summary>
        /// Gets the response of the sent message
        /// </summary>
        /// <returns>The response message for the current request</returns>
        internal FBMResponse GetResponse()
        {
            if (Response == null)
            {
                return new();
            }

            /*
             * NOTICE
             * 
             * The FBM Client will position the response stream to the start 
             * of the header section (missing the message-id header)
             * 
             * The message id belongs to this request so it cannot be mismatched
             * 
             * The headers are read into a list of key-value pairs and the stream
             * is positioned to the start of the message body
             */

            //Parse message headers
            HeaderParseError statusFlags = Helpers.ParseHeaders(Response, _buffer, ResponseHeaderList, HeaderEncoding);

            //return response structure
            return new(Response, statusFlags, ResponseHeaderList);
        }

        #endregion


        #region Diagnostics

        ///<inheritdoc/>
        public string Compile()
        {
            ReadOnlyMemory<byte> requestData = GetRequestData();

            int charSize = Helpers.DefaultEncoding.GetCharCount(requestData.Span);
            
            using UnsafeMemoryHandle<char> buffer = MemoryUtil.UnsafeAlloc<char>(charSize + 128);
            
            ERRNO count = Compile(buffer.Span);
            
            return buffer.AsSpan(0, count).ToString();
        }

        ///<inheritdoc/>
        public void Compile(ref ForwardOnlyWriter<char> writer)
        {
            ReadOnlyMemory<byte> requestData = GetRequestData();
            writer.AppendSmall("Message ID:");
            writer.Append(MessageId);
            writer.AppendSmall(Environment.NewLine);
            Helpers.DefaultEncoding.GetChars(requestData.Span, ref writer);
        }

        ///<inheritdoc/>
        public ERRNO Compile(Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }

        ///<inheritdoc/>
        public override string ToString() => Compile();

        #endregion

        #region waiter
        private sealed class FBMMessageWaiter : IFBMMessageWaiter, IDisposable, IThreadPoolWorkItem
        {
            private readonly Timer _timer;
            private readonly FBMRequest _request;

            private TaskCompletionSource? _tcs;
            private CancellationTokenRegistration _token;

            public FBMMessageWaiter(FBMRequest request)
            {
                _request = request;

                _timer = new(OnTimeout, this, Timeout.Infinite, Timeout.Infinite);
            }

            ///<inheritdoc/>
            public void OnBeginRequest() => _tcs = new(TaskCreationOptions.None);

            ///<inheritdoc/>
            public void OnEndRequest()
            {
                //Cleanup tcs ref
                _tcs = null;

                //Always stop timer if set
                _timer.Stop();

                //Cleanup cancellation token
                _token.Dispose();
            }

            ///<inheritdoc/>
            public bool Complete(VnMemoryStream ms)
            {
                TaskCompletionSource? tcs = _tcs;

                //Work is done/cancelled
                if (tcs != null && tcs.Task.IsCompleted)
                {
                    return false;
                }

                //Store response
                _request.Response = ms;

                /*
                 * The calling thread may be a TP thread proccessing an async event loop.
                 * We do not want to block this worker thread.
                 */
                return ThreadPool.UnsafeQueueUserWorkItem(this, true);
            }

            /*
             * Called when scheduled on the TP thread pool
             */
            ///<inheritdoc/>
            public void Execute() => _tcs?.TrySetResult();

            
            ///<inheritdoc/>
            public Task GetTask(TimeSpan timeout, CancellationToken cancellation)
            {
                TaskCompletionSource? tcs = _tcs;

                Debug.Assert(tcs != null, "A call to GetTask was made outside of the request flow, the TaskCompletionSource was null");

                /*
                 * Get task will only be called after the message has been sent.
                 * The Complete method may have already scheduled a completion by 
                 * the time this method is called, so we may avoid setting up the 
                 * timer and cancellation if possible. Also since this mthod is 
                 * called from the request side, we know the tcs cannot be null
                 */

                if (!tcs.Task.IsCompleted)
                {
                    if (timeout.Ticks > 0)
                    {
                        //Restart timer if timeout is configured
                        _timer.Restart(timeout);
                    }

                    if (cancellation.CanBeCanceled)
                    {
                        //Register cancellation
                        _token = cancellation.Register(OnCancelled, null, false);
                    }
                }

                return tcs.Task;
            }

            ///<inheritdoc/>
            public void ManualCancellation() => OnCancelled(null);

            //Set cancelled state if exists, the task may have already completed
            private void OnCancelled(object? state) => _tcs?.TrySetCanceled();

            private void OnTimeout(object? state)
            {
               TimeoutException to = new("A response was not received in the desired timeout period. Operation aborted");
                _tcs?.TrySetException(to);
            }

            ///<inheritdoc/>
            public void Dispose()
            {
                _timer.Dispose();
                _token.Dispose();
            }
        }

        #endregion
    }
}
