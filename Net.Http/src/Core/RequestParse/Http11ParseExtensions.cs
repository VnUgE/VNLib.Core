/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Collections.Generic;
using System.Security.Authentication;
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
        /// Stores the state of an HTTP/1.1 parsing operation
        /// </summary>
        public ref struct Http1ParseState
        {
            internal UriBuilder? Location;
            internal bool IsAbsoluteRequestUrl;
            internal long ContentLength;
        }


        /// <summary>
        /// Reads the first line from the transport stream using the specified buffer
        /// and parses the HTTP request line components: Method, resource, Http Version
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="reader">The reader to read lines from the transport</param>
        /// <param name="parseState">The HTTP1 parsing state</param>
        /// <param name="lineBuf">The buffer to use when parsing the request data</param>
        /// <returns>0 if the request line was successfully parsed, a status code if the request could not be processed</returns>
        /// <exception cref="UriFormatException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public static HttpStatusCode Http1ParseRequestLine(this HttpRequest Request, ref Http1ParseState parseState, ref TransportReader reader, in Span<char> lineBuf)
        {
            //Locals
            ERRNO requestResult;
            int index, endloc;

            //Read the start line
            requestResult = reader.ReadLine(lineBuf);
            //Must be able to parse the verb and location
            if (requestResult < 1)
            {
                //empty request
                return (HttpStatusCode)1000;
            }
            
            //true up the request line to actual size
            ReadOnlySpan<char> requestLine = lineBuf[..(int)requestResult].Trim();
            //Find the first white space character ("GET / HTTP/1.1")
            index = requestLine.IndexOf(' ');
            if (index == -1)
            {
                return HttpStatusCode.BadRequest;
            }
            
            //Decode the verb (function requires the string be the exact characters of the request method)
            Request.Method = HttpHelpers.GetRequestMethod(requestLine[0..index]);
            //Make sure the method is supported
            if (Request.Method == HttpMethod.NOT_SUPPORTED)
            {
                return HttpStatusCode.MethodNotAllowed;
            }
            
            //location string should be from end of verb to HTTP/ NOTE: Only supports http... this is an http server
            endloc = requestLine.LastIndexOf(" HTTP/", StringComparison.OrdinalIgnoreCase);
            //Client must specify an http version prepended by a single whitespace(rfc2612)
            if (endloc == -1)
            {
                return HttpStatusCode.HttpVersionNotSupported;
            }
            
            //Try to parse the version and only accept the 3 major versions of http
            Request.HttpVersion = HttpHelpers.ParseHttpVersion(requestLine[endloc..]);
            //Check to see if the version was parsed succesfully
            if (Request.HttpVersion == HttpVersion.NotSupported)
            {
                //Return not supported
                return HttpStatusCode.HttpVersionNotSupported;
            }

            //Set keepalive flag if http11
            Request.KeepAlive = Request.HttpVersion == HttpVersion.Http11;

            //Get the location segment from the request line
            ReadOnlySpan<char> paq = requestLine[(index + 1)..endloc].TrimCRLF();

            //Process an absolute uri, 
            if (paq.Contains("://", StringComparison.Ordinal))
            {
                //Convert the location string to a .net string and init the location builder (will perform validation when the Uri propery is used)
                parseState.Location = new(paq.ToString());
                parseState.IsAbsoluteRequestUrl = true;
                return 0;
            }
            //Try to capture a realative uri
            else if (paq.Length > 0 && paq[0] == '/')
            {
                //Create a default location uribuilder
                parseState.Location = new()
                {
                    //Set a default scheme
                    Scheme = Request.EncryptionVersion == SslProtocols.None ? Uri.UriSchemeHttp : Uri.UriSchemeHttps,
                };
                //Need to manually parse the query string
                int q = paq.IndexOf('?');
                //has query?
                if (q == -1)
                {
                    parseState.Location.Path = paq.ToString();
                }
                //Does have query argument
                else
                {
                    //separate the path from the query
                    parseState.Location.Path = paq[0..q].ToString();
                    parseState.Location.Query = paq[(q + 1)..].ToString();
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
        public static HttpStatusCode Http1ParseHeaders(this HttpRequest Request, ref Http1ParseState parseState, ref TransportReader reader, in HttpConfig Config, in Span<char> lineBuf)
        {
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
                                Request.KeepAlive &= !requestHeaderValue.Contains("close", StringComparison.OrdinalIgnoreCase);
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
                                Request.Boundry = boundry;
                                Request.Charset = charset;
                                //Get the content type enum from mime type
                                Request.ContentType = HttpHelpers.GetContentType(ct);
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
                                //Slicing beofre the colon should always provide a useable hostname, so allocate a string for it
                                string host = requestHeaderValue.SliceBeforeParam(':').Trim().ToString();
                                
                                //Verify that the host is usable
                                if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
                                {
                                    return HttpStatusCode.BadRequest;
                                }
                                
                                //Verify that the host matches the host header if absolue uri is set
                                if (parseState.IsAbsoluteRequestUrl)
                                {
                                    if (!host.Equals(parseState.Location!.Host, StringComparison.OrdinalIgnoreCase))
                                    {
                                        return HttpStatusCode.BadRequest;
                                    }
                                }

                                //store the host value
                                parseState.Location!.Host = host;
                                
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
                            }
                            break;
                        case HttpRequestHeader.Cookie:
                            {
                                //Local function to break cookie segments into key-value pairs
                                static void AddCookiesCallback(ReadOnlySpan<char> cookie, Dictionary<string, string> cookieContainer)
                                {
                                    //Get the name parameter and alloc a string
                                    string name = cookie.SliceBeforeParam('=').Trim().ToString();
                                    //Get the value parameter and alloc a string
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
                                    Request.Referrer = refer;
                                }
                            }
                            break;
                        case HttpRequestHeader.Range:
                            {
                                //See if range bytes value has been set
                                ReadOnlySpan<char> rawRange = requestHeaderValue.SliceAfterParam("bytes=").TrimCRLF();
                                //Make sure the bytes parameter is set
                                if (rawRange.IsEmpty)
                                {
                                    break;
                                }
                                //Get start range
                                ReadOnlySpan<char> startRange = rawRange.SliceBeforeParam('-');
                                //Get end range (empty if no - exists)
                                ReadOnlySpan<char> endRange = rawRange.SliceAfterParam('-');
                                //See if a range end is specified
                                if (endRange.IsEmpty)
                                {
                                    //No end range specified, so only range start
                                    if (long.TryParse(startRange, out long start) && start > -1)
                                    {
                                        //Create new range 
                                        Request.Range = new(start, -1);
                                        break;
                                    }
                                }
                                //Range has a start and end
                                else if (long.TryParse(startRange, out long start) && long.TryParse(endRange, out long end) && end > -1)
                                {
                                    //get start and end components from range header
                                    Request.Range = new(start, end);
                                    break;
                                }
                            }
                            //Could not parse start range from header
                            return HttpStatusCode.RequestedRangeNotSatisfiable;
                        case HttpRequestHeader.UserAgent:
                            //Store user-agent
                            Request.UserAgent = requestHeaderValue.IsEmpty ? string.Empty : requestHeaderValue.TrimCRLF().ToString();
                            break;
                        //Special code for origin header
                        case HttpHelpers.Origin:
                            {
                                //Alloc a string for origin
                                string origin = requestHeaderValue.ToString();
                                //Origin headers should always be absolute address "parsable"
                                if (Uri.TryCreate(origin, UriKind.Absolute, out Uri? org))
                                {
                                    Request.Origin = org;
                                }
                            }
                            break;
                        case HttpRequestHeader.Expect:
                            //Accept 100-continue for the Expect header value
                            Request.Expect = requestHeaderValue.Equals("100-continue", StringComparison.OrdinalIgnoreCase);
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
                if (Request.HttpVersion == HttpVersion.Http11 && !hostFound)
                {
                    return HttpStatusCode.BadRequest;
                }
                
            }
            //Catch an arugment exception within the header add function to cause a bad request result
            catch (ArgumentException)
            {
                return HttpStatusCode.BadRequest;
            }

            //Check the final location to make sure data was properly sent
            if (string.IsNullOrWhiteSpace(parseState.Location?.Host)
                || string.IsNullOrWhiteSpace(parseState.Location.Scheme)
                || string.IsNullOrWhiteSpace(parseState.Location.Path)
                )
            {
                return HttpStatusCode.BadRequest;
            }
            
            //Store the finalized location
            Request.Location = parseState.Location.Uri;
            
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
        public static HttpStatusCode Http1PrepareEntityBody(this HttpRequest Request, ref Http1ParseState parseState, ref TransportReader reader, in HttpConfig Config)
        {
            //If the content type is multipart, make sure its not too large to ingest
            if (Request.ContentType == ContentType.MultiPart && parseState.ContentLength > Config.MaxFormDataUploadSize)
            {
                return HttpStatusCode.RequestEntityTooLarge;
            }

            //Only ingest the rest of the message body if the request is not a head, get, or trace methods
            if ((Request.Method & (HttpMethod.GET | HttpMethod.HEAD | HttpMethod.TRACE)) != 0)
            {
                //Bad format to include a message body with a GET, HEAD, or TRACE request
                if (parseState.ContentLength > 0)
                {
                    Config.ServerLog.Debug("Message body received from {ip} with GET, HEAD, or TRACE request, was considered an error and the request was dropped", Request.RemoteEndPoint);
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
                if (Request.HttpVersion != HttpVersion.Http11)
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
                    Config.ServerLog.Debug("Possible attempted desync, Content length + chunked encoding specified. RemoteEP: {ip}", Request.RemoteEndPoint);
                    return HttpStatusCode.BadRequest;
                }

                //Handle chunked transfer encoding (not implemented yet)
                return HttpStatusCode.NotImplemented;
            }
            //Make sure non-zero cl header was provided
            else if (parseState.ContentLength > 0)
            {
                //Open a temp buffer to store initial data in
                ISlindingWindowBuffer<byte>? initData = reader.GetReminaingData(parseState.ContentLength);
                //Setup the input stream and capture the initial data from the reader, and wrap the transport stream to read data directly
                Request.InputStream.Prepare(parseState.ContentLength, initData);
                Request.HasEntityBody = true;
            }
            //Success!
            return 0;
        }
    }
}
