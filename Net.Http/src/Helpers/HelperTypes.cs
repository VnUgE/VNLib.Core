/*
* Copyright (c) 2022 Vaughn Nugent
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
    [Flags]
    public enum HttpMethod
    {
        NOT_SUPPORTED,
        GET     = 0x01,
        POST    = 0x02,
        PUT     = 0x04,
        OPTIONS = 0x08,
        HEAD    = 0x10,
        MERGE   = 0x20,
        COPY    = 0x40,
        DELETE  = 0x80,
        PATCH   = 0x100,
        TRACE   = 0x200,
        MOVE    = 0x400,
        LOCK    = 0x800
    }
    /// <summary>
    /// HTTP protocol version
    /// </summary>
    [Flags]
    public enum HttpVersion 
    {
        NotSupported,
        Http1 = 0x01, 
        Http11 = 0x02, 
        Http2 = 0x04, 
        Http09 = 0x08
    }
    /// <summary>
    /// HTTP response entity cache flags
    /// </summary>
    [Flags]
    public enum CacheType
    {
        Ignore = 0x00,
        NoCache = 0x01, 
        Private = 0x02, 
        Public = 0x04, 
        NoStore = 0x08,
        Revalidate = 0x10
    }
  
    /// <summary>
    /// Specifies an HTTP cookie SameSite type
    /// </summary>
    public enum CookieSameSite 
    {
        Lax, None, SameSite
    }
   
    /// <summary>
    /// Low level 301 "hard" redirect
    /// </summary>
    public class Redirect
    {
        public readonly string Url;
        public readonly Uri RedirectUrl;
        /// <summary>
        /// Quickly redirects a url to another url before sessions are established
        /// </summary>
        /// <param name="url">Url to redirect on</param>
        /// <param name="redirecturl">Url to redirect to</param>
        public Redirect(string url, string redirecturl)
        {
            Url = url;
            RedirectUrl = new(redirecturl);
        }
    }
}