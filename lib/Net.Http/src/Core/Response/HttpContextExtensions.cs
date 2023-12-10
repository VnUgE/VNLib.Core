/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpContextExtensions.cs 
*
* HttpContextExtensions.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Runtime.CompilerServices;


namespace VNLib.Net.Http.Core.Response
{
    /// <summary>
    /// Provides extended funcionality of an <see cref="HttpContext"/>
    /// </summary>
    internal static class HttpContextExtensions
    {
        /// <summary>
        /// Responds to a connection with the given status code
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="code">The status code to send</param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void Respond(this HttpContext ctx, HttpStatusCode code) => ctx.Response.SetStatusCode(code);

        /// <summary>
        /// Begins a 301 redirection by sending status code and message heaaders to client.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="location">Location to direct client to, sets the "Location" header</param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void Redirect301(this HttpContext ctx, Uri location)
        {
            ctx.Response.SetStatusCode(HttpStatusCode.MovedPermanently);
            //Encode the string for propery http url formatting and set the location header
            ctx.Response.Headers[HttpResponseHeader.Location] = location.ToString();
        }

        public const string NO_CACHE_STRING = "no-cache";
        private static readonly string CACHE_CONTROL_VALUE = HttpHelpers.GetCacheString(CacheType.NoCache | CacheType.NoStore);

        /// <summary>
        /// Sets CacheControl and Pragma headers to no-cache
        /// </summary>
        /// <param name="Response"></param>
        public static void SetNoCache(this HttpResponse Response)
        {
            Response.Headers[HttpResponseHeader.Pragma] = NO_CACHE_STRING;
            Response.Headers[HttpResponseHeader.CacheControl] = CACHE_CONTROL_VALUE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlyMemory<byte> GetRemainingConstrained(this IMemoryResponseReader reader, int limit)
        {
            //Calc the remaining bytes
            int size = Math.Min(reader.Remaining, limit);
            //get segment and slice
            return reader.GetMemory()[..size];
        }
    }
}