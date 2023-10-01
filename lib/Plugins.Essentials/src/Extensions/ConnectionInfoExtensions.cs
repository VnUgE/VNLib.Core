/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ConnectionInfoExtensions.cs 
*
* ConnectionInfoExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Utils.Extensions;

#nullable enable

namespace VNLib.Plugins.Essentials.Extensions
{

    /// <summary>
    /// Provides <see cref="ConnectionInfo"/> extension methods
    /// for common use cases
    /// </summary>
    public static class IConnectionInfoExtensions
    {
        public const string SEC_HEADER_MODE = "Sec-Fetch-Mode";
        public const string SEC_HEADER_SITE = "Sec-Fetch-Site";
        public const string SEC_HEADER_USER = "Sec-Fetch-User";
        public const string SEC_HEADER_DEST = "Sec-Fetch-Dest";
        public const string X_FORWARDED_FOR_HEADER = "x-forwarded-for";
        public const string X_FORWARDED_PROTO_HEADER = "x-forwarded-proto";
        public const string DNT_HEADER = "dnt";

        /// <summary>
        /// Cache-Control header value for disabling cache
        /// </summary>
        public static readonly string NO_CACHE_RESPONSE_HEADER_VALUE = HttpHelpers.GetCacheString(CacheType.NoCache | CacheType.NoStore | CacheType.Revalidate);
    

        /// <summary>
        /// Determines if the client accepts the response content type
        /// </summary>
        /// <param name="server"></param>
        /// <param name="type">The desired content type</param>
        /// <returns>True if the client accepts the content type, false otherwise</returns>
        public static bool Accepts(this IConnectionInfo server, ContentType type)
        {
            //Get the content type string from he specified content type
            string contentType = HttpHelpers.GetContentTypeString(type);
            return Accepts(server, contentType);
        }

        /// <summary>
        /// Determines if the client accepts the response content type
        /// </summary>
        /// <param name="server"></param>
        /// <param name="contentType">The desired content type</param>
        /// <returns>True if the client accepts the content type, false otherwise</returns>
        public static bool Accepts(this IConnectionInfo server, string contentType)
        {
            if (AcceptsAny(server))
            {
                return true;
            }

            //If client accepts exact requested encoding 
            if (server.Accept.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            //Search for the content-sub-type 

            //Get prinary side of mime type
            ReadOnlySpan<char> primary = contentType.AsSpan().SliceBeforeParam('/');

            foreach(string accept in server.Accept)
            {
                //The the accept subtype
                ReadOnlySpan<char> ctSubType = accept.AsSpan().SliceBeforeParam('/');

                //See if accepts any subtype, or the primary sub-type matches
                if (ctSubType[0] == '*' || ctSubType.Equals(primary, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if the connection accepts any content type
        /// </summary>
        /// <returns>true if the connection accepts any content typ, false otherwise</returns>
        private static bool AcceptsAny(this IConnectionInfo server)
        {
            //Accept any if no accept header was present, or accept all value */*
            return server.Accept.Count == 0 || server.Accept.Where(static t => t.StartsWith("*/*", StringComparison.OrdinalIgnoreCase)).Any();
        }

        /// <summary>
        /// Gets the <see cref="HttpRequestHeader.IfModifiedSince"/> header value and converts its value to a datetime value
        /// </summary>
        /// <returns>The if modified-since header date-time, null if the header was not set or the value was invalid</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTimeOffset? LastModified(this IConnectionInfo server)
        {
            //Get the if-modified-since header
            string? ifModifiedSince = server.Headers[HttpRequestHeader.IfModifiedSince];
            //Make sure tis set and try to convert it to a date-time structure
            return DateTimeOffset.TryParse(ifModifiedSince, out DateTimeOffset d) ? d : null;
        }

        /// <summary>
        /// Sets the last-modified response header value
        /// </summary>
        /// <param name="server"></param>
        /// <param name="value">Time the entity was last modified</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LastModified(this IConnectionInfo server, DateTimeOffset value)
        {
            server.Headers[HttpResponseHeader.LastModified] = value.ToString("R");
        }

        /// <summary>
        /// Is the connection requesting cors
        /// </summary>
        /// <returns>true if the user-agent specified the cors security header</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCors(this IConnectionInfo server) => "cors".Equals(server.Headers[SEC_HEADER_MODE], StringComparison.OrdinalIgnoreCase);
        /// <summary>
        /// Determines if the User-Agent specified "cross-site" in the Sec-Site header, OR 
        /// the connection spcified an origin header and the origin's host does not match the 
        /// requested host
        /// </summary>
        /// <returns>true if the request originated from a site other than the current one</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCrossSite(this IConnectionInfo server)
        {
            return "cross-site".Equals(server.Headers[SEC_HEADER_SITE], StringComparison.OrdinalIgnoreCase) 
                || (server.Origin != null && !server.RequestUri.DnsSafeHost.Equals(server.Origin.DnsSafeHost, StringComparison.Ordinal));
        }
        /// <summary>
        /// Is the connection user-agent created, or automatic
        /// </summary>
        /// <param name="server"></param>
        /// <returns>true if sec-user header was set to "?1"</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUserInvoked(this IConnectionInfo server) => "?1".Equals(server.Headers[SEC_HEADER_USER], StringComparison.OrdinalIgnoreCase);
        /// <summary>
        /// Was this request created from normal user navigation
        /// </summary>
        /// <returns>true if sec-mode set to "navigate"</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNavigation(this IConnectionInfo server) => "navigate".Equals(server.Headers[SEC_HEADER_MODE], StringComparison.OrdinalIgnoreCase);
        /// <summary>
        /// Determines if the client specified "no-cache" for the cache control header, signalling they do not wish to cache the entity
        /// </summary>
        /// <returns>True if <see cref="HttpRequestHeader.CacheControl"/> contains the string "no-cache", false otherwise</returns>
        public static bool NoCache(this IConnectionInfo server)
        {
            string? cache_header = server.Headers[HttpRequestHeader.CacheControl];
            return !string.IsNullOrWhiteSpace(cache_header) && cache_header.Contains("no-cache", StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Sets the response cache headers to match the requested caching type. Does not check against request headers
        /// </summary>
        /// <param name="server"></param>
        /// <param name="type">One or more <see cref="CacheType"/> flags that identify the way the entity can be cached</param>
        /// <param name="maxAge">The max age the entity is valid for</param>
        public static void SetCache(this IConnectionInfo server, CacheType type, TimeSpan maxAge)
        {
            //If no cache flag is set, set the pragma header to no-cache
            if((type & CacheType.NoCache) > 0)
            {
                server.Headers[HttpResponseHeader.Pragma] = "no-cache";
            }
            //Set the cache hader string using the http helper class
            server.Headers[HttpResponseHeader.CacheControl] = HttpHelpers.GetCacheString(type, maxAge);
        }
        /// <summary>
        /// Sets the Cache-Control response header to <see cref="NO_CACHE_RESPONSE_HEADER_VALUE"/>
        /// and the pragma response header to 'no-cache'
        /// </summary>
        /// <param name="server"></param>
        public static void SetNoCache(this IConnectionInfo server)
        {
            //Set default nocache string
            server.Headers[HttpResponseHeader.CacheControl] = NO_CACHE_RESPONSE_HEADER_VALUE;
            server.Headers[HttpResponseHeader.Pragma] = "no-cache";
        }

        /// <summary>
        /// Gets a value indicating whether the port number in the request is equivalent to the port number 
        /// on the local server. 
        /// </summary>
        /// <returns>True if the port number in the <see cref="ConnectionInfo.RequestUri"/> matches the 
        /// <see cref="ConnectionInfo.LocalEndpoint"/> port false if they do not match
        /// </returns>
        /// <remarks>
        /// Users should call this method to help prevent port based attacks if your
        /// code relies on the port number of the <see cref="ConnectionInfo.RequestUri"/>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnpointPortsMatch(this IConnectionInfo server)
        {
            return server.RequestUri.Port == server.LocalEndpoint.Port;
        }
        /// <summary>
        /// Determines if the host of the current request URI matches the referer header host
        /// </summary>
        /// <returns>True if the request host and the referer host paremeters match, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RefererMatch(this IConnectionInfo server)
        {
            return server.RequestUri.DnsSafeHost.Equals(server.Referer?.DnsSafeHost, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Expires a client's cookie
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="domain"></param>
        /// <param name="path"></param>
        /// <param name="sameSite"></param>
        /// <param name="secure"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExpireCookie(this IConnectionInfo server, string name, string domain = "", string path = "/", CookieSameSite sameSite = CookieSameSite.None, bool secure = false)
        {
            server.SetCookie(name, string.Empty, domain, path, TimeSpan.Zero, sameSite, false, secure);
        }
        /// <summary>
        /// Sets a cookie with an infinite (session life-span)
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="domain"></param>
        /// <param name="path"></param>
        /// <param name="sameSite"></param>
        /// <param name="httpOnly"></param>
        /// <param name="secure"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSessionCookie(
            this IConnectionInfo server,
            string name,
            string value,
            string domain = "",
            string path = "/",
            CookieSameSite sameSite = CookieSameSite.None,
            bool httpOnly = false,
            bool secure = false)
        {
            server.SetCookie(name, value, domain, path, TimeSpan.MaxValue, sameSite, httpOnly, secure);
        }

        /// <summary>
        /// Sets a cookie with an infinite (session life-span)
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="domain"></param>
        /// <param name="path"></param>
        /// <param name="sameSite"></param>
        /// <param name="httpOnly"></param>
        /// <param name="secure"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCookie(
            this IConnectionInfo server,
            string name,
            string value,
            TimeSpan expires,
            string domain = "",
            string path = "/",
            CookieSameSite sameSite = CookieSameSite.None,
            bool httpOnly = false,
            bool secure = false)
        {
            server.SetCookie(name, value, domain, path, expires, sameSite, httpOnly, secure);
        }


        /// <summary>
        /// Sets the desired http cookie for the current connection
        /// </summary>
        /// <param name="server"></param>
        /// <param name="cookie">The cookie to set for the server</param>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCookie(this IConnectionInfo server, in HttpCookie cookie)
        {
            //Cookie name is required
            if(string.IsNullOrWhiteSpace(cookie.Name))
            {
                throw new ArgumentException("A nonn-null cookie name is required");
            }

            //Set the cookie
            server.SetCookie(cookie.Name,
                             cookie.Value,
                             cookie.Domain,
                             cookie.Path,
                             cookie.ValidFor,
                             cookie.SameSite,
                             cookie.HttpOnly,
                             cookie.Secure);
        }

        /// <summary>
        /// Is the current connection a "browser" ?
        /// </summary>
        /// <param name="server"></param>
        /// <returns>true if the user agent string contains "Mozilla" and does not contain "bot", false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBrowser(this IConnectionInfo server)
        {
            //Get user-agent and determine if its a browser
            return server.UserAgent != null && !server.UserAgent.Contains("bot", StringComparison.OrdinalIgnoreCase) && server.UserAgent.Contains("Mozilla", StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Determines if the current connection is the loopback/internal network adapter
        /// </summary>
        /// <param name="server"></param>
        /// <returns>True of the connection was made from the local machine</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLoopBack(this IConnectionInfo server)
        {
            IPAddress realIp = server.GetTrustedIp();
            return IPAddress.Any.Equals(realIp) || IPAddress.Loopback.Equals(realIp);
        }
       
        /// <summary>
        /// Did the connection set the dnt header?
        /// </summary>
        /// <returns>true if the connection specified the dnt header, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DNT(this IConnectionInfo server) => !string.IsNullOrWhiteSpace(server.Headers[DNT_HEADER]);

        /// <summary>
        /// Determins if the current connection is behind a trusted downstream server
        /// </summary>
        /// <param name="server"></param>
        /// <returns>True if the connection came from a trusted downstream server, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBehindDownStreamServer(this IConnectionInfo server)
        {
            //See if there is an ambient event processor 
            EventProcessor? ev = EventProcessor.Current;
            //See if the connection is coming from an downstream server
            return ev != null && ev.Options.DownStreamServers.Contains(server.RemoteEndpoint.Address);
        }

        /// <summary>
        /// Gets the real IP address of the request if behind a trusted downstream server, otherwise returns the transport remote ip address
        /// </summary>
        /// <param name="server"></param>
        /// <returns>The real ip of the connection</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IPAddress GetTrustedIp(this IConnectionInfo server) => GetTrustedIp(server, server.IsBehindDownStreamServer());
        /// <summary>
        /// Gets the real IP address of the request if behind a trusted downstream server, otherwise returns the transport remote ip address
        /// </summary>
        /// <param name="server"></param>
        /// <param name="isTrusted"></param>
        /// <returns>The real ip of the connection</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IPAddress GetTrustedIp(this IConnectionInfo server, bool isTrusted)
        {
            //If the connection is not trusted, then ignore header parsing
            if (isTrusted)
            {
                //Nginx sets a header identifying the remote ip address so parse it
                string? real_ip = server.Headers[X_FORWARDED_FOR_HEADER];
                //If the real-ip header is set, try to parse is and return the address found, otherwise return the remote ep 
                return !string.IsNullOrWhiteSpace(real_ip) && IPAddress.TryParse(real_ip, out IPAddress? addr) ? addr : server.RemoteEndpoint.Address;
            }
            else
            {
                return server.RemoteEndpoint.Address;
            }
        }
        
        /// <summary>
        /// Gets a value that determines if the connection is using tls, locally 
        /// or behind a trusted downstream server that is using tls.
        /// </summary>
        /// <param name="server"></param>
        /// <returns>True if the connection is secure, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSecure(this IConnectionInfo server)
        {
            //Get value of the trusted downstream server
            return IsSecure(server, server.IsBehindDownStreamServer());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsSecure(this IConnectionInfo server, bool isTrusted)
        {
            //If the connection is not trusted, then ignore header parsing
            if (isTrusted)
            {
                //Standard https protocol header
                string? protocol = server.Headers[X_FORWARDED_PROTO_HEADER];
                //If the header is set and equals https then tls is being used
                return string.IsNullOrWhiteSpace(protocol) ? server.IsSecure : "https".Equals(protocol, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return server.IsSecure;
            }
        }

        /// <summary>
        /// Was the connection made on a local network to the server? NOTE: Use with caution
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLocalConnection(this IConnectionInfo server) => server.LocalEndpoint.Address.IsLocalSubnet(server.GetTrustedIp());

        /// <summary>
        /// Get a cookie from the current request
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name">Name/ID of cookie</param>
        /// <param name="cookieValue">Is set to cookie if found, or null if not</param>
        /// <returns>True if cookie exists and was retrieved</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetCookie(this IConnectionInfo server, string name, [NotNullWhen(true)] out string? cookieValue)
        {
            //Try to get a cookie from the request
            return server.RequestCookies.TryGetValue(name, out cookieValue);
        }
    }
}