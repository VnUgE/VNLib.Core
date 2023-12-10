/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpResponse.cs 
*
* HttpResponse.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core.Response
{
    internal sealed class HttpResponse : IHttpLifeCycle
#if DEBUG
        , IStringSerializeable
#endif
    {
        private readonly IHttpContextInformation ContextInfo;
        private readonly HashSet<HttpCookie> Cookies;
        private readonly DirectStream ReusableDirectStream;
        private readonly ChunkedStream ReusableChunkedStream;
        private readonly HeaderDataAccumulator Writer;

        private int _headerWriterPosition;

        private bool HeadersSent;
        private bool HeadersBegun;

        private HttpStatusCode _code;

        /// <summary>
        /// Response header collection
        /// </summary>
        public VnWebHeaderCollection Headers { get; }

        /// <summary>
        /// The current http status code value
        /// </summary>
        internal HttpStatusCode StatusCode => _code;

        public HttpResponse(IHttpContextInformation ctx, IHttpBufferManager manager)
        {
            ContextInfo = ctx;

            //Initialize a new header collection and a cookie jar
            Headers = new();
            Cookies = new();

            //Init header accumulator
            Writer = new(manager.ResponseHeaderBuffer, ContextInfo);

            //Create a new chunked stream
            ReusableChunkedStream = new(manager.ChunkAccumulatorBuffer, ContextInfo);
            ReusableDirectStream = new();
        }

        /// <summary>
        /// Sets the status code of the response
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetStatusCode(HttpStatusCode code)
        {
            if (HeadersBegun)
            {
                throw new InvalidOperationException("Status code has already been sent");
            }

            _code = code;
        }

        /// <summary>
        /// Adds a new http-cookie to the collection
        /// </summary>
        /// <param name="cookie">Cookie to add</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddCookie(HttpCookie cookie) => Cookies.Add(cookie);

        /// <summary>
        /// Compiles and flushes all headers to the header accumulator ready for sending
        /// </summary>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void FlushHeaders()
        {
            Check();

            //Get a fresh writer to buffer character data
            ForwardOnlyWriter<char> writer = Writer.GetWriter();

            //If headers havent been sent yet, start with status line
            if (!HeadersBegun)
            {
                //write status code first
                writer.Append(HttpHelpers.GetResponseString(ContextInfo.CurrentVersion, _code));
                writer.Append(HttpHelpers.CRLF);

                //Write the date to header buffer
                writer.Append("Date: ");
                writer.Append(DateTimeOffset.UtcNow, "R");
                writer.Append(HttpHelpers.CRLF);

                //Set begun flag
                HeadersBegun = true;
            }

            //Write headers
            for (int i = 0; i < Headers.Count; i++)
            {
                writer.Append(Headers.Keys[i]);     //Write header key
                writer.Append(": ");           //Write separator
                writer.Append(Headers[i]);          //Write the header value
                writer.Append(HttpHelpers.CRLF);    //Crlf
            }

            //Remove writen headers
            Headers.Clear();

            //Write cookies if any are set
            if (Cookies.Count > 0)
            {
                //Enumerate and write
                foreach (HttpCookie cookie in Cookies)
                {
                    writer.Append("Set-Cookie: ");

                    //Write the cookie to the header buffer
                    cookie.Compile(ref writer);

                    writer.Append(HttpHelpers.CRLF);
                }

                //Clear all current cookies
                Cookies.Clear();
            }

            //Commit headers
            Writer.CommitChars(ref writer, ref _headerWriterPosition);
        }

        private ValueTask EndFlushHeadersAsync()
        {
            //Sent all available headers
            FlushHeaders();

            //Last line to end headers
            Writer.WriteTermination(ref _headerWriterPosition);

            //Get the response data header block
            Memory<byte> responseBlock = Writer.GetResponseData(_headerWriterPosition);

            //Update sent headers
            HeadersSent = true;

            //Get the transport stream to write the response data to
            Stream transport = ContextInfo.GetTransport();

            /*
             * ASYNC NOTICE: It is safe to get the memory block then return the task
             * because the response writer is not cleaned up until the OnComplete()
             * method, so the memory block is valid until then.
             */

            //Write the response data to the base stream
            return responseBlock.IsEmpty ? ValueTask.CompletedTask : transport.WriteAsync(responseBlock);
        }

        /// <summary>
        /// Flushes all available headers to the transport asynchronously
        /// </summary>
        /// <param name="contentLength">The optional content length if set, <![CDATA[ < 0]]> for chunked responses</param>
        /// <returns>A value task that completes when header data has been made available to the transport</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask CompleteHeadersAsync(long contentLength)
        {
            Check();

            if (contentLength < 0)
            {
                //Add chunked header
                Headers.Set(HttpResponseHeader.TransferEncoding, "chunked");

            }
            else
            {
                //Add content length header
                Headers.Set(HttpResponseHeader.ContentLength, contentLength.ToString());
            }

            //Flush headers
            return EndFlushHeadersAsync();
        }

        /// <summary>
        /// Gets a response writer for writing directly to the transport stream
        /// </summary>
        /// <returns>The <see cref="IDirectResponsWriter"/> instance for writing stream data to</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDirectResponsWriter GetDirectStream()
        {
            //Headers must be sent before getting a direct stream
            Debug.Assert(HeadersSent, "A call to stream capture was made before the headers were flushed to the transport");
            return ReusableDirectStream;
        }

        /// <summary>
        /// Gets a response writer for writing chunked data to the transport stream
        /// </summary>
        /// <returns>The <see cref="IResponseDataWriter"/> for buffering response chunks</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IResponseDataWriter GetChunkWriter()
        {
            //Chunking is only an http 1.1 feature (should never get called otherwise)
            Debug.Assert(ContextInfo.CurrentVersion == HttpVersion.Http11, "Chunked response handler was requested, but is not an HTTP/1.1 response");
            Debug.Assert(HeadersSent, "Chunk write was requested but header data has not been sent");

            return ReusableChunkedStream;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Check()
        {
            if (HeadersSent)
            {
                throw new InvalidOperationException("Headers have already been sent!");
            }
        }

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

        /// <summary>
        /// Allows sending an early 100-Continue status message to the client
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        internal async Task SendEarly100ContinueAsync()
        {
            Check();

            //Send a status message with the continue response status
            Writer.WriteToken(HttpHelpers.GetResponseString(ContextInfo.CurrentVersion, HttpStatusCode.Continue), ref _headerWriterPosition);

            //Trailing crlf
            Writer.WriteTermination(ref _headerWriterPosition);

            //Get the response data header block
            Memory<byte> responseBlock = Writer.GetResponseData(_headerWriterPosition);

            //reset after getting the written buffer
            _headerWriterPosition = 0;

            //get base stream
            Stream bs = ContextInfo.GetTransport();

            //Write the response data to the base stream
            await bs.WriteAsync(responseBlock);

            //Flush the base stream to send the data to the client
            await bs.FlushAsync();
        }

        /// <summary>
        /// Finalzies the response to a client by sending all available headers if 
        /// they have not been sent yet
        /// </summary>
        /// <exception cref="OutOfMemoryException"></exception>
        internal ValueTask CloseAsync()
        {
            //If headers haven't been sent yet, send them and there must be no content
            if (!HeadersSent)
            {
                //Finalize headers
                return EndFlushHeadersAsync();
            }

            return ValueTask.CompletedTask;
        }

#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

        ///<inheritdoc/>
        public void OnPrepare()
        { }

        ///<inheritdoc/>
        public void OnRelease()
        {
            ReusableChunkedStream.OnRelease();
            ReusableDirectStream.OnRelease();
        }

        ///<inheritdoc/>
        public void OnNewConnection()
        {
            //Get the transport stream and init streams
            Stream transport = ContextInfo.GetTransport();
            ReusableChunkedStream.OnNewConnection(transport);
            ReusableDirectStream.OnNewConnection(transport);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnNewRequest()
        {
            //Default to okay status code
            _code = HttpStatusCode.OK;

            //Set new header writer on every new request
            _headerWriterPosition = 0;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnComplete()
        {
            //Clear headers and cookies
            Headers.Clear();
            Cookies.Clear();
            //Reset status values
            HeadersBegun = false;
            HeadersSent = false;

            //clear header writer
            _headerWriterPosition = 0;

            //Call child lifecycle hooks
            ReusableChunkedStream.OnComplete();
        }

        private sealed class DirectStream : ReusableResponseStream, IDirectResponsWriter
        {
            ///<inheritdoc/>
            public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer) => transport!.WriteAsync(buffer);
        }

        /// <summary>
        /// Writes chunked HTTP message bodies to an underlying streamwriter 
        /// </summary>
        private sealed class ChunkedStream : ReusableResponseStream, IResponseDataWriter
        {
            private readonly ChunkDataAccumulator _chunkAccumulator;

            /*
             * Tracks the number of bytes accumulated in the 
             * current chunk.
             */
            private int _accumulatedBytes;

            public ChunkedStream(IChunkAccumulatorBuffer buffer, IHttpContextInformation context)
                => _chunkAccumulator = new(buffer, context);

            #region Hooks

            ///<inheritdoc/>
            public void OnComplete() => _accumulatedBytes = 0;

            ///<inheritdoc/>
            public Memory<byte> GetMemory() => _chunkAccumulator.GetRemainingSegment(_accumulatedBytes);

            ///<inheritdoc/>
            public int Advance(int written)
            {
                //Advance the accumulator
                _accumulatedBytes += written;
                return _chunkAccumulator.GetRemainingSegmentSize(_accumulatedBytes);
            }

            ///<inheritdoc/>
            public ValueTask FlushAsync(bool isFinal)
            {
                /*
                 * We need to know when the final chunk is being flushed so we can
                 * write the final termination sequence to the transport.
                 */

                Memory<byte> chunkData = isFinal ?
                    _chunkAccumulator.GetFinalChunkData(_accumulatedBytes) :
                    _chunkAccumulator.GetChunkData(_accumulatedBytes);

                //Reset accumulator
                _accumulatedBytes = 0;

                //Write remaining data to stream
                return transport!.WriteAsync(chunkData, CancellationToken.None);
            }

            #endregion
        }

#if DEBUG

        public override string ToString() => Compile();

        public string Compile()
        {
            //Alloc char buffer 
            using IMemoryHandle<char> buffer = MemoryUtil.SafeAlloc<char>(16 * 1024);

            //Writer
            ForwardOnlyWriter<char> writer = new(buffer.Span);
            Compile(ref writer);
            return writer.ToString();
        }

        public void Compile(ref ForwardOnlyWriter<char> writer)
        {
            /* READONLY!!! */

            //Status line
            writer.Append(HttpHelpers.GetResponseString(ContextInfo.CurrentVersion, _code));
            writer.Append(HttpHelpers.CRLF);
            writer.Append("Date: ");
            writer.Append(DateTimeOffset.UtcNow, "R");
            writer.Append(HttpHelpers.CRLF);

            //Write headers
            for (int i = 0; i < Headers.Count; i++)
            {
                writer.Append(Headers.Keys[i]);     //Write header key
                writer.Append(": ");           //Write separator
                writer.Append(Headers[i]);          //Write the header value
                writer.Append(HttpHelpers.CRLF);    //Crlf
            }

            //Enumerate and write
            foreach (HttpCookie cookie in Cookies)
            {
                writer.Append("Set-Cookie: ");

                //Write the cookie to the header buffer
                cookie.Compile(ref writer);

                writer.Append(HttpHelpers.CRLF);
            }

            //Last line to end headers
            writer.Append(HttpHelpers.CRLF);
        }

        public ERRNO Compile(Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }
#else

        public override string ToString() => "HTTP Library was compiled without a DEBUG directive, response logging is not available";

#endif

    }
}