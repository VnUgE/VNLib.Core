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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

using VNLib.Net.Http.Core.Buffering;
using VNLib.Net.Http.Core.Response;

namespace VNLib.Net.Http.Core
{
    internal sealed class HttpResponse : IHttpLifeCycle
#if DEBUG
        ,IStringSerializeable
#endif
    {
        private readonly HashSet<HttpCookie> Cookies;
        private readonly HeaderDataAccumulator Writer;
        
        private readonly DirectStream ReusableDirectStream;
        private readonly ChunkedStream ReusableChunkedStream;
        private readonly IHttpContextInformation ContextInfo;

        private bool HeadersSent;
        private bool HeadersBegun;
      
        private HttpStatusCode _code;

        /// <summary>
        /// Response header collection
        /// </summary>
        public VnWebHeaderCollection Headers { get; }

        public HttpResponse(IHttpBufferManager manager, IHttpContextInformation ctx)
        {
            ContextInfo = ctx;

            //Initialize a new header collection and a cookie jar
            Headers = new();
            Cookies = new();

            //Create a new reusable writer stream 
            Writer = new(manager.ResponseHeaderBuffer, ctx);
            
            //Create a new chunked stream
            ReusableChunkedStream = new(manager.ChunkAccumulatorBuffer, ctx);
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
            Writer.CommitChars(ref writer);
        }

        private ValueTask EndFlushHeadersAsync()
        {
            //Sent all available headers
            FlushHeaders();

            //Last line to end headers
            Writer.WriteTermination();

            //Get the response data header block
            Memory<byte> responseBlock = Writer.GetResponseData();

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
        /// Gets a stream for writing data of a specified length directly to the client
        /// </summary>
        /// <param name="ContentLength"></param>
        /// <returns>A <see cref="Stream"/> configured for writing data to client</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public ValueTask<Stream> GetStreamAsync(long ContentLength)
        {
            Check();

            //Add content length header
            Headers[HttpResponseHeader.ContentLength] = ContentLength.ToString();

            //Flush headers
            ValueTask flush = EndFlushHeadersAsync();

            //Return the reusable stream
            return flush.IsCompletedSuccessfully ?
                ValueTask.FromResult<Stream>(ReusableDirectStream)
                : GetStreamAsyncCore(flush, ReusableDirectStream);
        }

        /// <summary>
        /// Sets up the client for chuncked encoding and gets a stream that allows for chuncks to be sent. User must call dispose on stream when done writing data
        /// </summary>
        /// <returns><see cref="Stream"/> supporting chunked encoding</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public ValueTask<Stream> GetStreamAsync()
        {
            //Chunking is only an http 1.1 feature (should never get called otherwise)
            Debug.Assert(ContextInfo.CurrentVersion == HttpVersion.Http11);

            Check();

            //Set encoding type to chunked with user-defined compression
            Headers[HttpResponseHeader.TransferEncoding] = "chunked";

            //Flush headers
            ValueTask flush = EndFlushHeadersAsync();

            //Return the reusable stream
            return flush.IsCompletedSuccessfully ? 
                ValueTask.FromResult<Stream>(ReusableChunkedStream) 
                : GetStreamAsyncCore(flush, ReusableChunkedStream);
        }
        
        private static async ValueTask<Stream> GetStreamAsyncCore(ValueTask flush, Stream stream)
        {
            //Await the flush and get the stream
            await flush.ConfigureAwait(false);
            return stream;
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
            Writer.WriteToken(HttpHelpers.GetResponseString(ContextInfo.CurrentVersion, HttpStatusCode.Continue));

            //Trailing crlf
            Writer.WriteTermination();

            //Get the response data header block
            Memory<byte> responseBlock = Writer.GetResponseData();

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
                //RFC 7230, length only set on 200 + but not 204
                if ((int)_code >= 200 && (int)_code != 204)
                {
                    //If headers havent been sent by this stage there is no content, so set length to 0
                    Headers[HttpResponseHeader.ContentLength] = "0";
                }

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
        public void OnNewRequest()
        {
            //Default to okay status code
            _code = HttpStatusCode.OK;
            
            ReusableChunkedStream.OnNewRequest();
        }

        ///<inheritdoc/>
        public void OnComplete()
        {
            //Clear headers and cookies
            Headers.Clear();
            Cookies.Clear();
            //Reset status values
            HeadersBegun = false;
            HeadersSent = false;

            //Reset header writer
            Writer.Reset();

            //Call child lifecycle hooks
            ReusableChunkedStream.OnComplete();
        }

#if DEBUG

        public override string ToString() => Compile();

        public string Compile()
        {
            //Alloc char buffer 
            using IMemoryHandle<char> buffer = MemoryUtil.SafeAlloc<char>(16 * 1024);

            //Writer
            ForwardOnlyWriter<char> writer = new (buffer.Span);
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