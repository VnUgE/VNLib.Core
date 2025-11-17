/*
* Copyright (c) 2025 Vaughn Nugent
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

using VNLib.Net.Http.Core.Response;

namespace VNLib.Net.Http.Core
{
    internal sealed class HttpEvent(HttpContext ctx) : MarshalByRefObject, IHttpEvent
    {
        private HttpContext Context = ctx;
        private ConnectionInfo _ci = new(ctx);
        private FileUpload[] _uploads = ctx.Request.CopyUploads();

        ///<inheritdoc/>
        IConnectionInfo IHttpEvent.Server => _ci;

        ///<inheritdoc/>
        IHttpServer IHttpEvent.OriginServer => Context.ParentServer;

        ///<inheritdoc/>
        IReadOnlyDictionary<string, string> IHttpEvent.QueryArgs => Context.Request.QueryArgs;

        ///<inheritdoc/>
        IReadOnlyDictionary<string, string> IHttpEvent.RequestArgs => Context.Request.RequestArgs;

        ///<inheritdoc/>
        IReadOnlyList<FileUpload> IHttpEvent.Files => _uploads;

        ///<inheritdoc/>
        void IHttpEvent.SetControlFlag(ulong mask) => Context.ContextFlags.Set(mask);

        ///<inheritdoc/>
        void IHttpEvent.DangerousChangeProtocol(IAlternateProtocol protocolHandler)
        {
            if (Context.AlternateProtocol != null)
            {
                throw new InvalidOperationException("A protocol handler was already specified");
            }

            ArgumentNullException.ThrowIfNull(protocolHandler);

            //Set 101 status code
            Context.Respond(HttpStatusCode.SwitchingProtocols);
            Context.AlternateProtocol = protocolHandler;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IHttpEvent.CloseResponse(HttpStatusCode code) => Context.Respond(code);

        ///<inheritdoc/>
        void IHttpEvent.CloseResponse(HttpStatusCode code, ContentType type, Stream stream, long length)
        {
            ArgumentNullException.ThrowIfNull(stream, nameof(stream));
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            //Check if the stream is valid. We will need to read the stream, and we will also need to get the length property 
            if (!stream.CanRead)
            {
                throw new IOException("The stream.Length property must be available and the stream must be readable");
            }

            //If stream is empty, ignore it, the server will default to 0 content length and avoid overhead
            if (length == 0)
            {
                //Stream is disposed because it is assumed we now own the lifecycle of the stream
                stream.Dispose();
                return;
            }

            //Finally store the stream input
            if (!Context.ResponseBody.TrySetResponseBody(stream, length))
            {
                throw new InvalidOperationException("A response body has already been set");
            }

            //User may want to set the content type header themselves
            if (type != ContentType.NonSupported)
            {
                //Set content type header after body
                Context.Response.Headers.Set(HttpResponseHeader.ContentType, HttpHelpers.GetContentTypeString(type));
            }

            //Set status code only after everything else is good, should never throw
            Context.Response.SetStatusCode(code);
        }

        ///<inheritdoc/>
        void IHttpEvent.CloseResponse(HttpStatusCode code, ContentType type, IMemoryResponseReader entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            //If stream is empty, ignore it, the server will default to 0 content length and avoid overhead
            if (entity.Remaining == 0)
            {
                //Stream is disposed because it is assumed we now own the lifecycle of the stream
                entity.Close();
                return;
            }

            //Store the memory reader input
            if (!Context.ResponseBody.TrySetResponseBody(entity))
            {
                throw new InvalidOperationException("A response body has already been set");
            }

            //User may want to set the content type header themselves
            if (type != ContentType.NonSupported)
            {
                //Set content type header after body
                Context.Response.Headers.Set(HttpResponseHeader.ContentType, HttpHelpers.GetContentTypeString(type));
            }

            //Set status code only after everything else is good, should never throw
            Context.Response.SetStatusCode(code);
        }

        ///<inheritdoc/>
        void IHttpEvent.CloseResponse(HttpStatusCode code, ContentType type, IHttpStreamResponse stream, long length)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            //If stream is empty, ignore it, the server will default to 0 content length and avoid overhead
            if (length == 0)
            {
                //Stream is disposed because it is assumed we now own the lifecycle of the stream
                stream.Dispose();
                return;
            }

            //Finally store the stream input
            if (!Context.ResponseBody.TrySetResponseBody(stream, length))
            {
                throw new InvalidOperationException("A response body has already been set");
            }

            //User may want to set the content type header themselves
            if (type != ContentType.NonSupported)
            {
                //Set content type header after body
                Context.Response.Headers.Set(HttpResponseHeader.ContentType, HttpHelpers.GetContentTypeString(type));
            }

            //Set status code only after everything else is good, should never throw
            Context.Response.SetStatusCode(code);
        }

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        internal void Clear()
        {
            //Clean up reference types and cleanable objects
            Context = null;
            _ci.Clear();
            _ci = null;
            _uploads = null;
        }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
