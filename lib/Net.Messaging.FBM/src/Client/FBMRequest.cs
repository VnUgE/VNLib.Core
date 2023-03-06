/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Text;
using System.Buffers;
using System.Threading;
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
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly FBMBuffer Buffer;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly Encoding HeaderEncoding;

        /*
         * Local list stores processed headers for response messages
         * which are structures and will be allocted in the list.
         * FBMMessagesHeader's are essentially pointers to locations
         * in the reused buffer (in response "mode") cast to a 
         * character buffer.
         */
        private readonly List<FBMMessageHeader> ResponseHeaderList = new();


        /// <summary>
        /// The size (in bytes) of the request message
        /// </summary>
        public int Length => Buffer.RequestBuffer.AccumulatedSize;

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
        public FBMRequest(in FBMClientConfig config) : this(Helpers.RandomMessageId, in config)
        { }

        /// <summary>
        /// Initializes a new <see cref="FBMRequest"/> with the sepcified message buffer size and a custom MessageId
        /// </summary>
        /// <param name="messageId">The custom message id</param>
        /// <param name="config">The fbm client config storing required config variables</param>
        public FBMRequest(int messageId, in FBMClientConfig config)
        :this(messageId, config.BufferHeap, config.MessageBufferSize, config.HeaderEncoding)
        { }

        /// <summary>
        /// Initializes a new <see cref="FBMRequest"/> with the sepcified message buffer size and a custom MessageId
        /// </summary>
        /// <param name="messageId">The custom message id</param>
        /// <param name="heap">The heap to allocate the internal buffer from</param>
        /// <param name="bufferSize">The size of the internal buffer</param>
        /// <param name="headerEncoding">The encoding instance used for header character encoding</param>
        public FBMRequest(int messageId, IUnmangedHeap heap, int bufferSize, Encoding headerEncoding)
        {
            MessageId = messageId;
            HeaderEncoding = headerEncoding;

            //Configure waiter
            Waiter = new FBMMessageWaiter(this);

            //Alloc the buffer as a memory owner so a memory buffer can be used
            IMemoryOwner<byte> HeapBuffer = heap.DirectAlloc<byte>(bufferSize);
            Buffer = new(HeapBuffer);

            //Prepare the message incase the request is fresh
            Reset();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteHeader(HeaderCommand header, ReadOnlySpan<char> value) => WriteHeader((byte)header, value);
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteHeader(byte header, ReadOnlySpan<char> value) => Helpers.WriteHeader(Buffer.RequestBuffer, header, value, Helpers.DefaultEncoding);
        ///<inheritdoc/>
        public void WriteBody(ReadOnlySpan<byte> body, ContentType contentType = ContentType.Binary)
        {
            //Write content type header
            WriteHeader(HeaderCommand.ContentType, HttpHelpers.GetContentTypeString(contentType));
            //Now safe to write body
            Helpers.WriteBody(Buffer.RequestBuffer, body);
        }

        /// <summary>
        /// Returns buffer writer for writing the body data to the internal message buffer
        /// </summary>
        /// <returns>A <see cref="IBufferWriter{T}"/> to write message body to</returns>
        /// <remarks>Calling this method ends the headers section of the request</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IBufferWriter<byte> GetBodyWriter() => Buffer.GetBodyWriter();

        /// <summary>
        /// The request message packet, this may cause side effects
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> GetRequestData()
        {
            return Buffer.RequestData;
        }

        /// <summary>
        /// Resets the internal buffer and allows for writing a new message with
        /// the same message-id
        /// </summary>
        public void Reset()
        {
            Buffer.Reset(MessageId);
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            Buffer.Dispose();
            Response?.Dispose();
            //Dispose waiter
            (Waiter as FBMMessageWaiter)!.Dispose();
        }

        void IReusable.Prepare() => Reset();

        bool IReusable.Release()
        {
            //Make sure response header list is clear
            ResponseHeaderList.Clear();

            //Clear old response data if error occured
            Response?.Dispose();
            Response = null;

            return true;
        }

        #region Response 
        
        /// <summary>
        /// Gets the response of the sent message
        /// </summary>
        /// <returns>The response message for the current request</returns>
        internal FBMResponse GetResponse()
        {
            if (Response != null)
            {
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
                HeaderParseError statusFlags = Helpers.ParseHeaders(Response, Buffer, ResponseHeaderList, HeaderEncoding);

                //return response structure
                return new(Response, statusFlags, ResponseHeaderList, OnResponseDisposed);
            }
            else
            {
                return new();
            }
        }

        //Called when a response message is disposed to cleanup resources held by the response
        private void OnResponseDisposed()
        {
            //Clear response header list
            ResponseHeaderList.Clear();

            //Clear old response
            Response?.Dispose();
            Response = null;
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
            writer.Append("Message ID:");
            writer.Append(MessageId);
            writer.Append(Environment.NewLine);
            Helpers.DefaultEncoding.GetChars(requestData.Span, ref writer);
        }
        ///<inheritdoc/>
        public ERRNO Compile(in Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }
        ///<inheritdoc/>
        public override string ToString() => Compile();

        #endregion

        #region waiter
        private sealed class FBMMessageWaiter : IFBMMessageWaiter, IDisposable
        {
            private readonly Timer _timer;
            private readonly FBMRequest _request;

            private TaskCompletionSource? _tcs;

            public FBMMessageWaiter(FBMRequest request)
            {
                _request = request;

                //Configure timer
                _timer = new(OnCancelled, this, Timeout.Infinite, Timeout.Infinite);
            }

            ///<inheritdoc/>
            public void OnBeginRequest()
            {
                //Configure new tcs
                _tcs = new(TaskCreationOptions.None);
            }

            ///<inheritdoc/>
            public void OnEndRequest()
            {
                //Cleanup tcs ref
                _ = Interlocked.Exchange(ref _tcs, null);

                //Stop timer if set
                _timer.Stop();
            }

            ///<inheritdoc/>
            public void Complete(VnMemoryStream ms)
            {
                //Read the current state of the tcs
                TaskCompletionSource? tcs = _tcs;

                if (tcs == null)
                {
                    //Work is done/cancelled, dispose the ms and leave
                    ms.Dispose();
                }

                //Store response
                _request.Response = ms;

                //Transition to completed state in background thread
                static void OnTpCallback(object? state)
                {
                    _ = (state as TaskCompletionSource)!.TrySetResult();
                }

                /*
                 * The calling thread may be a TP thread proccessing an async event loop.
                 * We do not want to block this worker thread.
                 */
                ThreadPool.UnsafeQueueUserWorkItem(OnTpCallback, tcs);
            }

            ///<inheritdoc/>
            public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellation)
            {
                if (timeout.Ticks > 0)
                {
                    //Restart timer if timeout is configured
                    _timer.Restart(timeout);
                }

                //Confim the token may be cancelled
                if (cancellation.CanBeCanceled)
                {
                    //Register cancellation
                    using CancellationTokenRegistration reg = cancellation.Register(OnCancelled, this, false);

                    //await task that may be canclled
                    await _tcs.Task.ConfigureAwait(false);
                }
                else
                {
                    //await the task directly
                    await _tcs.Task.ConfigureAwait(false);
                }
            }

            ///<inheritdoc/>
            public void ManualCancellation() => OnCancelled(this);

            private void OnCancelled(object? state)
            {
                //Set cancelled state if exists
                _ = _tcs?.TrySetCanceled();
            }

            ///<inheritdoc/>
            public void Dispose()
            {
                _timer.Dispose();
            }
        }

        #endregion
    }
}
