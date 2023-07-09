/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Threading.Tasks;

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

            //See if discard is needed
            if (ResponseBody.HasData)
            {
                //Parallel the write and discard
                Task response = WriteResponseInternalAsync();

                if (discardTask.IsCompletedSuccessfully)
                {
                    //If discard is already complete, await the response
                    await response;
                }
                else
                {
                    //If discard is not complete, await both, avoid wait-all method because it will allocate
                    await Task.WhenAll(discardTask.AsTask(), response);
                }
            }
            else
            {
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

            bool hasRange = Request.Range != null;
            long length = ResponseBody.Length;
            CompressionMethod compMethod = CompressionMethod.None;

            /*
             * Process range header, data will not be compressed because that would 
             * require buffering, not a feature yet, and since the range will tell
             * us the content length, we can get a direct stream to write to
            */
            if (hasRange)
            {
                //Get local range
                Tuple<long, long> range = Request.Range!;

                //Calc constrained content length
                length = ResponseBody.GetResponseLengthWithRange(range);

                //End range is inclusive so substract 1
                long endRange = (range.Item1 + length) - 1;

                //Set content-range header
                Response.SetContentRange(range.Item1, endRange, length);
            }
            /*
             * It will be known at startup whether compression is supported, if not this is 
             * essentially a constant. 
             */
            else if (ParentServer.SupportedCompressionMethods != CompressionMethod.None)
            {
                //Determine if compression should be used
                bool compressionDisabled = 
                        //disabled because app code disabled it
                        ContextFlags.IsSet(COMPRESSION_DISABLED_MSK)
                        //Disabled because too large or too small
                        || ResponseBody.Length >= ParentServer.Config.CompressionLimit
                        || ResponseBody.Length < ParentServer.Config.CompressionMinimum
                        //Disabled because lower than http11 does not support chunked encoding
                        || Request.HttpVersion < HttpVersion.Http11;

                if (!compressionDisabled)
                {
                    //Get first compression method or none if disabled
                    compMethod = Request.GetCompressionSupport(ParentServer.SupportedCompressionMethods);

                    //Set response headers
                    switch (compMethod)
                    {
                        case CompressionMethod.Gzip:
                            //Specify gzip encoding (using chunked encoding)
                            Response.Headers[HttpResponseHeader.ContentEncoding] = "gzip";
                            break;
                        case CompressionMethod.Deflate:
                            //Specify delfate encoding (using chunked encoding)
                            Response.Headers[HttpResponseHeader.ContentEncoding] = "deflate";
                            break;
                        case CompressionMethod.Brotli:
                            //Specify Brotli encoding (using chunked encoding)
                            Response.Headers[HttpResponseHeader.ContentEncoding] = "br";
                            break;
                    }
                }
            }

            //Check on head methods
            if (Request.Method == HttpMethod.HEAD)
            {
                //Specify what the content length would be
                Response.Headers[HttpResponseHeader.ContentLength] = length.ToString();

                //We must send headers here so content length doesnt get overwritten, close will be called after this to flush to transport
                Response.FlushHeaders();

                return Task.CompletedTask;
            }
            else
            {
                //Set the explicit length if a range was set
                return WriteEntityDataAsync(length, compMethod, hasRange);
            }
        }
      
        private async Task WriteEntityDataAsync(long length, CompressionMethod compMethod, bool hasExplicitLength)
        {
            //Get output stream, and always dispose it
            await using Stream outputStream = await GetOutputStreamAsync(length, compMethod);

            //Determine if buffer is required
            Memory<byte> buffer = ResponseBody.BufferRequired ? Buffers.GetResponseDataBuffer() : Memory<byte>.Empty;

            /*
             * Using compression, we must initialize a compressor, and write the response
             * with the locked compressor
             */
            if (compMethod != CompressionMethod.None)
            {
                //Compressor must never be null at this point
                Debug.Assert(_compressor != null, "Compression was allowed but the compressor was not initialized");

                //Init compressor (Deinint is deferred to the end of the request)
                _compressor.Init(outputStream, compMethod);

                //Write response
                await ResponseBody.WriteEntityAsync(_compressor, buffer);

            }
            /*
             * Explicit length may be set when the response may have more data than requested
             * by the client. IE: when a range is set, we need to make sure we sent exactly the 
             * correct data, otherwise the client will drop the connection.
             */
            else if(hasExplicitLength)
            {
                //Write response with explicit length
                await ResponseBody.WriteEntityAsync(outputStream, length, buffer);
               
            }
            else
            {                
                 await ResponseBody.WriteEntityAsync(outputStream, buffer);
            }
        }

        private ValueTask<Stream> GetOutputStreamAsync(long length, CompressionMethod compMethod)
        {
            return compMethod == CompressionMethod.None ? Response.GetStreamAsync(length) : Response.GetStreamAsync();
        }

#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

    }
}
