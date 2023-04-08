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
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

using static VNLib.Net.Http.Core.CoreBufferHelpers;

namespace VNLib.Net.Http.Core
{
    internal static class HttpRequestExtensions
    {
        public enum CompressionType
        {
            None,
            Gzip,
            Deflate,
            Brotli
        }
        
        /// <summary>
        /// Gets the <see cref="CompressionType"/> that the connection accepts
        /// in a default order, or none if not enabled
        /// </summary>
        /// <param name="request"></param>
        /// <returns>A <see cref="CompressionType"/> with a value the connection support</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompressionType GetCompressionSupport(this HttpRequest request)
        {
            string? acceptEncoding = request.Headers[HttpRequestHeader.AcceptEncoding];

            if (acceptEncoding == null)
            {
                return CompressionType.None;
            }
            else if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
            {
                return CompressionType.Gzip;
            }
            else if (acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
            {
                return CompressionType.Deflate;
            }
            else if (acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase))
            {
                return CompressionType.Brotli;
            }
            else
            {
                return CompressionType.None;
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
            return Request.Origin != null
                && (!Request.Origin.Authority.Equals(Request.Location.Authority, StringComparison.Ordinal) 
                || !Request.Origin.Scheme.Equals(Request.Location.Scheme, StringComparison.Ordinal));
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
        /// Initializes the <see cref="HttpRequest"/> for an incomming connection
        /// </summary>
        /// <param name="server"></param>
        /// <param name="ctx">The <see cref="ITransportContext"/> to attach the request to</param>
        /// <param name="defaultHttpVersion">The default http version</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Initialize(this HttpRequest server, ITransportContext ctx, HttpVersion defaultHttpVersion)
        {
            server.LocalEndPoint = ctx.LocalEndPoint;
            server.RemoteEndPoint = ctx.RemoteEndpoint;
            server.EncryptionVersion = ctx.SslVersion;
            //Set to default http version so the response can be configured properly
            server.HttpVersion = defaultHttpVersion;
        }


        /// <summary>
        /// Initializes the <see cref="HttpRequest.RequestBody"/> for the current request
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="maxBufferSize">The maxium buffer size allowed while parsing reqeust body data</param>
        /// <param name="encoding">The request data encoding for url encoded or form data bodies</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        internal static ValueTask InitRequestBodyAsync(this HttpRequest Request, int maxBufferSize, Encoding encoding)
        {
            /*
             * Parses query parameters from the request location query
             */
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void ParseQueryArgs(HttpRequest Request)
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                //Query string parse method
                static void QueryParser(ReadOnlySpan<char> queryArgument, HttpRequest Request)
                {
                    //Split spans after the '=' character
                    ReadOnlySpan<char> key = queryArgument.SliceBeforeParam('=');
                    ReadOnlySpan<char> value = queryArgument.SliceAfterParam('=');
                    //Insert into dict
                    Request.RequestBody.QueryArgs[key.ToString()] = value.ToString();
                }

                //if the request has query args, parse and store them
                ReadOnlySpan<char> queryString = Request.Location.Query;
                if (!queryString.IsEmpty)
                {
                    //trim leading '?' if set
                    queryString = queryString.TrimStart('?');
                    //Split args by '&'
                    queryString.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, QueryParser, Request);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static async ValueTask ParseInputStream(HttpRequest Request, int maxBufferSize, Encoding encoding)
            {
                /*
                *  Reads all available data from the request input stream  
                *  If the stream size is smaller than a TCP buffer size, will complete synchronously
                */
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static ValueTask<VnString> ReadInputStreamAsync(HttpRequest Request, int maxBufferSize, Encoding encoding)
                {
                    //Calculate a largest available buffer to read the entire stream or up to the maximum buffer size
                    int bufferSize = (int)Math.Min(Request.InputStream.Length, maxBufferSize);
                    //Read the stream into a vnstring
                    return VnString.FromStreamAsync(Request.InputStream, encoding, HttpPrivateHeap, bufferSize);
                }
                /*
                 * SpanSplit callback function for storing UrlEncoded request entity 
                 * bodies in key-value pairs and writing them to the 
                 */
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static void UrlEncodedSplitCb(ReadOnlySpan<char> kvArg, HttpRequest Request)
                {
                    //Get key side of agument (or entire argument if no value is set)
                    ReadOnlySpan<char> key = kvArg.SliceBeforeParam('=');
                    ReadOnlySpan<char> value = kvArg.SliceAfterParam('=');
                    //trim, allocate strings, and store in the request arg dict
                    Request.RequestBody.RequestArgs[key.TrimCRLF().ToString()] = value.TrimCRLF().ToString();
                }

                /*
                * Parses a Form-Data content type request entity body and stores those arguments in 
                * Request uploads or request args
                */
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static void FormDataBodySplitCb(ReadOnlySpan<char> formSegment, ValueTuple<HttpRequestBody, Encoding> state)
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
                        //Get header data
                        ReadOnlySpan<char> header = reader.Window[..index];
                        //Split header at colon
                        int colon = header.IndexOf(':');
                        //If no data is available after the colon the header is not valid, so move on to the next body
                        if (colon < 1)
                        {
                            return;
                        }
                        //Hash the header value into a header enum
                        HttpRequestHeader headerType = HttpHelpers.GetRequestHeaderEnumFromValue(header[..colon]);
                        //get the header value
                        ReadOnlySpan<char> headerValue = header[(colon + 1)..];
                        //Check for content dispositon header
                        if (headerType == HttpHelpers.ContentDisposition)
                        {
                            //Parse the content dispostion
                            HttpHelpers.ParseDisposition(headerValue, out DispType, out Name, out FileName);
                        }
                        //Check for content type
                        else if (headerType == HttpRequestHeader.ContentType)
                        {
                            //The header value for content type should be an MIME content type
                            ctHeaderVal = HttpHelpers.GetContentType(headerValue.Trim().ToString());
                        }
                        //Shift window to the next line
                        reader.Advance(index + HttpHelpers.CRLF.Length);
                    } while (true);
                    //Remaining data should be the body data (will have leading and trailing CRLF characters
                    //If filename is set, this must be a file
                    if (!string.IsNullOrWhiteSpace(FileName))
                    {
                        //Store the file in the uploads 
                        state.Item1.Uploads.Add(FileUpload.FromString(reader.Window.TrimCRLF(), state.Item2, FileName, ctHeaderVal));
                    }
                    //Make sure the name parameter was set and store the message body as a string
                    else if (!string.IsNullOrWhiteSpace(Name))
                    {
                        //String data as body
                        state.Item1.RequestArgs[Name] = reader.Window.TrimCRLF().ToString();
                    }
                }

                switch (Request.ContentType)
                {
                    //CT not supported, dont read it
                    case ContentType.NonSupported:
                        break;
                    case ContentType.UrlEncoded:
                        //Create a vnstring from the message body and parse it (assuming url encoded bodies are small so a small stack buffer will be fine)
                        using (VnString urlbody = await ReadInputStreamAsync(Request, maxBufferSize, encoding))
                        {
                            //Get the body as a span, and split the 'string' at the & character
                            urlbody.AsSpan().Split('&', StringSplitOptions.RemoveEmptyEntries, UrlEncodedSplitCb, Request);
                        }
                        break;
                    case ContentType.MultiPart:
                        //Make sure we have a boundry specified
                        if (string.IsNullOrWhiteSpace(Request.Boundry))
                        {
                            break;
                        }
                        //Read all data from stream into string
                        using (VnString body = await ReadInputStreamAsync(Request, maxBufferSize, encoding))
                        {
                            //Split the body as a span at the boundries
                            body.AsSpan().Split($"--{Request.Boundry}", StringSplitOptions.RemoveEmptyEntries, FormDataBodySplitCb, (Request.RequestBody, encoding));
                        }
                        break;
                    //Default case is store as a file
                    default:
                        //add upload 
                        Request.RequestBody.Uploads.Add(new(Request.InputStream, string.Empty, Request.ContentType, false));
                        break;
                }
            }

            //Parse query
            ParseQueryArgs(Request);

            //Decode requests from body
            return !Request.HasEntityBody ? ValueTask.CompletedTask : ParseInputStream(Request, maxBufferSize, encoding);
        }
    }
}