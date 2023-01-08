/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHeaderCollection.cs 
*
* IHeaderCollection.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Collections.Generic;

namespace VNLib.Net.Http
{
    /// <summary>
    /// The container for request and response headers
    /// </summary>
    public interface IHeaderCollection
    {
        /// <summary>
        /// Allows for enumeratring all requesest headers
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> RequestHeaders { get; }
        /// <summary>
        /// Allows for enumeratring all response headers
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> ResponseHeaders { get; }      
        /// <summary>
        /// Gets request header, or sets a response header
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Request header with key</returns>
        string? this[string index] { get; set; }        
        /// <summary>
        /// Sets a response header only with a response header index
        /// </summary>
        /// <param name="index">Response header</param>
        string this[HttpResponseHeader index] { set; }
        /// <summary>
        /// Gets a request header
        /// </summary>
        /// <param name="index">The request header enum </param>
        string? this[HttpRequestHeader index] { get; }
        /// <summary>
        /// Determines if the given header is set in current response headers
        /// </summary>
        /// <param name="header">Header value to check response headers for</param>
        /// <returns>true if header exists in current response headers, false otherwise</returns>
        bool HeaderSet(HttpResponseHeader header);
        /// <summary>
        /// Determines if the given request header is set in current request headers
        /// </summary>
        /// <param name="header">Header value to check request headers for</param>
        /// <returns>true if header exists in current request headers, false otherwise</returns>
        bool HeaderSet(HttpRequestHeader header);

        /// <summary>
        /// Overwrites (sets) the given response header to the exact value specified
        /// </summary>
        /// <param name="header">The enumrated header id</param>
        /// <param name="value">The value to specify</param>
        void Append(HttpResponseHeader header, string? value);
        /// <summary>
        /// Overwrites (sets) the given response header to the exact value specified
        /// </summary>
        /// <param name="header">The header name</param>
        /// <param name="value">The value to specify</param>
        void Append(string header, string? value);
    }
}