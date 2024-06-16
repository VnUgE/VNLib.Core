/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: Http11ParseExtensions.cs 
*
* Http11ParseExtensions.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Net;
using System.Linq;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http.Core
{

    internal static class Http11ParseExtensions
    {
        /// <summary>
        /// An internal HTTP character binary pool for HTTP specific internal buffers
        /// </summary>
        public static ArrayPool<byte> InputDataBufferPool { get; } = ArrayPool<byte>.Create();

        /// <summary>
        /// Stores the state of an HTTP/1.1 parsing operation
        /// </summary>
        public ref struct Http1ParseState
        {
            internal Uri? AbsoluteUri;
            internal UriSegments Location;
            internal long ContentLength;
        }

        /*
         * Reduces load when parsing uri components 
         * and allows a one-time vaidation once the uri
         * is compiled
         */
        internal ref struct UriSegments
        {
            public string Scheme;
            public string Host;
            public string Path;
            public string Query;

            public int Port;
        }


        /// <summary>
        /// Reads the first line from the transport stream using the specified buffer
        /// and parses the HTTP request line components: Method, resource, Http Version
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="reader">The reader to read lines from the transport</param>
        /// <param name="parseState">The HTTP1 parsing state</param>
        /// <param name="lineBuf">The buffer to use when parsing the request data</param>
        /// <param name="usingTls">True if the transport is using TLS</param>
        /// <returns>0 if the request line was successfully parsed, a status code if the request could not be processed</returns>
        /// <exception cref="UriFormatException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public static HttpStatusCode Http1ParseRequestLine(this HttpRequest Request, ref Http1ParseState parseState, ref TransportReader reader, Span<char> lineBuf, bool usingTls)
        {
            /*
             * Evil mutable struct, get a local mutable reference to the request's 
             * state structure in order to initialize state variables. 
             */
            ref HttpRequestState reqState = ref Request.GetMutableStateForInit();

            //Locals
            ERRNO requestResult;
            int index, endloc;
            ReadOnlySpan<char> requestLine, pathAndQuery;

            //Read the start line
            requestResult = reader.ReadLine(lineBuf);

            //Must be able to parse the verb and location
            if (requestResult < 1)
            {
                //empty request
                return (HttpStatusCode)1000;
            }
            
            //true up the request line to actual size
            requestLine = lineBuf[..(int)requestResult].Trim();

            //Find the first white space character ("GET / HTTP/1.1")
            index = requestLine.IndexOf(' ');
            if (index == -1)
            {
                return HttpStatusCode.BadRequest;
            }

            //Decode the verb (function requires the string be the exact characters of the request method)
            if ((reqState.Method = HttpHelpers.GetRequestMethod(requestLine[0..index])) == HttpMethod.None)
            {
                return HttpStatusCode.MethodNotAllowed;
            }
            
            //location string should be from end of verb to HTTP/ NOTE: Only supports http... this is an http server
            
            //Client must specify an http version prepended by a single whitespace(rfc2612)
            if ((endloc = requestLine.LastIndexOf(" HTTP/", StringComparison.OrdinalIgnoreCase)) == -1)
            {
                return HttpStatusCode.HttpVersionNotSupported;
            }
            
            //Try to parse the requested http version, only supported versions
            if ((reqState.HttpVersion = HttpHelpers.ParseHttpVersion(requestLine[endloc..])) == HttpVersion.None)
            {
                return HttpStatusCode.HttpVersionNotSupported;
            }

            //Http 1.1 spec defaults to keepalive if the connection header is not set to close
            reqState.KeepAlive = reqState.HttpVersion == HttpVersion.Http11;

            pathAndQuery = requestLine[(index + 1)..endloc].TrimCRLF();

            //Process an absolute uri, 
            if (pathAndQuery.Contains("://", StringComparison.Ordinal))
            {
                //Convert the location string to a .net string and init the location builder (will perform validation when the Uri propery is used)
                parseState.AbsoluteUri = new(pathAndQuery.ToString());
                return 0;
            }
            //Must be a relaive uri that starts with /
            else if (pathAndQuery.Length > 0 && pathAndQuery[0] == '/')
            {
                //Set default scheme
                parseState.Location.Scheme = usingTls ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;

                //Need to manually parse the query string
                int q = pathAndQuery.IndexOf('?');

                //has query?
                if (q == -1)
                {
                    parseState.Location.Path = pathAndQuery.ToString();
                }
                //Does have query argument
                else
                {
                    //separate the path from the query
                    parseState.Location.Path = pathAndQuery[0..q].ToString();
                    parseState.Location.Query = pathAndQuery[(q + 1)..].ToString();
                }
                return 0;
            }

            //Cannot service an unknonw location
            return HttpStatusCode.BadRequest;
        }

        /// <summary>
        /// Reads headers from the transport using the supplied character buffer, and updates the current request
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="parseState">The HTTP1 parsing state</param>
        /// <param name="Config">The current server <see cref="HttpConfig"/></param>
        /// <param name="reader">The <see cref="VnStreamReader"/> to read lines from the transport</param>
        /// <param name="lineBuf">The buffer read data from the transport with</param>
        /// <returns>0 if the request line was successfully parsed, a status code if the request could not be processed</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public static HttpStatusCode Http1ParseHeaders(this HttpRequest Request, ref Http1ParseState parseState, ref TransportReader reader, ref readonly HttpConfig Config, Span<char> lineBuf)
        {
            /*
            * Evil mutable struct, get a local mutable reference to the request's 
            * state structure in order to initialize state variables. 
            */
            ref HttpRequestState reqState = ref Request.GetMutableStateForInit();

            try
            {
                int headerCount = 0, colon;
                bool hostFound = false;
                ERRNO charsRead;
                ReadOnlySpan<char> headerName, requestHeaderValue;
                
                /*
                 * This loop will read "lines" from the transport/reader buffer as headers
                 * and store them in the rented character buffer with 0 allocations.
                 * 
                 * Lines will be read from the transport reader until an empty line is read, 
                 * or an exception occurs. The VnStreamReader class will search for lines 
                 * directly in the binary rather than converting the data then parsing it. 
                 * When a line is parsed, its assumed to be an HTTP header at this point in 
                 * the parsing, and is separated into its key-value pair determined by the 
                 * first ':' character to appear.
                 * 
                 * The header length will be limited by the size of the character buffer, 
                 * or the reader binary buffer while reading lines. Buffer sizes are fixed 
                 * to the system memory page size. Depending on the encoding the user chooses
                 * this should not be an issue for most configurations. This strategy is 
                 * most efficient for realtivly small header sizes. 
                 * 
                 * The header's key is hashed by the HttpHelpers class and the hash is used to 
                 * index a lookup table to return its enumeration value which is used in the swtich 
                 * statement to reduce the number of strings added to the request header container.
                 * This was a major effort to reduce memory and CPU overhead while using the 
                 * WebHeaderCollection .NET class, which I think is still worth using instead of a
                 * custom header data structure class. 
                 * 
                 * Some case statments are custom HttpRequestHeader enum values via internal casted 
                 * constants to be consistant with he .NET implementation.
                 */
                do
                {
                    //Read a line until we reach the end of headers, this call will block if end of characters is reached and a new string will be read
                    charsRead = reader.ReadLine(lineBuf);

                    //If the result is less than 1, no line is available (end of headers) or could not be read 
                    if (charsRead < 1)
                    {
                        break;
                    }

                    //Header count exceeded or header larger than header buffer size
                    if (charsRead < 0 || headerCount > Config.MaxRequestHeaderCount)
                    {
                        return HttpStatusCode.RequestHeaderFieldsTooLarge;
                    }                    
                    
                    {
                        //Get the true size of the read header line as a readonly span 
                        ReadOnlySpan<char> header = lineBuf[..(int)charsRead];

                        /*
                         * RFC 7230, ignore headers with preceeding whitespace
                         * 
                         * If the first character is whitespace that is enough to 
                         * ignore the rest of the header
                         */
                        if (header[0] == ' ')
                        {
                            //Move on to next header
                            continue;
                        }

                        //Find the first colon
                        colon = header.IndexOf(':');
                        //No colon was found, this is an invalid string, try to skip it and keep reading
                        if (colon <= 0)
                        {
                            continue;
                        }
                        
                        //Store header and its value (sections before and after colon)
                        headerName = header[..colon].TrimCRLF();
                        requestHeaderValue = header[(colon + 1)..].TrimCRLF();
                    }
                   
                    //Hash the header key and lookup the request header value
                    switch (HttpHelpers.GetRequestHeaderEnumFromValue(headerName))
                    {
                        case HttpRequestHeader.Connection:
                            {
                                //Update keepalive, if the connection header contains "closed" and with the current value of keepalive
                                reqState.KeepAlive &= !requestHeaderValue.Contains("close", StringComparison.OrdinalIgnoreCase);

                                //Also store the connecion header into the store
                                Request.Headers.Add(HttpRequestHeader.Connection, requestHeaderValue.ToString());
                            }
                            break;
                        case HttpRequestHeader.ContentType:
                            {
                                if (!HttpHelpers.TryParseContentType(requestHeaderValue.ToString(), out string? ct, out string? charset, out string? boundry) || ct == null)
                                {
                                    //Invalid content type header value
                                    return HttpStatusCode.UnsupportedMediaType;
                                }

                                reqState.Boundry = boundry;
                                reqState.Charset = charset;

                                //Get the content type enum from mime type
                                reqState.ContentType = HttpHelpers.GetContentType(ct);
                            }
                            break;
                        case HttpRequestHeader.ContentLength:
                            {
                                //Content length has already been calculated, ERROR, rfc 7230
                                if(parseState.ContentLength > 0)
                                {
                                    Config.ServerLog.Debug("Message warning, recieved multiple content length headers");
                                    return HttpStatusCode.BadRequest;
                                }

                                //Only capture positive values, and if length is negative we are supposed to ignore it
                                if (ulong.TryParse(requestHeaderValue, out ulong len) && len < long.MaxValue)
                                {
                                    parseState.ContentLength = (long)len;
                                }
                                else
                                {
                                    return HttpStatusCode.BadRequest;
                                }
                                
                                //Request size it too large to service
                                if (parseState.ContentLength > Config.MaxUploadSize)
                                {
                                    return HttpStatusCode.RequestEntityTooLarge;
                                }
                            }
                            break;
                        case HttpRequestHeader.Host:
                            {
                                //Set host found flag
                                hostFound = true;

                                //Split the host value by the port parameter 
                                ReadOnlySpan<char> port = requestHeaderValue.SliceAfterParam(':').Trim();

                                //Slicing before the colon should always provide a useable hostname, so allocate a string for it
                                string hostOnly = requestHeaderValue.SliceBeforeParam(':').Trim().ToString();
                                
                                //Verify that the host is usable
                                if (Uri.CheckHostName(hostOnly) == UriHostNameType.Unknown)
                                {
                                    return HttpStatusCode.BadRequest;
                                }
                                
                                //Verify that the host matches the host header if absolue uri is set
                                if (parseState.AbsoluteUri != null)
                                {
                                    if (!hostOnly.Equals(parseState.Location.Host, StringComparison.OrdinalIgnoreCase))
                                    {
                                        return HttpStatusCode.BadRequest;
                                    }
                                }

                                /*
                                 * Uri segments are only assigned/used if an absolute 
                                 * uri was not set in the request line.
                                 */

                                parseState.Location.Host = hostOnly;
                                
                                //If the port span is empty, no colon was found or the port is invalid
                                if (!port.IsEmpty)
                                {
                                    //try to parse the port number
                                    if (!int.TryParse(port, out int p) || p < 0 || p > ushort.MaxValue)
                                    {
                                        return HttpStatusCode.BadRequest;
                                    }
                                    //Store port
                                    parseState.Location.Port = p;
                                }

                                //Set host header in collection also
                                Request.Headers.Add(HttpRequestHeader.Host, requestHeaderValue.ToString());
                            }
                            break;
                        case HttpRequestHeader.Cookie:
                            {
                                //Local function to break cookie segments into key-value pairs
                                static void AddCookiesCallback(ReadOnlySpan<char> cookie, Dictionary<string, string> cookieContainer)
                                {
                                    //Get the name parameter and alloc a string
                                    string name = cookie.SliceBeforeParam('=').Trim().ToString();
                                    string value = cookie.SliceAfterParam('=').Trim().ToString();

                                    //Add the cookie to the dictionary
                                    _ = cookieContainer.TryAdd(name, value);
                                }
                                //Split all cookies by ; with trailing whitespace
                                requestHeaderValue.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, AddCookiesCallback, Request.Cookies);
                            }
                            break;
                        case HttpRequestHeader.AcceptLanguage:
                            //Capture accept languages and store in the request accept collection
                            requestHeaderValue.Split(',', Request.AcceptLanguage, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            break;
                        case HttpRequestHeader.Accept:
                            //Capture accept content types and store in request accept collection
                            requestHeaderValue.Split(',', Request.Accept, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            break;
                        case HttpRequestHeader.Referer:
                            {
                                //Check the referer header and capture its uri instance, it should be absolutely parseable
                                if (!requestHeaderValue.IsEmpty && Uri.TryCreate(requestHeaderValue.ToString(), UriKind.Absolute, out Uri? refer))
                                {
                                    reqState.Referrer = refer;
                                }
                            }
                            break;
                        case HttpRequestHeader.Range:
                            {
                                //Use rfc 7233 -> https://www.rfc-editor.org/rfc/rfc7233

                                //MUST ignore range header if not a GET method
                                if(reqState.Method != HttpMethod.GET)
                                {
                                    //Ignore the header and continue parsing headers
                                    break;
                                }

                                //See if range bytes value has been set
                                ReadOnlySpan<char> rawRange = requestHeaderValue.SliceAfterParam("bytes=").TrimCRLF();

                                //Make sure the bytes parameter is set
                                if (rawRange.IsEmpty)
                                {
                                    //Ignore the header and continue parsing headers
                                    break;
                                }

                                //Get start range
                                ReadOnlySpan<char> startRange = rawRange.SliceBeforeParam('-');
                                //Get end range (empty if no - exists)
                                ReadOnlySpan<char> endRange = rawRange.SliceAfterParam('-');

                                //try to parse the range values
                                bool hasStartRange = ulong.TryParse(startRange, out ulong startRangeValue);
                                bool hasEndRange = ulong.TryParse(endRange, out ulong endRangeValue);

                                /*
                                 * The range header may be a range-from-end type request that 
                                 * looks like this:
                                 * 
                                 * bytes=-500
                                 * 
                                 * or a range-from-start type request that looks like this:
                                 * bytes=500-
                                 * 
                                 * or a full range of bytes
                                 * bytes=0-500
                                 */

                                if (hasEndRange)
                                {
                                    if (hasStartRange)
                                    {
                                        //Validate explicit range
                                        if(!HttpRange.IsValidRangeValue(startRangeValue, endRangeValue))
                                        {
                                            //If range is invalid were supposed to ignore it and continue
                                            break;
                                        }
                                      
                                        reqState.Range = HttpRange.FullRange(startRangeValue, endRangeValue);
                                    }
                                    else
                                    {
                                        reqState.Range = HttpRange.FromEnd(endRangeValue);
                                    }
                                }
                                else if(hasStartRange)
                                {
                                    //Valid start range only, so from start range
                                    reqState.Range = HttpRange.FromStart(startRangeValue);
                                }
                                //No valid range values
                            }
                           
                            break;
                        case HttpRequestHeader.UserAgent:
                            //Store user-agent
                            reqState.UserAgent = requestHeaderValue.IsEmpty ? string.Empty : requestHeaderValue.TrimCRLF().ToString();
                            break;
                        //Special code for origin header
                        case HttpHelpers.Origin:
                            {
                                //Alloc a string for origin
                                string origin = requestHeaderValue.ToString();

                                //Origin headers should always be absolute address "parsable"
                                if (Uri.TryCreate(origin, UriKind.Absolute, out Uri? org))
                                {
                                    reqState.Origin = org;
                                }
                            }
                            break;
                        case HttpRequestHeader.Expect:
                            //Accept 100-continue for the Expect header value
                            reqState.Expect = requestHeaderValue.Equals("100-continue", StringComparison.OrdinalIgnoreCase);
                            break;
                        default:
                            //By default store the header in the request header store
                            Request.Headers.Add(headerName.ToString(), requestHeaderValue.ToString());
                            break;
                    }
                    //Increment header count
                    headerCount++;
                } while (true);

                //If request is http11 then host is required
                if (reqState.HttpVersion == HttpVersion.Http11 && !hostFound)
                {
                    return HttpStatusCode.BadRequest;
                }
                
            }
            //Catch an arugment exception within the header add function to cause a bad request result
            catch (ArgumentException)
            {
                return HttpStatusCode.BadRequest;
            }

            //Store absolute uri if set
            if(parseState.AbsoluteUri != null)
            {
                reqState.Location = parseState.AbsoluteUri;
            }
            //Check the final location to make sure data was properly sent
            else if(string.IsNullOrWhiteSpace(parseState.Location.Host)
                || string.IsNullOrWhiteSpace(parseState.Location.Scheme)
                || string.IsNullOrWhiteSpace(parseState.Location.Path)
            )
            {
                return HttpStatusCode.BadRequest;
            }
            else
            {
                /*
                 * Double allocations are not ideal here, but for now, its the 
                 * safest way to build and validate a foreign uri. Its better
                 * than it was.
                 * 
                 * A string could be build from heap memory then passed to the 
                 * uri constructor for validation, but this will work for now.
                 */

                //Build the final uri if successfully parsed into segments
                reqState.Location = new UriBuilder(
                    scheme: parseState.Location.Scheme,
                    host: parseState.Location.Host,
                    port: parseState.Location.Port,
                    path: parseState.Location.Path,
                    extraValue: null
                )
                {
                    Query = parseState.Location.Query,
                }.Uri;                
            }

            return 0;
        }

        /// <summary>
        /// Prepares the entity body for the current HTTP1 request
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="Config">The current server <see cref="HttpConfig"/></param>
        /// <param name="parseState">The HTTP1 parsing state</param>
        /// <param name="reader">The <see cref="VnStreamReader"/> to read lines from the transport</param>
        /// <returns>0 if the request line was successfully parsed, a status code if the request could not be processed</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public static HttpStatusCode Http1PrepareEntityBody(this HttpRequest Request, ref Http1ParseState parseState, ref TransportReader reader, ref readonly HttpConfig Config)
        {
            /*
            * Evil mutable struct, get a local mutable reference to the request's 
            * state structure in order to initialize state variables. 
            */
            ref HttpRequestState reqState = ref Request.GetMutableStateForInit();

            //If the content type is multipart, make sure its not too large to ingest
            if (reqState.ContentType == ContentType.MultiPart && parseState.ContentLength > Config.MaxFormDataUploadSize)
            {
                return HttpStatusCode.RequestEntityTooLarge;
            }

            //Only ingest the rest of the message body if the request is not a head, get, or trace methods
            if ((reqState.Method & (HttpMethod.GET | HttpMethod.HEAD | HttpMethod.TRACE)) != 0)
            {
                //Bad format to include a message body with a GET, HEAD, or TRACE request
                if (parseState.ContentLength > 0)
                {
                    Config.ServerLog.Debug(
                        "Message body received from {ip} with GET, HEAD, or TRACE request, was considered an error and the request was dropped",
                        reqState.RemoteEndPoint
                    );
                    return HttpStatusCode.BadRequest;
                }
                else
                {
                    //Success!
                    return 0;
                }
            }
            
            //Check for chuncked transfer encoding
            ReadOnlySpan<char> transfer = Request.Headers[HttpRequestHeader.TransferEncoding];

            if (!transfer.IsEmpty && transfer.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                //Not a valid http version for chunked transfer encoding
                if (reqState.HttpVersion != HttpVersion.Http11)
                {
                    return HttpStatusCode.BadRequest;
                }

                /*
                 * Was a content length also specified?
                 * This is an issue and is likely an attack. I am choosing not to support 
                 * the HTTP 1.1 standard and will deny reading the rest of the data from the 
                 * transport. 
                 */
                if (parseState.ContentLength > 0)
                {
                    Config.ServerLog.Debug("Possible attempted desync, Content length + chunked encoding specified. RemoteEP: {ip}", reqState.RemoteEndPoint);
                    return HttpStatusCode.BadRequest;
                }

                //Handle chunked transfer encoding (not implemented yet)
                return HttpStatusCode.NotImplemented;
            }
            //Make sure non-zero cl header was provided
            else if (parseState.ContentLength > 0)
            {
                //clamp max available to max content length
                int available = Math.Clamp(reader.Available, 0, (int)parseState.ContentLength);

                /*
                 * The reader may still have available content data following the headers segment
                 * that is part of the entity body data. This data must be read and stored in the
                 * input stream for the request.
                 * 
                 * If no data is available from the buffer, we can just prepare the input stream
                 * with null
                 */

                ref InitDataBuffer? initData = ref Request.InputStream.Prepare(parseState.ContentLength);

                if (available > 0)
                {
                    //Alloc the buffer and asign it
                    initData = InitDataBuffer.AllocBuffer(InputDataBufferPool, available);

                    //Read remaining data into the buffer's data segment
                    _ = reader.ReadRemaining(initData.Value.DataSegment);
                }

                //Notify request that an entity body has been set
                reqState.HasEntityBody = true;
            }
            //Success!
            return 0;
        }
    }
}
