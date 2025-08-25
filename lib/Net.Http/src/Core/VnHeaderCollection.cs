/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: VnHeaderCollection.cs 
*
* VnHeaderCollection.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Collections.Generic;

namespace VNLib.Net.Http.Core
{
    internal sealed class VnHeaderCollection(HttpContext context) : IHeaderCollection
    {
        private VnWebHeaderCollection _requestHeaders = context.Request.Headers;
        private VnWebHeaderCollection _responseHeaders = context.Response.Headers;

        ///<inheritdoc/>
        IEnumerable<KeyValuePair<string, string>> IHeaderCollection.RequestHeaders => _requestHeaders!;

        ///<inheritdoc/>
        IEnumerable<KeyValuePair<string, string>> IHeaderCollection.ResponseHeaders => _responseHeaders!;

        ///<inheritdoc/>
        string? IHeaderCollection.this[string index]
        {
            get => _requestHeaders[index];
            set => _responseHeaders[index] = value;
        }

        ///<inheritdoc/>
        string IHeaderCollection.this[HttpResponseHeader index]
        {
            set => _responseHeaders[index] = value;
        }

        ///<inheritdoc/>
        string? IHeaderCollection.this[HttpRequestHeader index]
        {
            get => _requestHeaders[index];
        }

        ///<inheritdoc/>
        bool IHeaderCollection.HeaderSet(HttpResponseHeader header) 
            => !string.IsNullOrEmpty(_responseHeaders[header]);

        ///<inheritdoc/>
        bool IHeaderCollection.HeaderSet(HttpRequestHeader header) 
            => !string.IsNullOrEmpty(_requestHeaders[header]);

        ///<inheritdoc/>
        void IHeaderCollection.Append(HttpResponseHeader header, string? value) 
            => _responseHeaders.Add(header, value);

        ///<inheritdoc/>
        void IHeaderCollection.Append(string header, string? value) 
            => _responseHeaders.Add(header, value);

#nullable disable
        internal void Clear()
        {
            _requestHeaders = null;
            _responseHeaders = null;
        }
    }
}