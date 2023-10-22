/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IConnectionInfo.cs 
*
* IConnectionInfo.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Text;
using System.Collections.Generic;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents a client's connection info as interpreted by the current server
    /// </summary>
    /// <remarks>Methods and properties are undefined when <see cref="IWebRoot.ClientConnectedAsync(IHttpEvent)"/> returns</remarks>
    public interface IConnectionInfo
    {
        /// <summary>
        /// Full request uri of current connection
        /// </summary>
        Uri RequestUri { get; }

        /// <summary>
        /// Current request path. Shortcut to <seealso cref="RequestUri"/> <see cref="Uri.LocalPath"/>
        /// </summary>
        string Path => RequestUri.LocalPath;

        /// <summary>
        /// Current connection's user-agent header, (may be null if no user-agent header found)
        /// </summary>
        string? UserAgent { get; }

        /// <summary>
        /// Current connection's headers
        /// </summary>
        IHeaderCollection Headers { get; }

        /// <summary>
        /// A value that indicates if the connection's origin header was set and it's
        /// authority segment does not match the <see cref="RequestUri"/> authority
        /// segment.
        /// </summary>
        bool CrossOrigin { get; }

        /// <summary>
        /// Is the current connecion a websocket request
        /// </summary>
        bool IsWebSocketRequest { get; }

        /// <summary>
        /// Request specified content-type
        /// </summary>
        ContentType ContentType { get; }

        /// <summary>
        /// Current request's method
        /// </summary>
        HttpMethod Method { get; }

        /// <summary>
        /// The current connection's HTTP protocol version
        /// </summary>
        HttpVersion ProtocolVersion { get; }

        /// <summary>
        /// Origin header of current connection if specified, null otherwise
        /// </summary>
        Uri? Origin { get; }

        /// <summary>
        /// Referer header of current connection if specified, null otherwise
        /// </summary>
        Uri? Referer { get; }

        /// <summary>
        /// The parsed range header, or -1,-1 if the range header was not set
        /// </summary>
        Tuple<long, long>? Range { get; }

        /// <summary>
        /// The server endpoint that accepted the connection
        /// </summary>
        IPEndPoint LocalEndpoint { get; }

        /// <summary>
        /// The raw <see cref="IPEndPoint"/> of the downstream connection. 
        /// </summary>
        IPEndPoint RemoteEndpoint { get; }

        /// <summary>
        /// The encoding type used to decode and encode character data to and from the current client
        /// </summary>
        Encoding Encoding { get; }

        /// <summary>
        /// A <see cref="IReadOnlyDictionary{TKey, TValue}"/> of client request cookies
        /// </summary>
        IReadOnlyDictionary<string, string> RequestCookies { get; }

        /// <summary>
        /// Gets an <see cref="IEnumerator{T}"/> for the parsed accept header values
        /// </summary>
        IReadOnlyCollection<string> Accept { get; }

        /// <summary>
        /// Gets the underlying transport security information for the current connection
        /// </summary>
        ref readonly TransportSecurityInfo? GetTransportSecurityInfo();
       

        /// <summary>
        /// Adds a new cookie to the response. If a cookie with the same name and value
        /// has been set, the old cookie is replaced with the new one.
        /// </summary>
        /// <param name="name">Cookie name/id</param>
        /// <param name="value">Value to be stored in cookie</param>
        /// <param name="domain">Domain for cookie to operate</param>
        /// <param name="path">Path to store cookie</param>
        /// <param name="Expires">Timespan representing how long the cookie should exist</param>
        /// <param name="sameSite">Samesite attribute, Default = Lax</param>
        /// <param name="httpOnly">Specify the HttpOnly flag</param>
        /// <param name="secure">Specify the Secure flag</param>
        void SetCookie(string name, string value, string? domain, string? path, TimeSpan Expires, CookieSameSite sameSite, bool httpOnly, bool secure);
    }
}