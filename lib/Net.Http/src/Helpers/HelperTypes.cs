/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HelperTypes.cs 
*
* HelperTypes.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// HTTP request method
    /// </summary>
    [Flags]
    public enum HttpMethod
    {
        /// <summary>
        /// default/no method found
        /// </summary>
        None,
        /// <summary>
        /// Http GET request method
        /// </summary>
        GET     = 0x01,
        /// <summary>
        /// Http POST request method
        /// </summary>
        POST    = 0x02,
        /// <summary>
        /// Http PUT request method
        /// </summary>
        PUT     = 0x04,
        /// <summary>
        /// Http OPTIONS request method
        /// </summary>
        OPTIONS = 0x08,
        /// <summary>
        /// Http HEAD request method
        /// </summary>
        HEAD    = 0x10,
        /// <summary>
        /// Http MERGE request method
        /// </summary>
        MERGE   = 0x20,
        /// <summary>
        /// Http COPY request method
        /// </summary>
        COPY    = 0x40,
        /// <summary>
        /// Http DELETE request method
        /// </summary>
        DELETE  = 0x80,
        /// <summary>
        /// Http PATCH request method
        /// </summary>
        PATCH   = 0x100,
        /// <summary>
        /// Http TRACE request method
        /// </summary>
        TRACE   = 0x200,
        /// <summary>
        /// Http MOVE request method
        /// </summary>
        MOVE    = 0x400,
        /// <summary>
        /// Http LOCK request method
        /// </summary>
        LOCK    = 0x800,
        /// <summary>
        /// Http UNLOCK request method
        /// </summary>
        UNLOCK  = 0x1000
    }

    /// <summary>
    /// HTTP protocol version
    /// </summary>
    [Flags]
    public enum HttpVersion 
    {
        /// <summary>
        /// Enum empty type
        /// </summary>
        None,
        /// <summary>
        /// Http Version 0.9
        /// </summary>
        Http09 = 0x01,
        /// <summary>
        /// Http Version 1
        /// </summary>
        Http1 = 0x02, 
        /// <summary>
        /// Http Version 1.1
        /// </summary>
        Http11 = 0x04, 
        /// <summary>
        /// Http Version 2.0
        /// </summary>
        Http2 = 0x08, 
        /// <summary>
        /// Http Version 3.0
        /// </summary>
        Http3 = 0x0A
    }

    /// <summary>
    /// HTTP response entity cache flags
    /// </summary>
    [Flags]
    public enum CacheType
    {
        /// <summary>
        /// default cache type
        /// </summary>
        None = 0x00,
        /// <summary>
        /// Set the cache header to 'No-Cache'
        /// </summary>
        NoCache = 0x01, 
        /// <summary>
        /// Sets the cache type private varibale
        /// </summary>
        Private = 0x02, 
        /// <summary>
        /// sets the cache type to public
        /// </summary>
        Public = 0x04, 
        /// <summary>
        /// Sets the nocache to no-store
        /// </summary>
        NoStore = 0x08,
        /// <summary>
        /// Sets the must-revalidate cache flag
        /// </summary>
        Revalidate = 0x10
    }
  
    /// <summary>
    /// Specifies an HTTP cookie SameSite type
    /// </summary>
    public enum CookieSameSite 
    {
        /// <summary>
        /// Cookie samesite property lax mode
        /// </summary>
        Lax, 
        /// <summary>
        /// Cookie samesite property, None mode
        /// </summary>
        None, 
        /// <summary>
        /// Cookie samesite property, strict mode
        /// </summary>
        Strict
    }
}