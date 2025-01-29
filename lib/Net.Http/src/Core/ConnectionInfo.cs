/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ConnectionInfo.cs 
*
* ConnectionInfo.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Text;
using System.Collections.Generic;
using VNLib.Net.Http.Core.Request;

namespace VNLib.Net.Http.Core
{
    ///<inheritdoc/>
    internal sealed class ConnectionInfo : IConnectionInfo
    {
        private HttpContext Context;

        ///<inheritdoc/>
        public Uri RequestUri => Context.Request.State.Location;

        ///<inheritdoc/>
        public string Path => RequestUri.LocalPath;

        ///<inheritdoc/>
        public string? UserAgent => Context.Request.State.UserAgent;

        ///<inheritdoc/>
        public IHeaderCollection Headers { get; private set; }

        ///<inheritdoc/>
        public bool CrossOrigin { get; }

        ///<inheritdoc/>
        public bool IsWebSocketRequest { get; }

        ///<inheritdoc/>
        public ContentType ContentType => Context.Request.State.ContentType;

        ///<inheritdoc/>
        public HttpMethod Method => Context.Request.State.Method;

        ///<inheritdoc/>
        public HttpVersion ProtocolVersion => Context.Request.State.HttpVersion;

        ///<inheritdoc/>
        public Uri? Origin => Context.Request.State.Origin;

        ///<inheritdoc/>
        public Uri? Referer => Context.Request.State.Referrer;

        ///<inheritdoc/>
        public HttpRange Range => Context.Request.State.Range;

        ///<inheritdoc/>
        public IPEndPoint LocalEndpoint => Context.Request.State.LocalEndPoint;

        ///<inheritdoc/>
        public IPEndPoint RemoteEndpoint => Context.Request.State.RemoteEndPoint;

        ///<inheritdoc/>
        public Encoding Encoding => Context.ParentServer.Config.HttpEncoding;

        ///<inheritdoc/>
        public IReadOnlyDictionary<string, string> RequestCookies => Context.Request.Cookies;

        ///<inheritdoc/>
        public IReadOnlyCollection<string> Accept => Context.Request.Accept;

        ///<inheritdoc/>
        public ref readonly TransportSecurityInfo? GetTransportSecurityInfo() => ref Context.GetSecurityInfo();

        ///<inheritdoc/>
        public void SetCookie(in HttpResponseCookie cookie)
        {
            //name MUST not be null
            ArgumentException.ThrowIfNullOrWhiteSpace(cookie.Name, nameof(cookie.Name));

            Context.Response.AddCookie(in cookie);
        }

        internal ConnectionInfo(HttpContext ctx)
        {
            //Update the context referrence
            Context = ctx;
            //Create new header collection
            Headers = new VnHeaderCollection(ctx);
            //set co value
            CrossOrigin = ctx.Request.IsCrossOrigin();
            //Set websocket status
            IsWebSocketRequest = ctx.Request.IsWebSocketRequest();
        }

#nullable disable
        internal void Clear()
        {
            Context = null;
            (Headers as VnHeaderCollection).Clear();
            Headers = null;
        }
    }
}