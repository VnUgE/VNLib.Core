/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpHelpers.cs 
*
* HttpHelpers.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Linq;
using System.Net.Sockets;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Provides a set of HTTP helper functions
    /// </summary>
    public static partial class HttpHelpers
    {
        /// <summary>
        /// Carrage return + line feed characters used within the VNLib.Net.Http namespace to delimit http messages/lines
        /// </summary>
        public const string CRLF = "\r\n";

        public const string WebsocketRFC4122Guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public const string EVENT_STREAM_ACCEPT_TYPE = "text/event-stream";

        /// <summary>
        /// Extended <see cref="HttpRequestHeader"/> for origin header, DO NOT USE IN <see cref="WebHeaderCollection"/>
        /// </summary>
        internal const HttpRequestHeader Origin = (HttpRequestHeader)42;

        /// <summary>
        /// Extended <see cref="HttpRequestHeader"/> for Content-Disposition, DO NOT USE IN <see cref="WebHeaderCollection"/>
        /// </summary>
        internal const HttpRequestHeader ContentDisposition = (HttpRequestHeader)41;

        private static readonly Regex HttpRequestBuilderRegex = new("(?<=[a-z])([A-Z])", RegexOptions.Compiled);

        /*
         * Provides a hashable lookup table from a method string's hashcode to output 
         * an HttpMethod enum value,
         */

        private static readonly FrozenDictionary<int, HttpMethod> MethodHashLookup = HashHttpMethods();

        /*
         * Provides a constant lookup table from an MIME http request header string to a .NET 
         * enum value (with some extra support)
         */

        private static readonly FrozenDictionary<string, HttpRequestHeader> RequestHeaderLookup = new Dictionary<string, HttpRequestHeader>(StringComparer.OrdinalIgnoreCase)
        {
            {"CacheControl", HttpRequestHeader.CacheControl },
            {"Connection", HttpRequestHeader.Connection },
            {"Date", HttpRequestHeader.Date },
            {"Keep-Alive", HttpRequestHeader.KeepAlive },
            {"Pragma", HttpRequestHeader.Pragma },
            {"Trailer", HttpRequestHeader.Trailer },
            {"Transfer-Encoding", HttpRequestHeader.TransferEncoding },
            {"Upgrade", HttpRequestHeader.Upgrade },
            {"Via", HttpRequestHeader.Via },
            {"Warning", HttpRequestHeader.Warning },
            {"Allow", HttpRequestHeader.Allow },
            {"Content-Length", HttpRequestHeader.ContentLength },
            {"Content-Type", HttpRequestHeader.ContentType },
            {"Content-Encoding", HttpRequestHeader.ContentEncoding },
            {"Content-Language", HttpRequestHeader.ContentLanguage },
            {"Content-Location", HttpRequestHeader.ContentLocation },
            {"Content-Md5", HttpRequestHeader.ContentMd5 },
            {"Content-Range", HttpRequestHeader.ContentRange },
            {"Expires", HttpRequestHeader.Expires },
            {"Last-Modified", HttpRequestHeader.LastModified },
            {"Accept", HttpRequestHeader.Accept },
            {"Accept-Charset", HttpRequestHeader.AcceptCharset },
            {"Accept-Encoding", HttpRequestHeader.AcceptEncoding },
            {"Accept-Language", HttpRequestHeader.AcceptLanguage },
            {"Authorization", HttpRequestHeader.Authorization },
            {"Cookie", HttpRequestHeader.Cookie },
            {"Expect", HttpRequestHeader.Expect },
            {"From", HttpRequestHeader.From },
            {"Host", HttpRequestHeader.Host },
            {"IfMatch", HttpRequestHeader.IfMatch },
            {"If-Modified-Since", HttpRequestHeader.IfModifiedSince },
            {"If-None-Match", HttpRequestHeader.IfNoneMatch },
            {"If-Range", HttpRequestHeader.IfRange },
            {"If-Unmodified-Since", HttpRequestHeader.IfUnmodifiedSince },
            {"MaxForwards", HttpRequestHeader.MaxForwards },
            {"Proxy-Authorization", HttpRequestHeader.ProxyAuthorization },
            {"Referer", HttpRequestHeader.Referer },
            {"Range", HttpRequestHeader.Range },
            {"Te", HttpRequestHeader.Te },
            {"Translate", HttpRequestHeader.Translate },
            {"User-Agent", HttpRequestHeader.UserAgent },
            //Custom request headers
            { "Content-Disposition", ContentDisposition },
            { "origin", Origin }
        }.ToFrozenDictionary();

        /*
         * Provides a lookup table for request header hashcodes (that are hashed in 
         * the static constructor) to ouput an http request header enum value from 
         * a header string's hashcode (allows for spans to produce an enum value
         * during request parsing)
         * 
         */
        private static readonly FrozenDictionary<int, HttpRequestHeader> RequestHeaderHashLookup = ComputeCodeHashLookup(RequestHeaderLookup);

        /*
         * Provides a constant lookup table for http version hashcodes to an http
         * version enum value
         */
        private static readonly FrozenDictionary<int, HttpVersion> VersionHashLookup = new Dictionary<int, HttpVersion>()
        {
            { string.GetHashCode("HTTP/0.9", StringComparison.OrdinalIgnoreCase), HttpVersion.Http09 },
            { string.GetHashCode("HTTP/1.0", StringComparison.OrdinalIgnoreCase), HttpVersion.Http1 },
            { string.GetHashCode("HTTP/1.1", StringComparison.OrdinalIgnoreCase), HttpVersion.Http11 },
            { string.GetHashCode("HTTP/2.0", StringComparison.OrdinalIgnoreCase), HttpVersion.Http2 }
        }.ToFrozenDictionary();


        //Pre-compiled strings for all status codes for http 0.9 1, 1.1
        private static readonly FrozenDictionary<HttpStatusCode, string> V0_9_STATUS_CODES = GetStatusCodes("0.9");
        private static readonly FrozenDictionary<HttpStatusCode, string> V1_STAUTS_CODES = GetStatusCodes("1.0");
        private static readonly FrozenDictionary<HttpStatusCode, string> V1_1_STATUS_CODES = GetStatusCodes("1.1");
        private static readonly FrozenDictionary<HttpStatusCode, string> V2_STATUS_CODES = GetStatusCodes("2.0");

        private static FrozenDictionary<HttpStatusCode, string> GetStatusCodes(string version)
        {
            //Setup status code dict
            Dictionary<HttpStatusCode, string> statusCodes = new();
            //Get all status codes
            foreach (HttpStatusCode code in Enum.GetValues<HttpStatusCode>())
            {
                //Use a regex to write the status code value as a string
                statusCodes[code] = $"HTTP/{version} {(int)code} {HttpRequestBuilderRegex.Replace(code.ToString(), " $1")}";
            }
            return statusCodes.ToFrozenDictionary();
        }
        
        private static FrozenDictionary<int, HttpMethod> HashHttpMethods()
        {
            /*
             * Http methods are hashed at runtime using the HttpMethod enum
             * values, purley for compatability and automation
             */
            return ComputeCodeHashLookup(
                Enum.GetValues<HttpMethod>()
                //Exclude the not supported method
                .Except([ HttpMethod.None ])
                .Select(m => KeyValuePair.Create(m.ToString(), m))
            ).ToFrozenDictionary();
        }

        private static FrozenDictionary<int, T> ComputeCodeHashLookup<T>(IEnumerable<KeyValuePair<string, T>> enumerable)
            => enumerable.ToFrozenDictionary(
                static kv => string.GetHashCode(kv.Key, StringComparison.OrdinalIgnoreCase),
                static kv => kv.Value
            );

        /// <summary>
        /// Returns an http formatted content type string of a specified content type
        /// </summary>
        /// <param name="type">Contenty type</param>
        /// <returns>Http acceptable string representing a content type</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public static string GetContentTypeString(ContentType type) => CtToMime[type];

        /// <summary>
        /// Returns the <see cref="ContentType"/> enum value from the MIME string
        /// </summary>
        /// <param name="type">Content type from request</param>
        /// <returns><see cref="ContentType"/> of request, <see cref="ContentType.NonSupported"/> if unknown</returns>
        public static ContentType GetContentType(string type) => MimeToCt.GetValueOrDefault(type, ContentType.NonSupported);

        /// <summary>
        /// Returns the <see cref="ContentType"/> enum value from the MIME string
        /// </summary>
        /// <param name="type">Content type character span to compute the hashcode of</param>
        /// <returns><see cref="ContentType"/> of request, <see cref="ContentType.NonSupported"/> if unknown</returns>
        public static ContentType GetContentType(ReadOnlySpan<char> type)
        {
            //Compute hashcode 
            int ctHashCode = string.GetHashCode(type, StringComparison.OrdinalIgnoreCase);
            return ContentTypeHashLookup.GetValueOrDefault(ctHashCode, ContentType.NonSupported);
        }

        //Cache control string using mdn reference 
        //https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Cache-Control
        /// <summary>
        /// Builds a Cache-Control MIME content header from the specified flags
        /// </summary>
        /// <param name="type">The cache type/mode</param>
        /// <param name="maxAge">The max-age (time in seconds) argument</param>
        /// <param name="immutable">Sets the immutable argument</param>
        /// <returns>The string representation of the Cache-Control header</returns>
        public static string GetCacheString(CacheType type, int maxAge = 0, bool immutable = false)
        {
            //Rent a buffer to write header to
            Span<char> buffer = stackalloc char[128];
            //Get buffer writer for cache header
            ForwardOnlyWriter<char> sb = new(buffer);
            if ((type & CacheType.NoCache) > 0)
            {
                sb.AppendSmall("no-cache, ");
            }
            if ((type & CacheType.NoStore) > 0)
            {
                sb.AppendSmall("no-store, ");
            }
            if ((type & CacheType.Public) > 0)
            {
                sb.AppendSmall("public, ");
            }
            if ((type & CacheType.Private) > 0)
            {
                sb.AppendSmall("private, ");
            }
            if ((type & CacheType.Revalidate) > 0)
            {
                sb.AppendSmall("must-revalidate, ");
            }
            if (immutable)
            {
                sb.AppendSmall("immutable, ");
            }
            sb.AppendSmall("max-age=");
            sb.Append(maxAge);
            return sb.ToString();
        }

        /// <summary>
        /// Builds a Cache-Control MIME content header from the specified flags
        /// </summary>
        /// <param name="type">The cache type/mode</param>
        /// <param name="maxAge">The max-age argument</param>
        /// <param name="immutable">Sets the immutable argument</param>
        /// <returns>The string representation of the Cache-Control header</returns>
        public static string GetCacheString(CacheType type, TimeSpan maxAge, bool immutable = false) => GetCacheString(type, (int)maxAge.TotalSeconds, immutable);

        /// <summary>
        /// Returns an enum value of an httpmethod of an http request method string
        /// </summary>
        /// <param name="smethod">Http acceptable method type string</param>
        /// <returns>Request method, <see cref="HttpMethod.None"/> if method is malformatted or unsupported</returns>
        public static HttpMethod GetRequestMethod(ReadOnlySpan<char> smethod)
        {
            //Get the hashcode for the method "string"
            int hashCode = string.GetHashCode(smethod, StringComparison.OrdinalIgnoreCase);
            //run the lookup and return not supported if the method was not found
            return MethodHashLookup.GetValueOrDefault(hashCode, HttpMethod.None);
        }

        /// <summary>
        /// Compares the first 3 bytes of IPV4 ip address or the first 6 bytes of a IPV6. Can be used to determine if the address is local to another address
        /// </summary>
        /// <param name="first">Address to be compared</param>
        /// <param name="other">Address to be comared to first address</param>
        /// <returns>True if first 2 bytes of each address match (Big Endian)</returns>
        public static bool IsLocalSubnet(this IPAddress first, IPAddress other)
        {
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(other);

            if(first.AddressFamily != other.AddressFamily)
            {
                return false;
            }
            switch (first.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    {
                        //Alloc buffers 4 bytes for IPV4
                        Span<byte> firstBytes = stackalloc byte[4];
                        Span<byte> otherBytes = stackalloc byte[4];
                        //Write address's to the buffers 
                        if (first.TryWriteBytes(firstBytes, out _) && other.TryWriteBytes(otherBytes, out _))
                        {
                            //Compare the first 3 bytes of the first address to the second address
                            return firstBytes.StartsWith(otherBytes[..3]);
                        }
                    }
                    break;
                case AddressFamily.InterNetworkV6:
                    {
                        //Alloc buffers 8 bytes for IPV6
                        Span<byte> firstBytes = stackalloc byte[8];
                        Span<byte> otherBytes = stackalloc byte[8];
                        //Write address's to the buffers 
                        if (first.TryWriteBytes(firstBytes, out _) && other.TryWriteBytes(otherBytes, out _))
                        {
                            //Compare the first 6 bytes of the first address to the second address
                            return firstBytes.StartsWith(otherBytes[..6]);
                        }
                    }
                    break;
            }
            return false;
        }

        /// <summary>
        /// Selects a <see cref="ContentType"/> for a given file extension
        /// </summary>
        /// <param name="path">Path (including extension) of a file</param>
        /// <returns><see cref="ContentType"/> of file. Returns <see cref="ContentType.Binary"/> if extension is unknown</returns>
        public static ContentType GetContentTypeFromFile(ReadOnlySpan<char> path)
        {
            //Get the file's extension
            ReadOnlySpan<char> extention = Path.GetExtension(path);
            //Trim leading .
            extention = extention.Trim('.');
            //If the extension is defined, perform a lookup, otherwise return the default 
            return ExtensionToCt.GetValueOrDefault(extention.ToString(), ContentType.Binary);
        }

        /// <summary>
        /// Selects a runtime compiled <see cref="string"/> matching the given <see cref="HttpStatusCode"/> and <see cref="HttpVersion"/>
        /// </summary>
        /// <param name="version">Version of the response string</param>
        /// <param name="code">Status code of the response</param>
        /// <returns>The HTTP response status line matching the code and version</returns>
        public static string GetResponseString(HttpVersion version, HttpStatusCode code)
        {
            return version switch
            {
                HttpVersion.Http09 => V0_9_STATUS_CODES[code],
                HttpVersion.Http1 => V1_STAUTS_CODES[code],
                HttpVersion.Http2 => V2_STATUS_CODES[code],
                //Default to HTTP/1.1
                _ => V1_1_STATUS_CODES[code],
            };
        }

        /// <summary>
        /// Parses the mime Content-Type header value into its sub-components
        /// </summary>
        /// <param name="header">The Content-Type header value field</param>
        /// <param name="ContentType">The mime content type field</param>
        /// <param name="Charset">The mime charset</param>
        /// <param name="Boundry">The multi-part form boundry parameter</param>
        /// <returns>True if parsing the content type succeded, false otherwise</returns>
        public static bool TryParseContentType(string header, out string? ContentType, out string? Charset, out string? Boundry)
        {
            try
            {
                //Parse content type
                System.Net.Mime.ContentType ctype = new(header);
                Boundry = ctype.Boundary;
                Charset = ctype.CharSet;
                ContentType = ctype.MediaType;
                return true;
            }
            catch
//Disable warning for not using the exception, intended behavior
#pragma warning disable ERP022 // Unobserved exception in a generic exception handler.
            {
                ContentType = Charset = Boundry = null;
                //Invalid content type header value
            }
#pragma warning restore ERP022 // Unobserved exception in a generic exception handler.
            return false;
        }

        /// <summary>
        /// Parses a standard HTTP Content disposition header into its sub-components, type, name, filename (optional)
        /// </summary>
        /// <param name="header">The buffer containing the Content-Disposition header value only</param>
        /// <param name="type">The mime form type</param>
        /// <param name="name">The mime name argument</param>
        /// <param name="fileName">The mime filename</param>
        public static void ParseDisposition(ReadOnlySpan<char> header, out string? type, out string? name, out string? fileName)
        {
            //First parameter should be the type argument
            type = header.SliceBeforeParam(';').Trim().ToString();

            //Set defaults for name and filename
            name = fileName = null;

            //get the name parameter
            ReadOnlySpan<char> nameSpan = header.SliceAfterParam("name=\"");

            if (!nameSpan.IsEmpty)
            {
                //Capture the name parameter value and trim it up
                name = nameSpan.SliceBeforeParam('"').Trim().ToString();
            }

            //Check for the filename parameter
            ReadOnlySpan<char> fileNameSpan = header.SliceAfterParam("filename=\"");

            if (!fileNameSpan.IsEmpty)
            {
                //Capture the name parameter value and trim it up
                fileName = fileNameSpan.SliceBeforeParam('"').Trim().ToString();
            }
        }

        /// <summary>
        /// Performs a lookup of the specified header name to get the <see cref="HttpRequestHeader"/> enum value
        /// </summary>
        /// <param name="requestHeaderName">The value of the HTTP request header to compute</param>
        /// <returns>The <see cref="HttpRequestHeader"/> enum value of the header, or 255 if not found</returns>
        internal static HttpRequestHeader GetRequestHeaderEnumFromValue(ReadOnlySpan<char> requestHeaderName)
        {
            //Compute the hashcode from the header name
            int hashcode = string.GetHashCode(requestHeaderName, StringComparison.OrdinalIgnoreCase);
            //perform lookup
            return RequestHeaderHashLookup.GetValueOrDefault(hashcode, (HttpRequestHeader)255);
        }

        /// <summary>
        /// Gets the <see cref="HttpVersion"/> enum value from the version string
        /// </summary>
        /// <param name="httpVersion">The http header version string</param>
        /// <returns>The <see cref="HttpVersion"/> enum value, or 
        /// <see cref="HttpVersion.None"/> if the version could not be 
        /// determined
        /// </returns>
        public static HttpVersion ParseHttpVersion(ReadOnlySpan<char> httpVersion)
        {
            //Get the hashcode for the http version "string"
            int hashCode = string.GetHashCode(httpVersion.Trim(), StringComparison.OrdinalIgnoreCase);
            //return the version that matches the hashcode, or return unsupported of not found
            return VersionHashLookup.GetValueOrDefault(hashCode, HttpVersion.None);
        }
    }
}