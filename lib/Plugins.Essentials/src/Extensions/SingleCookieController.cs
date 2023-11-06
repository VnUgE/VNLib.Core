/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: SingleCookieController.cs 
*
* SingleCookieController.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.Extensions
{
    /// <summary>
    /// Implements a sinlge cookie controller
    /// </summary>
    public class SingleCookieController : ICookieController
    {
        private readonly string _cookieName;
        private readonly TimeSpan _validFor;

        /// <summary>
        /// Creates a new <see cref="SingleCookieController"/> instance
        /// </summary>
        /// <param name="cookieName">The name of the cookie to manage</param>
        /// <param name="validFor">The max-age cookie value</param>
        public SingleCookieController(string cookieName, TimeSpan validFor)
        {
            _cookieName = cookieName;
            _validFor = validFor;
        }

        /// <summary>
        /// The domain of the cookie
        /// </summary>
        public string? Domain { get; init; }

        /// <summary>
        /// The path of the cookie
        /// </summary>
        public string? Path { get; init; }

        /// <summary>
        /// Whether the cookie is secure
        /// </summary>
        public bool Secure { get; init; }

        /// <summary>
        /// Whether the cookie is HTTP only
        /// </summary>
        public bool HttpOnly { get; init; }

        /// <summary>
        /// The SameSite policy of the cookie
        /// </summary>
        public CookieSameSite SameSite { get; init; }


        /// <summary>
        /// Optionally clears the cookie (does not force)
        /// </summary>
        /// <param name="entity">The entity to clear the cookie for</param>
        public void ExpireCookie(HttpEntity entity) => ExpireCookie(entity, false);

        ///<inheritdoc/>
        public void ExpireCookie(HttpEntity entity, bool force)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));
            SetCookieInternal(entity, string.Empty, force);
        }

        ///<inheritdoc/>
        public string? GetCookie(HttpEntity entity)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));
            return entity.Server.RequestCookies.GetValueOrDefault(_cookieName);
        }

        ///<inheritdoc/>
        public void SetCookie(HttpEntity entity, string value)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));
            SetCookieInternal(entity, value, true);
        }

        private void SetCookieInternal(HttpEntity entity, string value, bool force)
        {
            //Only set cooke if already exists or force is true
            if (entity.Server.RequestCookies.ContainsKey(value) || force)
            {
                //Build and set cookie
                HttpCookie cookie = new(_cookieName, value)
                {
                    Secure = Secure,
                    HttpOnly = HttpOnly,
                    ValidFor = _validFor,
                    SameSite = SameSite,
                    Path = Path,
                    Domain = Domain
                };

                entity.Server.SetCookie(in cookie);
            }
        }
    }
}