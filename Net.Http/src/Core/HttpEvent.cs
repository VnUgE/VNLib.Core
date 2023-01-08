/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpEvent.cs 
*
* HttpEvent.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Net.Http.Core;

namespace VNLib.Net.Http
{   
    internal sealed class HttpEvent : MarshalByRefObject, IHttpEvent
    {
        private HttpContext Context;
        private ConnectionInfo _ci;

        internal HttpEvent(HttpContext ctx)
        {
            Context = ctx;
            _ci = new ConnectionInfo(ctx);
        }

        ///<inheritdoc/>
        IConnectionInfo IHttpEvent.Server => _ci;
        
        ///<inheritdoc/>
        HttpServer IHttpEvent.OriginServer => Context.ParentServer;       

        ///<inheritdoc/>
        IReadOnlyDictionary<string, string> IHttpEvent.QueryArgs => Context.Request.RequestBody.QueryArgs;
        ///<inheritdoc/>
        IReadOnlyDictionary<string, string> IHttpEvent.RequestArgs => Context.Request.RequestBody.RequestArgs;
        ///<inheritdoc/>
        IReadOnlyList<FileUpload> IHttpEvent.Files => Context.Request.RequestBody.Uploads;

        ///<inheritdoc/>
        void IHttpEvent.DisableCompression() => Context.ContextFlags.Set(HttpContext.COMPRESSION_DISABLED_MSK);

        ///<inheritdoc/>
        void IHttpEvent.DangerousChangeProtocol(IAlternateProtocol protocolHandler)
        {
            if(Context.AlternateProtocol != null)
            {
                throw new InvalidOperationException("A protocol handler was already specified");
            }
            
            _ = protocolHandler ?? throw new ArgumentNullException(nameof(protocolHandler));
            
            //Set 101 status code
            Context.Respond(HttpStatusCode.SwitchingProtocols);
            Context.AlternateProtocol = protocolHandler;
        }
       
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IHttpEvent.CloseResponse(HttpStatusCode code) => Context.Respond(code);

        ///<inheritdoc/>
        void IHttpEvent.CloseResponse(HttpStatusCode code, ContentType type, Stream stream)
        {
            //Check if the stream is valid. We will need to read the stream, and we will also need to get the length property 
            if (!stream.CanSeek || !stream.CanRead)
            {
                throw new IOException("The stream.Length property must be available and the stream must be readable");
            }

            //If stream is empty, ignore it, the server will default to 0 content length and avoid overhead
            if (stream.Length == 0)
            {
                return;
            }

            //Set status code
            Context.Response.SetStatusCode(code);
            
            //Finally store the stream input
            if(!(Context.ResponseBody as ResponseWriter)!.TrySetResponseBody(stream))
            {
                throw new InvalidOperationException("A response body has already been set");
            }

            //Set content type header after body
            Context.Response.Headers[HttpResponseHeader.ContentType] = HttpHelpers.GetContentTypeString(type);
        }

        ///<inheritdoc/>
        void IHttpEvent.CloseResponse(HttpStatusCode code, ContentType type, IMemoryResponseReader entity)
        {
            //If stream is empty, ignore it, the server will default to 0 content length and avoid overhead
            if (entity.Remaining == 0)
            {
                return;
            }

            //Set status code
            Context.Response.SetStatusCode(code);

            //Finally store the stream input
            if (!(Context.ResponseBody as ResponseWriter)!.TrySetResponseBody(entity))
            {
                throw new InvalidOperationException("A response body has already been set");
            }

            //Set content type header after body
            Context.Response.Headers[HttpResponseHeader.ContentType] = HttpHelpers.GetContentTypeString(type);
        }

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        internal void Clear()
        {
            //Clean up referrence types and cleanable objects
            Context = null;
            _ci.Clear();
            _ci = null;
        }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}