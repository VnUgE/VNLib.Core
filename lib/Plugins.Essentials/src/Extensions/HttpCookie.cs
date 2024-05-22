/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: HttpCookie.cs 
*
* HttpCookie.cs is part of VNLib.Plugins.Essentials which is part of
* the larger VNLib collection of libraries and utilities.
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

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.Extensions
{
    /// <summary>
    /// A structure for defining an HTTP cookie 
    /// </summary>
    /// <param name="Name">The cookie name</param>
    /// <param name="Value">The cookie value</param>
    [Obsolete("Obsolete in favor of HttpResponseCookie")]
    public readonly record struct HttpCookie (string Name, string Value)
    {
        /// <summary>
        /// The length of time the cookie is valid for
        /// </summary>
        public readonly TimeSpan ValidFor { get; init; } = TimeSpan.MaxValue;
        /// <summary>
        /// The cookie's domain parameter. If null, is not set in the
        /// Set-Cookie header.
        /// </summary>
        public readonly string? Domain { get; init; } = null;
        /// <summary>
        /// The cookies path parameter. If null, is not 
        /// set in the Set-Cookie header.
        /// </summary>
        public readonly string? Path { get; init; } = "/";
        /// <summary>
        /// The cookie's same-site parameter. Default is <see cref="CookieSameSite.None"/>
        /// </summary>
        public readonly CookieSameSite SameSite { get; init; } = CookieSameSite.None;
        /// <summary>
        /// Sets the cookie's HttpOnly parameter. Default is false. When false, does not 
        /// set the HttpOnly paramter in the Set-Cookie header.
        /// </summary>
        public readonly bool HttpOnly { get; init; } = false;
        /// <summary>
        /// Sets the cookie's Secure parameter. Default is false. When false, does not
        /// set the Secure parameter in the Set-Cookie header.
        /// </summary>
        public readonly bool Secure { get; init; } = false;

        /// <summary>
        /// Configures the default <see cref="HttpCookie"/>
        /// </summary>
        public HttpCookie():this(string.Empty, string.Empty)
        { }
    }
}