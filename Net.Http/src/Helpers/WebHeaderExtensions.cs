/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: WebHeaderExtensions.cs 
*
* WebHeaderExtensions.cs is part of VNLib.Net.Http which is part of the larger 
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

using System.Net;
using System.Runtime.CompilerServices;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Extends the <see cref="WebHeaderCollection"/> to provide some check methods
    /// </summary>
    public static class WebHeaderExtensions
    {
        /// <summary>
        /// Determines if the specified request header has been set in the current header collection
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="header">Header value to check</param>
        /// <returns>true if the header was set, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HeaderSet(this WebHeaderCollection headers, HttpRequestHeader header) => !string.IsNullOrWhiteSpace(headers[header]);
        /// <summary>
        /// Determines if the specified response header has been set in the current header collection
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="header">Header value to check</param>
        /// <returns>true if the header was set, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HeaderSet(this WebHeaderCollection headers, HttpResponseHeader header) => !string.IsNullOrWhiteSpace(headers[header]);
        /// <summary>
        /// Determines if the specified header has been set in the current header collection
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="header">Header value to check</param>
        /// <returns>true if the header was set, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HeaderSet(this WebHeaderCollection headers, string header) => !string.IsNullOrWhiteSpace(headers[header]);
    }
}