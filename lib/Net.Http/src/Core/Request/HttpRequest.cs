/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpRequest.cs 
*
* HttpRequest.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Collections.Generic;
using System.Security.Authentication;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http.Core
{

    internal sealed class HttpRequest : IHttpLifeCycle
#if DEBUG
        ,IStringSerializeable
#endif
    {
        public readonly VnWebHeaderCollection Headers;
        public readonly Dictionary<string, string> Cookies;
        public readonly List<string> Accept;
        public readonly List<string> AcceptLanguage;
        public readonly HttpRequestBody RequestBody;

        public HttpVersion HttpVersion { get; set; }
        public HttpMethod Method { get; set; }
        public string? UserAgent { get; set; }
        public string? Boundry { get; set; }
        public ContentType ContentType { get; set; }
        public string? Charset { get; set; }
        public Uri Location { get; set; }
        public Uri? Origin { get; set; }
        public Uri? Referrer { get; set; }
        internal bool KeepAlive { get; set; }
        public IPEndPoint RemoteEndPoint { get; set; }
        public IPEndPoint LocalEndPoint { get; set; }
        public SslProtocols EncryptionVersion { get; set; }
        public Tuple<long, long>? Range { get; set; }
        /// <summary>
        /// A value indicating whether the connection contained a request entity body.
        /// </summary>
        public bool HasEntityBody { get; set; }
        /// <summary>
        /// A transport stream wrapper that is positioned for reading
        /// the entity body from the input stream
        /// </summary>
        public HttpInputStream InputStream { get; }
        /// <summary>
        /// A value indicating if the client's request had an Expect-100-Continue header
        /// </summary>
        public bool Expect { get; set; }

#nullable disable
        public HttpRequest(IHttpContextInformation contextInfo)
        {
            //Create new collection for headers
            Headers = new();
            //Create new collection for request cookies
            Cookies = new();
            //New list for accept
            Accept = new();
            AcceptLanguage = new();
            //New reusable input stream
            InputStream = new(contextInfo);
            RequestBody = new();
        }
       

        public void OnPrepare()
        {}

        public void OnRelease()
        {}

        public void OnNewRequest()
        {
            //Set to defaults
            ContentType = ContentType.NonSupported;
            EncryptionVersion = default;
            Method = HttpMethod.None;
            HttpVersion = HttpVersion.None;
        }

        public void OnComplete()
        {
            //release the input stream
            InputStream.OnComplete();
            RequestBody.OnComplete();
            //Make sure headers, cookies, and accept are cleared for reuse
            Headers.Clear();
            Cookies.Clear();
            Accept.Clear();
            AcceptLanguage.Clear();
            //Clear request flags
            this.Expect = false;
            this.KeepAlive = false;
            this.HasEntityBody = false;
            //We need to clean up object refs
            this.Boundry = default;
            this.Charset = default;
            this.LocalEndPoint = default;
            this.Location = default;
            this.Origin = default;
            this.Referrer = default;
            this.RemoteEndPoint = default;
            this.UserAgent = default;
            this.Range = default;
            //We are all set to reuse the instance
        }


#if DEBUG

        public string Compile()
        {
            //Alloc char buffer for compilation
            using IMemoryHandle<char> buffer = MemoryUtil.SafeAlloc<char>(16 * 1024);

            ForwardOnlyWriter<char> writer = new(buffer.Span);
            
            Compile(ref writer);
            
            return writer.ToString();
        }

        public void Compile(ref ForwardOnlyWriter<char> writer)
        {
            //Request line
            writer.Append(Method.ToString());
            writer.Append(" ");
            writer.Append(Location?.PathAndQuery);
            writer.Append(" HTTP/");
            switch (HttpVersion)
            {
                case HttpVersion.None:
                    writer.Append("Unsuppored Http version");
                    break;
                case HttpVersion.Http1:
                    writer.Append("1.0");
                    break;
                case HttpVersion.Http11:
                    writer.Append("1.1");
                    break;
                case HttpVersion.Http2:
                    writer.Append("2.0");
                    break;
                case HttpVersion.Http09:
                    writer.Append("0.9");
                    break;
            }

            writer.Append("\r\n");

            //write host
            writer.Append("Host: ");
            writer.Append(Location?.Authority);
            writer.Append("\r\n");

            //Write headers
            foreach (string header in Headers.Keys)
            {
                writer.Append(header);
                writer.Append(": ");
                writer.Append(Headers[header]);
                writer.Append("\r\n");
            }

            //Write cookies
            foreach (string cookie in Cookies.Keys)
            {
                writer.Append("Cookie: ");
                writer.Append(cookie);
                writer.Append("=");
                writer.Append(Cookies[cookie]);
                writer.Append("\r\n");
            }

            //Write accept
            if (Accept.Count > 0)
            {
                writer.Append("Accept: ");
                foreach (string accept in Accept)
                {
                    writer.Append(accept);
                    writer.Append(", ");
                }
                writer.Append("\r\n");
            }
            //Write accept language
            if (AcceptLanguage.Count > 0)
            {
                writer.Append("Accept-Language: ");
                foreach (string acceptLanguage in AcceptLanguage)
                {
                    writer.Append(acceptLanguage);
                    writer.Append(", ");
                }
                writer.Append("\r\n");
            }
            //Write user agent
            if (UserAgent != null)
            {
                writer.Append("User-Agent: ");
                writer.Append(UserAgent);
                writer.Append("\r\n");
            }
            //Write content type
            if (ContentType != ContentType.NonSupported)
            {
                writer.Append("Content-Type: ");
                writer.Append(HttpHelpers.GetContentTypeString(ContentType));
                writer.Append("\r\n");
            }
            //Write content length
            if (ContentType != ContentType.NonSupported)
            {
                writer.Append("Content-Length: ");
                writer.Append(InputStream.Length);
                writer.Append("\r\n");
            }
            if (KeepAlive)
            {
                writer.Append("Connection: keep-alive\r\n");
            }
            if (Expect)
            {
                writer.Append("Expect: 100-continue\r\n");
            }
            if(Origin != null)
            {
                writer.Append("Origin: ");
                writer.Append(Origin.ToString());
                writer.Append("\r\n");
            }
            if (Referrer != null)
            {
                writer.Append("Referrer: ");
                writer.Append(Referrer.ToString());
                writer.Append("\r\n");
            }
            writer.Append("from ");
            writer.Append(RemoteEndPoint.ToString());
            writer.Append("\r\n");
            writer.Append("Received on ");
            writer.Append(LocalEndPoint.ToString());
            //Write end of headers
            writer.Append("\r\n");
        }

        public ERRNO Compile(in Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }

        public override string ToString() => Compile();
#else

        public override string ToString() => "HTTP Library was compiled without a DEBUG directive, request logging is not available";

#endif
    }
}