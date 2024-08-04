/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: SpecialHeaders.cs 
*
* SpecialHeaders.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System.Runtime.CompilerServices;

using VNLib.Net.Http;

namespace VNLib.WebServer
{
    /// <summary>
    /// Contains constants for internal/special headers by their name
    /// </summary>
    internal static class SpecialHeaders
    {
        public const string ContentSecPolicy = "Content-Security-Policy";
        public const string XssProtection = "X-XSS-Protection";
        public const string XContentOption = "X-Content-Type-Options";
        public const string Hsts = "Strict-Transport-Security";
        public const string Server = "Server";

        /// <summary>
        /// An array of the special headers to quickly compare against
        /// </summary>
        public static string[] SpecialHeader =
        {
            ContentSecPolicy,
            XssProtection,
            XContentOption,
            Hsts,
            Server
        };

        /// <summary>
        /// Appends the special header by the given name, if it is present 
        /// in the current configruation's special headers
        /// </summary>
        /// <param name="config"></param>
        /// <param name="server">The connection to set the response headers on</param>
        /// <param name="headerName">The name of the special header to get</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TrySetSpecialHeader(this VirtualHostConfig config, IConnectionInfo server, string headerName)
        {
            //Try to get the special header value, 
            if(config.SpecialHeaders.TryGetValue(headerName, out string? headerValue))
            {
                server.Headers.Append(headerName, headerValue);
            }
        }
    }
}
