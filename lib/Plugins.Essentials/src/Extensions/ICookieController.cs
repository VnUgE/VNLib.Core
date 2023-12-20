/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ICookieController.cs 
*
* ICookieController.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.Extensions
{
    /// <summary>
    /// Manges a single cookie for connections
    /// </summary>
    public interface ICookieController
    {
        /// <summary>
        /// Sets the cookie value for the given entity
        /// </summary>
        /// <param name="entity">The http connection to set the cookie value for</param>
        /// <param name="value">The cookie value</param>
        void SetCookie(IHttpEvent entity, string value);

        /// <summary>
        /// Gets the cookie value for the given entity
        /// </summary>
        /// <param name="entity">The entity to get the cookie for</param>
        /// <returns>The cookie value if set, null otherwise</returns>
        string? GetCookie(IHttpEvent entity);

        /// <summary>
        /// Expires an existing request cookie for the given entity, avoiding 
        /// setting the response cookie unless necessary
        /// </summary>
        /// <param name="entity">The http connection to expire the cookie on</param>
        /// <param name="force">Forcibly set the response cookie regardless of it's existence</param>
        void ExpireCookie(IHttpEvent entity, bool force);
    }
}