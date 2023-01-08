/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;

namespace VNLib.Net.Http.Core
{
    internal partial class HttpResponse : IHttpLifeCycle
    {
        private readonly HashSet<HttpCookie> Cookies;
        private readonly HeaderDataAccumulator Writer;
        
        private readonly DirectStream ReusableDirectStream;
        private readonly ChunkedStream ReusableChunkedStream;
        private readonly Func<Stream> _getStream;
        private readonly Encoding ResponseEncoding;
        private readonly Func<HttpVersion> GetVersion;

        private bool HeadersSent;
        private bool HeadersBegun;
      
        private HttpStatusCode _code;

        /// <summary>
        /// Response header collection
        /// </summary>
        public VnWebHeaderCollection Headers { get; }

        public HttpResponse(Encoding encoding, int headerBufferSize, int chunkedBufferSize, Func<Stream> getStream, Func<HttpVersion> getVersion)
        {
            //Initialize a new header collection and a cookie jar
            Headers = new();
            Cookies = new();
            //Create a new reusable writer stream 
            Writer = new(headerBufferSize);

            _getStream = getStream;
            ResponseEncoding = encoding;
            
            //Create a new chunked stream
            ReusableChunkedStream = new(encoding, chunkedBufferSize, getStream);
            ReusableDirectStream = new();
            GetVersion = getVersion;
        }


        /// <summary>
        /// Sets the status code of the response
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
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
        /// Allows sending an early 100-Continue status message to the client
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        internal async Task SendEarly100ContinueAsync()
        {
            Check();
            //Send a status message with the continue response status
            Writer.WriteLine(HttpHelpers.GetResponseString(GetVersion(), HttpStatusCode.Continue));
            //Trailing crlf
            Writer.WriteLine();
            //get base stream
            Stream bs = _getStream();
            //Flush writer to stream (will reset the buffer)
            Writer.Flush(ResponseEncoding, bs);
            //Flush the base stream
            await bs.FlushAsync();
        }

        /// <summary>
        /// Sends the status message and all available headers to the client. 
        /// Headers set after method returns will be sent when output stream is requested or scope exits
        /// </summary>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void FlushHeaders()
        {
            Check();
            //If headers havent been sent yet, start with status code
            if (!HeadersBegun)
            {
                //write status code first
                Writer.WriteLine(HttpHelpers.GetResponseString(GetVersion(), _code));

                //Write the date to header buffer
                Writer.Append("Date: ");
                Writer.Append(DateTimeOffset.UtcNow, "R");
                Writer.WriteLine();
                //Set begun flag
                HeadersBegun = true;
            }
            //Write headers
            for (int i = 0; i < Headers.Count; i++)
            {
                Writer.Append(Headers.Keys[i]);     //Write header key
                Writer.Append(": ");            //Write separator
                Writer.WriteLine(Headers[i]);       //Write the header value
            }
            //Remove writen headers
            Headers.Clear();
            //Write cookies if any are set
            if (Cookies.Count > 0)
            {
                //Write cookies if any have been set
                foreach (HttpCookie cookie in Cookies)
                {
                    Writer.Append("Set-Cookie: ");
                    Writer.Append(in cookie);
                    Writer.WriteLine();
                }
                //Clear all current cookies
                Cookies.Clear();
            }
        }
        private void EndFlushHeaders(Stream transport)
        {
            //Sent all available headers
            FlushHeaders();
            //Last line to end headers
            Writer.WriteLine();
            
            //Flush writer
            Writer.Flush(ResponseEncoding, transport);
            //Update sent headers
            HeadersSent = true;
        }

        /// <summary>
        /// Gets a stream for writing data of a specified length directly to the client
        /// </summary>
        /// <param name="ContentLength"></param>
        /// <returns>A <see cref="Stream"/> configured for writing data to client</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public Stream GetStream(long ContentLength)
        {
            Check();
            //Add content length header
            Headers[HttpResponseHeader.ContentLength] = ContentLength.ToString();
            //End sending headers so the user can write to the ouput stream
            Stream transport = _getStream();
            EndFlushHeaders(transport);

            //Init direct stream
            ReusableDirectStream.Prepare(transport);

            //Return the direct stream
            return ReusableDirectStream;
        }

        /// <summary>
        /// Sets up the client for chuncked encoding and gets a stream that allows for chuncks to be sent. User must call dispose on stream when done writing data
        /// </summary>
        /// <returns><see cref="Stream"/> supporting chunked encoding</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public Stream GetStream()
        {
#if DEBUG
            if (GetVersion() != HttpVersion.Http11)
            {
                throw new InvalidOperationException("Chunked transfer encoding is not acceptable for this http version");
            }
#endif
            Check();
            //Set encoding type to chunked with user-defined compression
            Headers[HttpResponseHeader.TransferEncoding] = "chunked";
            //End sending headers so the user can write to the ouput stream
            Stream transport = _getStream();
            EndFlushHeaders(transport);
            
            //Return the reusable stream
            return ReusableChunkedStream;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Check()
        {
            if (HeadersSent)
            {
                throw new InvalidOperationException("Headers have already been sent!");
            }
        }
       
        /// <summary>
        /// Finalzies the response to a client by sending all available headers if 
        /// they have not been sent yet
        /// </summary>
        /// <exception cref="OutOfMemoryException"></exception>
        internal async ValueTask CloseAsync()
        {
            //If headers havent been sent yet, send them and there must be no content
            if (!HeadersBegun)
            {
                //RFC 7230, length only set on 200 + but not 204
                if ((int)_code >= 200 && (int)_code != 204)
                {
                    //If headers havent been sent by this stage there is no content, so set length to 0
                    Headers[HttpResponseHeader.ContentLength] = "0";
                }
                //Flush transport
                Stream transport = _getStream();
                EndFlushHeaders(transport);
                //Flush transport
                await transport.FlushAsync();
            }
            //Headers have been started but not finished yet
            else if (!HeadersSent)
            {
                //RFC 7230, length only set on 200 + but not 204
                if ((int)_code >= 200 && (int)_code != 204)
                {
                    //If headers havent been sent by this stage there is no content, so set length to 0
                    Headers[HttpResponseHeader.ContentLength] = "0";
                }
                //If headers arent done sending yet, conclude headers
                Stream transport = _getStream();
                EndFlushHeaders(transport);
                //Flush transport
                await transport.FlushAsync();
            }
        }

     
        public void OnPrepare()
        {
            //Propagate all child lifecycle hooks
            Writer.OnPrepare();
            ReusableChunkedStream.OnPrepare();
        }

        public void OnRelease()
        {
            Writer.OnRelease();
            ReusableChunkedStream.OnRelease();
        }

        public void OnNewRequest()
        {
            //Default to okay status code
            _code = HttpStatusCode.OK;
            
            Writer.OnNewRequest();
            ReusableChunkedStream.OnNewRequest();
        }

        public void OnComplete()
        {
            //Clear headers and cookies
            Headers.Clear();
            Cookies.Clear();
            //Reset status values
            HeadersBegun = false;
            HeadersSent = false;

            //Call child lifecycle hooks
            Writer.OnComplete();
            ReusableChunkedStream.OnComplete();
        }
    }
}