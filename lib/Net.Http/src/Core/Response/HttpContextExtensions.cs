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