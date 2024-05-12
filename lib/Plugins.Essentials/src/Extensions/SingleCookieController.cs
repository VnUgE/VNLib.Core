/*
* Copyright (c) 2024 Vaughn Nugent
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
    /// <param name="Name">The name of the cookie to manage</param>
    /// <param name="ValidFor">The max-age cookie value</param>
    public record class SingleCookieController(string Name, TimeSpan ValidFor) : ICookieController
    {
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
        public void ExpireCookie(IHttpEvent entity) => ExpireCookie(entity, false);

        ///<inheritdoc/>
        public void ExpireCookie(IHttpEvent entity, bool force)
        {
            ArgumentNullException.ThrowIfNull(entity);
            SetCookieInternal(entity, string.Empty, force);
        }

        ///<inheritdoc/>
        public string? GetCookie(IHttpEvent entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            return entity.Server.RequestCookies.GetValueOrDefault(Name);
        }

        ///<inheritdoc/>
        public void SetCookie(IHttpEvent entity, string value)
        {
            ArgumentNullException.ThrowIfNull(entity);
            SetCookieInternal(entity, value, true);
        }

        private void SetCookieInternal(IHttpEvent entity, string value, bool force)
        {
            //Only set cooke if already exists or force is true
            if (entity.Server.RequestCookies.ContainsKey(Name) || force)
            {
                HttpResponseCookie cookie = new(Name)
                {
                    Value = value,
                    Domain = Domain,
                    Path = Path,
                    MaxAge = ValidFor,
                    IsSession = ValidFor == TimeSpan.MaxValue,
                    SameSite = SameSite,
                    HttpOnly = HttpOnly,
                    Secure = Secure | entity.Server.CrossOrigin,
                };

                entity.Server.SetCookie(in cookie);
            }
        }
    }
}