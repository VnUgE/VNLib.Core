/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpContext.cs 
*
* HttpContext.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Utils.Memory.Caching;
using VNLib.Net.Http.Core.Buffering;
using VNLib.Net.Http.Core.Compression;
using VNLib.Net.Http.Core.Response;
using VNLib.Net.Http.Core.Request;

namespace VNLib.Net.Http.Core
{

    internal sealed partial class HttpContext : IConnectionContext, IReusable, IHttpContextInformation
    {
        /// <summary>
        /// The reusable http request container
        /// </summary>
        public readonly HttpRequest Request;

        /// <summary>
        /// The reusable response controler
        /// </summary>
        public readonly HttpResponse Response;

        /// <summary>
        /// The http server that this context is bound to
        /// </summary>
        public readonly HttpServer ParentServer;

        /// <summary>
        /// The response entity body container
        /// </summary>
        public readonly ResponseWriter ResponseBody;

        /// <summary>
        /// A collection of flags that can be used to control the way the context 
        /// responds to client requests
        /// </summary>
        public readonly BitField ContextFlags;

        /// <summary>
        /// The internal buffer manager for the context
        /// </summary>
        public readonly ContextLockedBufferManager Buffers;

        /// <summary>
        /// Gets or sets the alternate application protocol to switch to
        /// </summary>
        /// <remarks>
        /// This property is only cleared when the context is released for reuse
        /// so when this property contains a value, the context must be released
        /// or this property must be exlicitly cleared
        /// </remarks>
        public IAlternateProtocol? AlternateProtocol { get; set; }

        private readonly TransportManager Transport;
        private readonly ManagedHttpCompressor? _compressor;
        private ITransportContext? _ctx;
        
        public HttpContext(HttpServer server, ManagedHttpCompressor? compressor)
        {
            ParentServer = server;
            _compressor = compressor;

            ContextFlags = new(0);

            Transport = new();

            //Init buffer manager, if compression is supported, we need to alloc a buffer for the compressor
            Buffers = new(in server.BufferConfig, _compressor != null);
         
            Request = new (Transport, server.Config.MaxUploadsPerRequest);
           
            Response = new (this, Transport, Buffers);
          
            ResponseBody = new ResponseWriter();
        }

        #region LifeCycle Hooks
        // Hooks are written in the order they are called
        // Shall remain near state objects reduced cognitive load

        void IReusable.Prepare()
        {
            Request.OnPrepare();
            Response.OnPrepare();
            ResponseBody.OnPrepare();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InitializeContext(ITransportContext ctx)
        {
            _ctx = ctx;

            //Alloc buffers during context init incase exception occurs in user-code
            Buffers.AllocateBuffer(
                allocator: ParentServer.Config.MemoryPool,
                config: in ParentServer.BufferConfig
            );

            //Init new connection
            _transport.OnNewConnection(ctx.ConnectionStream);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRequest()
        {
            ContextFlags.ClearAll();

            Debug.Assert(_ctx != null, "BeginRequest called before InitializeContext");

            //Lifecycle on new request
            Request.Initialize(_ctx!, ParentServer.Config.DefaultHttpVersion);

            Request.OnNewRequest();
            Response.OnNewRequest();
            ResponseBody.OnNewRequest();
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndRequest()
        {
            _preBufferedByteCount = 0; //Must reset after every request          

            Request.OnComplete();
            Response.OnComplete();
            ResponseBody.OnComplete();

            _compressor?.Free();
        }

        bool IReusable.Release()
        {
            _transport.OnRelease();

            _ctx = null;

            AlternateProtocol = null;

            Request.OnRelease();
            Response.OnRelease();
            ResponseBody.OnRelease();

            Buffers.FreeAll(ParentServer.Config.MemoryPool);

            return true;
        }

        #endregion

        /// <summary>
        /// Gets a readonly reference to the transport security information
        /// </summary>
        /// <returns>A readonly referrence to the <see cref="TransportSecurityInfo"/> structure </returns>
        public ref readonly TransportSecurityInfo? GetSecurityInfo() => ref _ctx!.GetSecurityInfo();

        #region Context information

        ///<inheritdoc/>
        Encoding IHttpContextInformation.Encoding => ParentServer.Config.HttpEncoding;

        ///<inheritdoc/>
        HttpVersion IHttpContextInformation.CurrentVersion => Request.State.HttpVersion;

        ///<inheritdoc/>
        public ref readonly HttpEncodedSegment CrlfSegment => ref ParentServer.Config.CrlfBytes;

        ///<inheritdoc/>
        public ref readonly HttpEncodedSegment FinalChunkSegment => ref ParentServer.Config.FinalChunkBytes;
     

        int _PreBufferedByteCount;

        /*
         * The following functions operate in tandem. Data should be buffered
         * by a call to BufferTransportAsync() and then made availbe by a call to
         * GetReader(). This set of functions only happens once per request/response
         * cycle. This allows a async buffer filling before a syncronous transport
         * read. 
         */

        public void GetReader(out TransportReader reader)
        {
            reader = new(
                Transport.Stream,
                Buffers.RequestHeaderParseBuffer,
                ParentServer.Config.HttpEncoding,
                ParentServer.Config.HeaderLineTermination
            );

            /*
             * Specal function to set available data
             * NOTE: this can be dangerous as the buffer is 
             */
            reader.SetAvailableData(_PreBufferedByteCount);

            Debug.Assert(reader.Available == _PreBufferedByteCount);
        }

        public async ValueTask BufferTransportAsync(CancellationToken cancellation)
        {
            /*
             * This function allows for pre-buffering of the transport
             * before parsing the request. It also allows waiting for more data async 
             * when an http1 request is in keep-alive mode waiting for more data. 
             * 
             * We can asynchronously read data when its available and preload 
             * the transport reader. The only catch is we need to access the 
             * raw Memory<byte> structure within the buffer. So the binary
             * buffer size MUST be respected.
             */

            _PreBufferedByteCount = 0;

            Memory<byte> dataBuffer = Buffers.GetInitStreamBuffer();

            _preBufferedByteCount = await _transport.Stream
                .ReadAsync(dataBuffer, cancellation)
                .ConfigureAwait(false);

            Debug.Assert(_preBufferedByteCount <= dataBuffer.Length);

            Debug.Assert(_PreBufferedByteCount <= dataBuffer.Length);
        }

        #endregion

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task FlushTransportAsync() => _transport.FlushAsync();
    }
}
