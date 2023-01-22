/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: RedirectType.cs 
*
* RedirectType.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using System.Net;

namespace VNLib.Plugins.Essentials.Extensions
{
    /// <summary>
    /// Shortened list of <see cref="HttpStatusCode"/>s for redirecting connections
    /// </summary>
    public enum RedirectType
    {
        /// <summary>
        /// NOT-USED
        /// </summary>
        None,
        /// <summary>
        /// Sets the HTTP 301 response code for a "moved" redirect 
        /// </summary>
        Moved = 301,
        /// <summary>
        /// Sets the HTTP 302 response code for a "Found" redirect
        /// </summary>
        Found = 302, 
        /// <summary>
        /// Sets the HTTP 303 response code for a "SeeOther" redirect
        /// </summary>
        SeeOther = 303,
        /// <summary>
        /// Sets the HTTP 307 response code for a "Temporary" redirect
        /// </summary>
        Temporary = 307,
        /// <summary>
        /// Sets the HTTP 308 response code for a "Permanent" redirect
        /// </summary>
        Permanent = 308
    }
}