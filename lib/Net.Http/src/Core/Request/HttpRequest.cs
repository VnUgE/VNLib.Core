/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpRequest.cs 
*
* HttpRequest.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http.Core
{
    internal sealed class HttpRequest(TransportManager transport, ushort maxUploads) : IHttpLifeCycle
#if DEBUG
        ,IStringSerializeable
#endif
    {
        public readonly VnWebHeaderCollection Headers = new();
        public readonly List<string> Accept = new(8);
        public readonly List<string> AcceptLanguage = new(8);
        public readonly Dictionary<string, string> Cookies = new(5, StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> RequestArgs = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> QueryArgs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A transport stream wrapper that is positioned for reading
        /// the entity body from the input stream
        /// </summary>
        public readonly HttpInputStream InputStream = new(transport);

        /*
         * Evil mutable structure that stores the http request state. 
         * 
         * Readonly ref allows for immutable accessors, but 
         * explicit initialization function for a mutable ref
         * that can be stored in local state to ensure proper state
         * initalization.
         * 
         * Reason - easy and mistake free object reuse with safe 
         * null/default values and easy reset.
         */
        private HttpRequestState _state;
        private readonly FileUpload[] _uploads = new FileUpload[maxUploads];
        private readonly FileUpload[] _singleUpload = new FileUpload[1];

        /// <summary>
        /// Gets a mutable structure ref only used to initalize the request 
        /// state.
        /// </summary>
        /// <returns>A mutable reference to the state structure for initalization purposes</returns>
        internal ref HttpRequestState GetMutableStateForInit() => ref _state;

        /// <summary>
        /// A readonly reference to the internal request state once initialized
        /// </summary>
        internal ref readonly HttpRequestState State => ref _state;

        void IHttpLifeCycle.OnPrepare()
        { }

        void IHttpLifeCycle.OnRelease()
        { }
        
        void IHttpLifeCycle.OnNewRequest()
        { }

        /// <summary>
        /// Initializes the <see cref="HttpRequest"/> for an incomming connection
        /// </summary>
        /// <param name="ctx">The <see cref="ITransportContext"/> to attach the request to</param>
        /// <param name="defaultHttpVersion">The default http version</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(ITransportContext ctx, HttpVersion defaultHttpVersion)
        {
            _state.LocalEndPoint = ctx.LocalEndPoint;
            _state.RemoteEndPoint = ctx.RemoteEndpoint;
            //Set to default http version so the response can be configured properly
            _state.HttpVersion = defaultHttpVersion;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnComplete()
        {
            FreeUploadBuffers();

            //Clear request state (this is why a struct is used)
            _state = default;

            //release the input stream
            InputStream.OnComplete();
            //Clear args
            RequestArgs.Clear();
            QueryArgs.Clear();
            //Make sure headers, cookies, and accept are cleared for reuse
            Headers.Clear();
            Cookies.Clear();
            Accept.Clear();
            AcceptLanguage.Clear();
        }

        private void FreeUploadBuffers()
        {
            //Dispose all initialized files, should be much faster than using Array.Clear();
            for (int i = 0; i < maxUploads; i++)
            {
                _uploads[i].Free();
                _uploads[i] = default;
            }

            _singleUpload[0] = default;
        }

        /// <summary>
        /// Checks if another upload can be added to the request
        /// </summary>
        /// <returns>A value indicating if another file upload can be added to the array</returns>
        public bool CanAddUpload() => _state.UploadCount < maxUploads;

        /// <summary>
        /// Attempts to obtain a reference to the next available 
        /// file upload in the request. If there are no more uploads
        /// available, a null reference is returned.
        /// </summary>
        /// <returns>A reference within the upload array to add the file</returns>
        public ref FileUpload AddFileUpload()
        {
            //See if there is room for another upload
            if (CanAddUpload())
            {
                //get ref to current position and increment the upload count
                return ref _uploads[_state.UploadCount++];
            }

            return ref Unsafe.NullRef<FileUpload>();
        }

        /// <summary>
        /// Attempts to add a file upload to the request if there 
        /// is room for it. If there is no room, it will be ignored.
        /// See <see cref="CanAddUpload"/> to check if another upload can be added.
        /// </summary>
        /// <param name="upload">The file upload structure to add to the list</param>
        public void AddFileUpload(in FileUpload upload) => AddFileUpload() = upload;

        /// <summary>
        /// Creates a new array and copies the uploads to it.
        /// </summary>
        /// <returns>The array clone of the file uploads</returns>
        public FileUpload[] CopyUploads()
        {
            if (_state.UploadCount == 0)
            {
                return [];
            }

            //Shortcut for a single upload request (hotpath optimization)
            if (_state.UploadCount == 1)
            {
                _singleUpload[0] = _uploads[0];
                return _singleUpload;
            }

            //Create new array to hold uploads
            FileUpload[] uploads = GC.AllocateUninitializedArray<FileUpload>(_state.UploadCount, pinned: false);
           
            Array.Copy(_uploads, uploads, _state.UploadCount);

            return uploads;
        }

#if DEBUG

        public string Compile()
        {
            //Alloc char buffer for compilation
            using IMemoryHandle<char> buffer = MemoryUtil.SafeAlloc<char>(16 * 1024);

            ForwardOnlyWriter<char> writer = new(buffer.Span);
            
            Compile(ref writer);
            
            return writer.ToString();
        }

        public void Compile(ref ForwardOnlyWriter<char> writer)
        {
            //Request line
            writer.Append(_state.Method.ToString());
            writer.Append(" ");
            writer.Append(_state.Location?.PathAndQuery);
            writer.Append(" HTTP/");
            switch (_state.HttpVersion)
            {
                case HttpVersion.None:
                    writer.Append("Unsuppored Http version");
                    break;
                case HttpVersion.Http1:
                    writer.Append("1.0");
                    break;
                case HttpVersion.Http11:
                    writer.Append("1.1");
                    break;
                case HttpVersion.Http2:
                    writer.Append("2.0");
                    break;
                case HttpVersion.Http09:
                    writer.Append("0.9");
                    break;
            }

            writer.Append("\r\n");

            //write host
            writer.Append("Host: ");
            writer.Append(_state.Location?.Authority);
            writer.Append("\r\n");

            //Write headers
            foreach (string header in Headers.Keys)
            {
                writer.Append(header);
                writer.Append(": ");
                writer.Append(Headers[header]);
                writer.Append("\r\n");
            }

            //Write cookies
            foreach (string cookie in Cookies.Keys)
            {
                writer.Append("Cookie: ");
                writer.Append(cookie);
                writer.Append("=");
                writer.Append(Cookies[cookie]);
                writer.Append("\r\n");
            }

            //Write accept
            if (Accept.Count > 0)
            {
                writer.Append("Accept: ");
                foreach (string accept in Accept)
                {
                    writer.Append(accept);
                    writer.Append(", ");
                }
                writer.Append("\r\n");
            }
            //Write accept language
            if (AcceptLanguage.Count > 0)
            {
                writer.Append("Accept-Language: ");
                foreach (string acceptLanguage in AcceptLanguage)
                {
                    writer.Append(acceptLanguage);
                    writer.Append(", ");
                }
                writer.Append("\r\n");
            }
            //Write user agent
            if (_state.UserAgent != null)
            {
                writer.Append("User-Agent: ");
                writer.Append(_state.UserAgent);
                writer.Append("\r\n");
            }
            //Write content type
            if (_state.ContentType != ContentType.NonSupported)
            {
                writer.Append("Content-Type: ");
                writer.Append(HttpHelpers.GetContentTypeString(_state.ContentType));
                writer.Append("\r\n");
            }
            //Write content length
            if (_state.ContentType != ContentType.NonSupported)
            {
                writer.Append("Content-Length: ");
                writer.Append(InputStream.Length);
                writer.Append("\r\n");
            }
            if (_state.KeepAlive)
            {
                writer.Append("Connection: keep-alive\r\n");
            }
            if (_state.Expect)
            {
                writer.Append("Expect: 100-continue\r\n");
            }
            if(_state.Origin != null)
            {
                writer.Append("Origin: ");
                writer.Append(_state.Origin.ToString());
                writer.Append("\r\n");
            }
            if (_state.Referrer != null)
            {
                writer.Append("Referrer: ");
                writer.Append(_state.Referrer.ToString());
                writer.Append("\r\n");
            }
            writer.Append("from ");
            writer.Append(_state.RemoteEndPoint.ToString());
            writer.Append("\r\n");
            writer.Append("Received on ");
            writer.Append(_state.LocalEndPoint.ToString());
            //Write end of headers
            writer.Append("\r\n");
        }

        public ERRNO Compile(Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }

        public override string ToString() => Compile();
#else

        public override string ToString() => "HTTP Library was compiled without a DEBUG directive, request logging is not available";

#endif
    }
}