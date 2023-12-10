/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpRequestExtensions.cs 
*
* HttpRequestExtensions.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http.Core
{

    internal static class HttpRequestExtensions
    {
        /// <summary>
        /// Gets the <see cref="CompressionMethod"/> that the connection accepts
        /// in a default order, or none if not enabled or the server does not support it
        /// </summary>
        /// <param name="request"></param>
        /// <param name="serverSupported">The server supported methods</param>
        /// <returns>A <see cref="CompressionMethod"/> with a value the connection support</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompressionMethod GetCompressionSupport(this HttpRequest request, CompressionMethod serverSupported)
        {
            string? acceptEncoding = request.Headers[HttpRequestHeader.AcceptEncoding];

            /*
             * Priority order is gzip, deflate, br. Br is last for dynamic compression 
             * because of performace. We also need to make sure the server supports 
             * the desired compression method also.
             */

            if (acceptEncoding == null)
            {
                return CompressionMethod.None;
            }
            else if (serverSupported.HasFlag(CompressionMethod.Gzip) 
                && acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
            {
                return CompressionMethod.Gzip;
            }
            else if (serverSupported.HasFlag(CompressionMethod.Deflate) 
                && acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
            {
                return CompressionMethod.Deflate;
            }
            else if (serverSupported.HasFlag(CompressionMethod.Brotli) 
                && acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase))
            {
                return CompressionMethod.Brotli;
            }
            else
            {
                return CompressionMethod.None;
            }
        }
    

        /// <summary>
        /// Tests the connection's origin header against the location URL by authority. 
        /// An origin matches if its scheme, host, and port match
        /// </summary>
        /// <returns>true if the origin header was set and does not match the current locations origin</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCrossOrigin(this HttpRequest Request)
        {
            return Request.State.Origin != null
                && (!Request.State.Origin.Authority.Equals(Request.State.Location.Authority, StringComparison.Ordinal) 
                || !Request.State.Origin.Scheme.Equals(Request.State.Location.Scheme, StringComparison.Ordinal));
        }

        /// <summary>
        /// Is the current connection a websocket upgrade request handshake
        /// </summary>
        /// <returns>true if the connection is a websocket upgrade request, false otherwise</returns>
        public static bool IsWebSocketRequest(this HttpRequest Request)
        {
            string? upgrade = Request.Headers[HttpRequestHeader.Upgrade];

            if (!string.IsNullOrWhiteSpace(upgrade) && upgrade.Contains("websocket", StringComparison.OrdinalIgnoreCase))
            {
                //This request is a websocket request
                //Check connection header
                string? connection = Request.Headers[HttpRequestHeader.Connection];

                //Must be a web socket request
                return !string.IsNullOrWhiteSpace(connection) && connection.Contains("upgrade", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Initializes the <see cref="HttpRequest.RequestBody"/> for the current request
        /// </summary>
        /// <param name="context"></param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        internal static ValueTask InitRequestBodyAsync(this HttpContext context)
        {
            //Parse query
            ParseQueryArgs(context.Request);

            //Decode requests from body
            return !context.Request.State.HasEntityBody ? ValueTask.CompletedTask : ParseInputStream(context);
        }

        private static async ValueTask ParseInputStream(HttpContext context)
        {
            HttpRequest request = context.Request;
            IHttpContextInformation info = context;
            IHttpMemoryPool pool = context.ParentServer.Config.MemoryPool;

            //Gets the max form data buffer size to help calculate the initial char buffer size
            int maxBufferSize = context.ParentServer.Config.BufferConfig.FormDataBufferSize;

            //Calculate a largest available buffer to read the entire stream or up to the maximum buffer size
            int bufferSize = (int)Math.Min(request.InputStream.Length, maxBufferSize);

            //Get the form data buffer (should be cost free)
            Memory<byte> formBuffer = context.Buffers.GetFormDataBuffer();

            Debug.Assert(!formBuffer.IsEmpty, "GetFormDataBuffer() returned an empty memory buffer");

            switch (request.State.ContentType)
            {
                //CT not supported, dont read it
                case ContentType.NonSupported:
                    break;
                case ContentType.UrlEncoded:
                    {
                        //Alloc the form data character buffer, this will need to grow if the form data is larger than the buffer
                        using IResizeableMemoryHandle<char> urlbody = pool.AllocFormDataBuffer<char>(bufferSize);

                        //Load char buffer from stream
                        int chars = await BufferInputStream(request.InputStream, urlbody, formBuffer, info.Encoding);

                        //Get the body as a span, and split the 'string' at the & character
                        ((ReadOnlySpan<char>)urlbody.AsSpan(0, chars))
                            .Split('&', StringSplitOptions.RemoveEmptyEntries, UrlEncodedSplitCb, request);

                    }
                    break;
                case ContentType.MultiPart:
                    {
                        //Make sure we have a boundry specified
                        if (string.IsNullOrWhiteSpace(request.State.Boundry))
                        {
                            break;
                        }

                        //Alloc the form data buffer
                        using IResizeableMemoryHandle<char> formBody = pool.AllocFormDataBuffer<char>(bufferSize);

                        //Load char buffer from stream
                        int chars = await BufferInputStream(request.InputStream, formBody, formBuffer, info.Encoding);

                        //Split the body as a span at the boundries
                        ((ReadOnlySpan<char>)formBody.AsSpan(0, chars))
                            .Split($"--{request.State.Boundry}", StringSplitOptions.RemoveEmptyEntries, FormDataBodySplitCb, context);

                    }
                    break;
                //Default case is store as a file
                default:
                    //add upload (if it fails thats fine, no memory to clean up)
                    request.AddFileUpload(new(request.InputStream, false, request.State.ContentType, null));
                    break;
            }
        }

        /*
         * Reads the input stream into the char buffer and returns the number of characters read. This method
         * expands the char buffer as needed to accomodate the input stream.
         * 
         * We assume the parsing method checked the size of the input stream so we can assume its safe to read
         * all of it into memory.
         */
        private static async ValueTask<int> BufferInputStream(Stream stream, IResizeableMemoryHandle<char> charBuffer, Memory<byte> binBuffer, Encoding encoding)
        {
            int length = 0;
            do
            {
                //read async
                int read = await stream.ReadAsync(binBuffer);

                //guard
                if (read <= 0)
                {
                    break;
                }

                //calculate the number of characters 
                int numChars = encoding.GetCharCount(binBuffer.Span[..read]);

                //Re-alloc buffer and guard for overflow
                charBuffer.ResizeIfSmaller(checked(numChars + length));

                //Decode and update position
                _ = encoding.GetChars(binBuffer.Span[..read], charBuffer.Span.Slice(length, numChars));

                //Update char count
                length += numChars;

            } while (true);

            //Return the number of characters read
            return length;
        }

        /*
         * Parses a Form-Data content type request entity body and stores those arguments in 
         * Request uploads or request args
         */
        private static void FormDataBodySplitCb(ReadOnlySpan<char> formSegment, HttpContext state)
        {
            //Form data arguments
            string? DispType = null, Name = null, FileName = null;

            ContentType ctHeaderVal = ContentType.NonSupported;

            //Get sliding window for parsing data
            ForwardOnlyReader<char> reader = new(formSegment.TrimCRLF());

            //Read content headers
            do
            {
                //Get the index of the next crlf
                int index = reader.Window.IndexOf(HttpHelpers.CRLF);

                //end of headers
                if (index < 1)
                {
                    break;
                }

                //Get header data before the next crlf
                ReadOnlySpan<char> header = reader.Window[..index];

                //Split header at colon
                int colon;

                //If no data is available after the colon the header is not valid, so move on to the next body
                if ((colon = header.IndexOf(':')) < 1)
                {
                    return;
                }

                //Hash the header value into a header enum
                HttpRequestHeader headerType = HttpHelpers.GetRequestHeaderEnumFromValue(header[..colon]);

                //get the header value
                ReadOnlySpan<char> headerValue = header[(colon + 1)..];

                switch (headerType)
                {
                    case HttpHelpers.ContentDisposition:
                        //Parse the content dispostion
                        HttpHelpers.ParseDisposition(headerValue, out DispType, out Name, out FileName);
                        break;
                    case HttpRequestHeader.ContentType:
                        //The header value for content type should be an MIME content type
                        ctHeaderVal = HttpHelpers.GetContentType(headerValue.Trim());
                        break;
                }

                //Shift window to the next line
                reader.Advance(index + HttpHelpers.CRLF.Length);

            } while (true);

            //Remaining data should be the body data (will have leading and trailing CRLF characters
            //If filename is set, this must be a file
            if (!string.IsNullOrWhiteSpace(FileName))
            {
                //Only add the upload if the request can accept more uploads, otherwise drop it
                if (state.Request.CanAddUpload())
                {
                    ReadOnlySpan<char> fileData = reader.Window.TrimCRLF();

                    FileUpload upload = UploadFromString(fileData, state, FileName, ctHeaderVal);

                    //Store the file in the uploads 
                    state.Request.AddFileUpload(in upload);
                }
            }

            //Make sure the name parameter was set and store the message body as a string
            else if (!string.IsNullOrWhiteSpace(Name))
            {
                //String data as body
                state.Request.RequestArgs[Name] = reader.Window.TrimCRLF().ToString();
            }
        }

        /// <summary>
        /// Allocates a new binary buffer, encodes, and copies the specified data to a new <see cref="FileUpload"/>
        /// structure of the specified content type
        /// </summary>
        /// <param name="data">The string data to copy</param>
        /// <param name="context">The connection context</param>
        /// <param name="filename">The name of the file</param>
        /// <param name="ct">The content type of the file data</param>
        /// <returns>The <see cref="FileUpload"/> container</returns>
        private static FileUpload UploadFromString(ReadOnlySpan<char> data, HttpContext context, string filename, ContentType ct)
        {
            IHttpContextInformation info = context;
            IHttpMemoryPool pool = context.ParentServer.Config.MemoryPool;

            //get number of bytes 
            int bytes = info.Encoding.GetByteCount(data);

            //get a buffer from the HTTP heap
            IResizeableMemoryHandle<byte> buffHandle = pool.AllocFormDataBuffer<byte>(bytes);
            try
            {
                //Convert back to binary
                bytes = info.Encoding.GetBytes(data, buffHandle.Span);

                //Create a new memory stream encapsulating the file data
                VnMemoryStream vms = VnMemoryStream.FromHandle(buffHandle, true, bytes, true);

                //Create new upload wrapper that owns the stream
                return new(vms, true, ct, filename);
            }
            catch
            {
                //Make sure the hanle gets disposed if there is an error
                buffHandle.Dispose();
                throw;
            }
        }

        /*
         * SpanSplit callback function for storing UrlEncoded request entity 
         * bodies in key-value pairs and writing them to the 
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UrlEncodedSplitCb(ReadOnlySpan<char> kvArg, HttpRequest Request)
        {
            //Get key side of agument (or entire argument if no value is set)
            ReadOnlySpan<char> key = kvArg.SliceBeforeParam('=');
            ReadOnlySpan<char> value = kvArg.SliceAfterParam('=');

            //trim, allocate strings, and store in the request arg dict
            Request.RequestArgs[key.TrimCRLF().ToString()] = value.TrimCRLF().ToString();
        }

        /*
         * Parses query parameters from the request location query
         */
        private static void ParseQueryArgs(HttpRequest Request)
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            //Query string parse method
            static void QueryParser(ReadOnlySpan<char> queryArgument, HttpRequest Request)
            {
                //Split spans at the '=' character
                ReadOnlySpan<char> key = queryArgument.SliceBeforeParam('=');
                ReadOnlySpan<char> value = queryArgument.SliceAfterParam('=');

                //Insert into dict
                Request.QueryArgs[key.ToString()] = value.ToString();
            }

            //if the request has query args, parse and store them
            ReadOnlySpan<char> queryString = Request.State.Location.Query;

            if (!queryString.IsEmpty)
            {
                //trim leading '?' if set
                queryString = queryString.TrimStart('?');

                //Split args by '&'
                queryString.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, QueryParser, Request);
            }
        }
    }
}