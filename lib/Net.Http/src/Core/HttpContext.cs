/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.IO;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.Memory.Caching;


namespace VNLib.Net.Http.Core
{
    internal sealed partial class HttpContext : IConnectionContext, IReusable
    {
        /// <summary>
        /// When set as a response flag, disables response compression for 
        /// the current request/response flow
        /// </summary>
        public const ulong COMPRESSION_DISABLED_MSK = 0x01UL;

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
        /// The shared transport header reader buffer
        /// </summary>
        public readonly SharedHeaderReaderBuffer RequestBuffer;

        /// <summary>
        /// The response entity body container
        /// </summary>
        public readonly IHttpResponseBody ResponseBody;

        /// <summary>
        /// A collection of flags that can be used to control the way the context 
        /// responds to client requests
        /// </summary>
        public readonly BitField ContextFlags;

        /// <summary>
        /// Gets or sets the alternate application protocol to swtich to
        /// </summary>
        /// <remarks>
        /// This property is only cleared when the context is released for reuse
        /// so when this property contains a value, the context must be released
        /// or this property must be exlicitly cleared
        /// </remarks>
        public IAlternateProtocol? AlternateProtocol { get; set; }

        private readonly ResponseWriter responseWriter;
        private ITransportContext? _ctx;   
        
        public HttpContext(HttpServer server)
        {
            /*
             * Local method for retreiving the transport stream,
             * this adds protection/debug from response/request
             * containers not allowed to maintain referrences 
             * to a transport stream after it has been released
             */
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            Stream GetStream() => _ctx!.ConnectionStream;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            HttpVersion GetVersion() => Request.HttpVersion;

            ParentServer = server;

            //Create new request
            Request = new HttpRequest(GetStream);
            
            //create a new response object
            Response = new HttpResponse(
                server.Config.HttpEncoding, 
                ParentServer.Config.ResponseHeaderBufferSize, 
                ParentServer.Config.ChunkedResponseAccumulatorSize, 
                GetStream, 
                GetVersion);

            //The shared request parsing buffer
            RequestBuffer = new(server.Config.HeaderBufferSize);

            //Init response writer
            ResponseBody = responseWriter = new ResponseWriter();

            ContextFlags = new(0);
        }

        public TransportSecurityInfo? GetSecurityInfo() => _ctx?.GetSecurityInfo();
       

        #region LifeCycle Hooks

        ///<inheritdoc/>
        public void InitializeContext(ITransportContext ctx) => _ctx = ctx;

        ///<inheritdoc/>
        public void BeginRequest()
        {
            //Clear all flags
            ContextFlags.ClearAll();

            //Lifecycle on new request
            Request.OnNewRequest();
            Response.OnNewRequest();
            RequestBuffer.OnNewRequest();

            //Initialize the request
            Request.Initialize(_ctx!, ParentServer.Config.DefaultHttpVersion);
        }

        ///<inheritdoc/>
        public void EndRequest()
        {
            Request.OnComplete();
            Response.OnComplete();
            RequestBuffer.OnComplete();
            responseWriter.OnComplete();
        }
        
        void IReusable.Prepare()
        {
            Request.OnPrepare();
            Response.OnPrepare();
            RequestBuffer.OnPrepare();
        }        
        
        bool IReusable.Release()
        {
            _ctx = null;

            AlternateProtocol = null;

            //Release response/requqests
            Request.OnRelease();
            Response.OnRelease();
            RequestBuffer.OnRelease();

            return true;
        }
        
        #endregion
    }
}