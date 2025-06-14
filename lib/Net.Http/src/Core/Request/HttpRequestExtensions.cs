﻿/*
* Copyright (c) 2025 Vaughn Nugent
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

namespace VNLib.Net.Http.Core.Request
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
            else if (serverSupported.HasFlag(CompressionMethod.Zstd)
                && acceptEncoding.Contains("zstd", StringComparison.OrdinalIgnoreCase))
            {
                return CompressionMethod.Zstd;
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
            if (Request.State.Origin is null)
            {
                return false;
            }

            //Get the origin string components for comparison (allocs new strings :( )
            string locOrigin = Request.State.Location.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped);
            string reqOrigin = Request.State.Origin.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped);

            //If origin components are not equal, this is a cross origin request
            return !string.Equals(locOrigin, reqOrigin, StringComparison.OrdinalIgnoreCase);
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

            switch (request.State.ContentType)
            {
                //CT not supported, dont read it
                case ContentType.NonSupported:
                    break;
                case ContentType.UrlEncoded:
                    {
                        //Alloc the form data character buffer, this will need to grow if the form data is larger than the buffer
                        using IResizeableMemoryHandle<char> urlbody = AllocFdBuffer(context);

                        int chars = await BufferInputStreamAsChars(request.InputStream, urlbody, GetFdBuffer(context), info.Encoding);

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

                        using IResizeableMemoryHandle<char> formBody = AllocFdBuffer(context);

                        int chars = await BufferInputStreamAsChars(request.InputStream, formBody, GetFdBuffer(context), info.Encoding);

                        //Split the body as a span at the boundries
                        ((ReadOnlySpan<char>)formBody.AsSpan(0, chars))
                            .Split($"--{request.State.Boundry}", StringSplitOptions.RemoveEmptyEntries, FormDataBodySplitCb, context);

                    }
                    break;
                //Default case is store as a file
                default:
                    //add upload (if it fails thats fine, no memory to clean up)
                    request.AddFileUpload(new(request.InputStream, DisposeStream: false, request.State.ContentType, FileName: null));
                    break;
            }


            static IResizeableMemoryHandle<char> AllocFdBuffer(HttpContext context)
            {
                //Gets the max form data buffer size to help calculate the initial char buffer size
                int maxBufferSize = context.ParentServer.BufferConfig.FormDataBufferSize;

                //Calculate a largest available buffer to read the entire stream or up to the maximum buffer size
                int buffersize = (int)Math.Min(context.Request.InputStream.Length, maxBufferSize);

                return context.ParentServer.Config.MemoryPool.AllocFormDataBuffer<char>(buffersize);
            }

            static Memory<byte> GetFdBuffer(HttpContext context)
            {
                Memory<byte> formBuffer = context.Buffers.GetFormDataBuffer();
                Debug.Assert(!formBuffer.IsEmpty, "GetFormDataBuffer() returned an empty memory buffer");
                return formBuffer;
            }
        }

        /*
         * Reads the input stream into the char buffer and returns the number of characters read. This method
         * expands the char buffer as needed to accomodate the input stream.
         * 
         * We assume the parsing method checked the size of the input stream so we can assume its safe to read
         * all of it into memory.
         */
        private static async ValueTask<int> BufferInputStreamAsChars(
            Stream stream,
            IResizeableMemoryHandle<char> charBuffer,
            Memory<byte> binBuffer,
            Encoding encoding
        )
        {
            int charsRead = 0;
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
                charBuffer.ResizeIfSmaller(checked(numChars + charsRead));

                _ = encoding.GetChars(
                    bytes: binBuffer.Span[..read],
                    chars: charBuffer.Span.Slice(charsRead, numChars)
                );

                charsRead += numChars;

            } while (true);

            return charsRead;
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
                    UploadFromString(
                        data: reader.Window.TrimCRLF(),
                        context: state,
                        filename: FileName,
                        contentType: ctHeaderVal,
                        upload: ref state.Request.AddFileUpload()
                    );
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
        /// <param name="contentType">The content type of the file data</param>
        /// <param name="upload">A reference to the file upload to assign</param>
        /// <returns>The <see cref="FileUpload"/> container</returns>
        private static void UploadFromString(
            ReadOnlySpan<char> data,
            HttpContext context,
            string filename,
            ContentType contentType,
            ref FileUpload upload
        )
        {
            IHttpContextInformation info = context;
            IHttpMemoryPool pool = context.ParentServer.Config.MemoryPool;

            int bytes = info.Encoding.GetByteCount(data);

            IResizeableMemoryHandle<byte> buffHandle = pool.AllocFormDataBuffer<byte>(bytes);
            try
            {
                //Convert back to binary
                bytes = info.Encoding.GetBytes(data, buffHandle.Span);

                //Readonly stream to buffer encoded data
                VnMemoryStream vms = VnMemoryStream.FromHandle(buffHandle, ownsHandle: true, bytes, readOnly: true);

                //Create new upload wrapper that owns the stream
                upload = new(vms, DisposeStream: true, contentType, filename);
            }
            catch
            {
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