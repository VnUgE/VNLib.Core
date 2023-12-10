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

namespace VNLib.Net.Http.Core
{

    /// <summary>
    /// A mutable http connection state structure that stores HTTP 
    /// status information
    /// </summary>
    internal struct HttpRequestState
    {
        /// <summary>
        /// A value indicating if the client's request had an Expect-100-Continue header
        /// </summary>
        internal bool Expect;

        /// <summary>
        /// A value that indicates if HTTP keepalive is desired by a client and is respected
        /// by the server
        /// </summary>
        internal bool KeepAlive;

        /// <summary>
        /// A value indicating whether the connection contained a request entity body.
        /// </summary>
        internal bool HasEntityBody;

        /// <summary>
        /// The connection HTTP version determined by the server.
        /// </summary>
        public HttpVersion HttpVersion;

        /// <summary>
        /// The requested HTTP method
        /// </summary>
        public HttpMethod Method;

        /// <summary>
        /// Request wide content type of a request entity body if not using FormData
        /// </summary>
        public ContentType ContentType;

        /// <summary>
        /// Conent range requested ranges, that are parsed into a start-end tuple
        /// </summary>
        public HttpRange Range;

        /// <summary>
        /// The number of uploaded files in the request
        /// </summary>
        public int UploadCount;

        /// <summary>
        /// request's user-agent string
        /// </summary>
        public string? UserAgent;
        
        /// <summary>
        /// Boundry header value if reuqest send data using MIME mulit-part form data
        /// </summary>
        public string? Boundry;
        
        /// <summary>
        /// Request entity body charset if parsed during content-type parsing
        /// </summary>
        public string? Charset;

        /// <summary>
        /// The requested resource location url
        /// </summary>
        public Uri Location;

        /// <summary>
        /// The value of the origin header if one was sent
        /// </summary>
        public Uri? Origin;

        /// <summary>
        /// The url value of the referer header if one was sent
        /// </summary>
        public Uri? Referrer;

        /// <summary>
        /// The connection's remote endpoint (ip/port) captured from transport
        /// </summary>
        public IPEndPoint RemoteEndPoint;

        /// <summary>
        /// The connection's local endpoint (the server's transport socket address)
        /// </summary>
        public IPEndPoint LocalEndPoint;

    }
}