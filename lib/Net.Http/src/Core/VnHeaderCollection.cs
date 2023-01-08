/*
* Copyright (c) 2022 Vaughn Nugent
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
    internal sealed class VnHeaderCollection : IHeaderCollection
    {
        private VnWebHeaderCollection _RequestHeaders;
        private VnWebHeaderCollection _ResponseHeaders;


        IEnumerable<KeyValuePair<string, string>> IHeaderCollection.RequestHeaders => _RequestHeaders!;

        IEnumerable<KeyValuePair<string, string>> IHeaderCollection.ResponseHeaders => _ResponseHeaders!;

        internal VnHeaderCollection(HttpContext context)
        {
            _RequestHeaders = context.Request.Headers;
            _ResponseHeaders = context.Response.Headers;
        }

        string? IHeaderCollection.this[string index]
        {
            get => _RequestHeaders[index];
            set => _ResponseHeaders[index] = value;
        }

        string IHeaderCollection.this[HttpResponseHeader index]
        {
            set => _ResponseHeaders[index] = value;
        }

        string? IHeaderCollection.this[HttpRequestHeader index] => _RequestHeaders[index];

        bool IHeaderCollection.HeaderSet(HttpResponseHeader header) => !string.IsNullOrEmpty(_ResponseHeaders[header]);

        bool IHeaderCollection.HeaderSet(HttpRequestHeader header) => !string.IsNullOrEmpty(_RequestHeaders[header]);

        void IHeaderCollection.Append(HttpResponseHeader header, string? value) => _ResponseHeaders.Add(header, value);

        void IHeaderCollection.Append(string header, string? value) => _ResponseHeaders.Add(header, value);

#nullable disable
        internal void Clear()
        {
            _RequestHeaders = null;
            _ResponseHeaders = null;
        }
    }
}