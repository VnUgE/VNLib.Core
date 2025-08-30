/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpContextResponseWriting.cs 
*
* HttpContextResponseWriting.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Diagnostics;
using System.Threading.Tasks;

using VNLib.Net.Http.Core.Response;
using VNLib.Net.Http.Core.Request;

namespace VNLib.Net.Http.Core
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

    internal partial class HttpContext
    {
        ///<inheritdoc/>
        public async Task WriteResponseAsync()
        {
            /*
             * If exceptions are raised, the transport is unusable, the connection is terminated,
             * and the release method will be called so the context can be reused
             */

            ValueTask discardTask = Request.InputStream.DiscardRemainingAsync();

            //See if response data needs to be written, if so we can parallel discard and write
            if (ResponseBody.HasData)
            {
                //Parallel the write and discard
                Task response = WriteResponseInternalAsync();

                //in .NET 8.0 WhenAll is now allocation free, so no biggie 
                await Task.WhenAll(discardTask.AsTask(), response);
            }
            else
            {
                //Set implicit 0 content length if not disabled
                if (!ContextFlags.IsSet(HttpControlMask.ImplictContentLengthDisabled))
                {
                    //RFC 7230, length only set on 200 + but not 204
                    if ((int)Response.StatusCode >= 200 && (int)Response.StatusCode != 204)
                    {
                        //If headers havent been sent by this stage there is no content, so set length to 0
                        Response.Headers.Set(HttpResponseHeader.ContentLength, "0");
                    }
                }

                await discardTask;
            }

            //Close response once send and discard are complete
            await Response.CloseAsync();
        }

        /// <summary>
        /// If implementing application set a response entity body, it is written to the output stream
        /// </summary>
        private Task WriteResponseInternalAsync()
        {
            //Adjust/append vary header
            Response.Headers.Add(HttpResponseHeader.Vary, "Accept-Encoding");

            long length = ResponseBody.Length;
            CompressionMethod compMethod = CompressionMethod.None;
            /*
             * It will be known at startup whether compression is supported, if not this is 
             * essentially a constant. 
             */
            if (ParentServer.SupportedCompressionMethods != CompressionMethod.None)
            {
                //Determine if compression should be used
                bool compressionDisabled = 
                        //disabled because app code disabled it
                        ContextFlags.IsSet(HttpControlMask.CompressionDisabled)
                        //Disabled because too large or too small
                        || length >= ParentServer.Config.CompressionLimit
                        || length < ParentServer.Config.CompressionMinimum
                        //Disabled because lower than http11 does not support chunked encoding
                        || Request.State.HttpVersion < HttpVersion.Http11;

                if (!compressionDisabled)
                {
                    //Get first compression method or none if disabled
                    compMethod = Request.GetCompressionSupport(ParentServer.SupportedCompressionMethods);

                    //Set response compression encoding headers
                    switch (compMethod)
                    {
                        case CompressionMethod.Gzip:
                            Response.Headers.Set(HttpResponseHeader.ContentEncoding, "gzip");
                            break;
                        case CompressionMethod.Deflate:
                            Response.Headers.Set(HttpResponseHeader.ContentEncoding, "deflate");
                            break;
                        case CompressionMethod.Brotli:
                            Response.Headers.Set(HttpResponseHeader.ContentEncoding, "br");
                            break;
                        case CompressionMethod.Zstd:
                            Response.Headers.Set(HttpResponseHeader.ContentEncoding, "zstd");
                            break;
                    }
                }
            }

            //Check on head methods
            if (Request.State.Method == HttpMethod.HEAD)
            {
                //Specify what the content length would be
                Response.Headers.Set(HttpResponseHeader.ContentLength, length.ToString());

                //We must send headers here so content length doesnt get overwritten, close will be called after this to flush to transport
                Response.FlushHeaders();

                return Task.CompletedTask;
            }
            /*
             * User submitted a 0 length response body, let hooks clean-up 
             * any resources. Simply flush headers and exit
             */
            else if(length == 0)
            {
                
                Response.FlushHeaders();
                return Task.CompletedTask;
            }
            else
            {
                //Set the explicit length if a range was set
                return WriteEntityDataAsync(length, compMethod);
            }
        }
      
        private async Task WriteEntityDataAsync(long length, CompressionMethod compMethod)
        {
            //Determine if buffer is required
            Memory<byte> buffer = ResponseBody.BufferRequired ? Buffers.GetResponseDataBuffer() : Memory<byte>.Empty;

            //We need to flush header before we can write to the transport
            await Response.CompleteHeadersAsync(compMethod == CompressionMethod.None ? length : -1);

            if (compMethod == CompressionMethod.None)
            {
                //Setup a direct stream to write to because compression is not enabled
                IDirectResponsWriter output = Response.GetDirectStream();

                //Write response with optional forced length
                await ResponseBody.WriteEntityAsync(output, buffer);
            }
            else
            {
                //Compressor must never be null at this point
                Debug.Assert(_compressor != null, "Compression was allowed but the compressor was not initialized");

                //Get the chunked response writer
                IResponseDataWriter output = Response.GetChunkWriter();

                //Init compressor (Deinint is deferred to the end of the request)
                _compressor.Init(compMethod);

                //Write response
                await ResponseBody.WriteEntityAsync(_compressor, output, buffer);
            }
        }


#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

    }
}
