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
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Net.Http.Core
{

    internal partial class HttpContext
    {
        ///<inheritdoc/>
        public async Task WriteResponseAsync(CancellationToken cancellation)
        {
            /*
             * If exceptions are raised, the transport is unusable, the connection is terminated,
             * and the release method will be called so the context can be reused
             */

            ValueTask discardTask = Request.InputStream.DiscardRemainingAsync(Buffers);

            //See if discard is needed
            if (ResponseBody.HasData)
            {
                //Parallel the write and discard
                Task response = WriteResponseInternalAsync(cancellation);

                if (discardTask.IsCompletedSuccessfully)
                {
                    //If discard is already complete, await the response
                    await response;
                }
                else
                {
                    //If discard is not complete, await both
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
        /// <param name="token">A token to cancel the operation</param>
        private async Task WriteResponseInternalAsync(CancellationToken token)
        {
            //Adjust/append vary header
            Response.Headers.Add(HttpResponseHeader.Vary, "Accept-Encoding");

            //For head methods
            if (Request.Method == HttpMethod.HEAD)
            {
                if (Request.Range != null)
                {
                    //Get local range
                    Tuple<long, long> range = Request.Range;

                    //Calc constrained content length
                    long length = ResponseBody.GetResponseLengthWithRange(range);

                    //End range is inclusive so substract 1
                    long endRange = (range.Item1 + length) - 1;

                    //Set content-range header
                    Response.SetContentRange(range.Item1, endRange, length);

                    //Specify what the content length would be
                    Response.Headers[HttpResponseHeader.ContentLength] = length.ToString();

                }
                else
                {
                    //If the request method is head, do everything but send the body
                    Response.Headers[HttpResponseHeader.ContentLength] = ResponseBody.Length.ToString();
                }

                //We must send headers here so content length doesnt get overwritten, close will be called after this to flush to transport
                Response.FlushHeaders();
            }
            else
            {
                Stream outputStream;
                /*
                 * Process range header, data will not be compressed because that would 
                 * require buffering, not a feature yet, and since the range will tell
                 * us the content length, we can get a direct stream to write to
                */
                if (Request.Range != null)
                {
                    //Get local range
                    Tuple<long, long> range = Request.Range;

                    //Calc constrained content length
                    long length = ResponseBody.GetResponseLengthWithRange(range);

                    //End range is inclusive so substract 1
                    long endRange = (range.Item1 + length) - 1;

                    //Set content-range header
                    Response.SetContentRange(range.Item1, endRange, length);

                    //Get the raw output stream and set the length to the number of bytes
                    outputStream = await Response.GetStreamAsync(length);

                    await WriteEntityDataAsync(outputStream, length, token);
                }
                else
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

                    //Get first compression method or none if disabled
                    HttpRequestExtensions.CompressionType ct = compressionDisabled ? HttpRequestExtensions.CompressionType.None : Request.GetCompressionSupport();

                    switch (ct)
                    {
                        case HttpRequestExtensions.CompressionType.Gzip:
                            {
                                //Specify gzip encoding (using chunked encoding)
                                Response.Headers[HttpResponseHeader.ContentEncoding] = "gzip";

                                //get the chunked output stream
                                Stream chunked = await Response.GetStreamAsync();

                                //Use chunked encoding and send data as its written 
                                outputStream = new GZipStream(chunked, ParentServer.Config.CompressionLevel, false);
                            }
                            break;
                        case HttpRequestExtensions.CompressionType.Deflate:
                            {
                                //Specify gzip encoding (using chunked encoding)
                                Response.Headers[HttpResponseHeader.ContentEncoding] = "deflate";
                                //get the chunked output stream
                                Stream chunked = await Response.GetStreamAsync();
                                //Use chunked encoding and send data as its written
                                outputStream = new DeflateStream(chunked, ParentServer.Config.CompressionLevel, false);
                            }
                            break;
                        case HttpRequestExtensions.CompressionType.Brotli:
                            {
                                //Specify Brotli encoding (using chunked encoding)
                                Response.Headers[HttpResponseHeader.ContentEncoding] = "br";
                                //get the chunked output stream
                                Stream chunked = await Response.GetStreamAsync();
                                //Use chunked encoding and send data as its written
                                outputStream = new BrotliStream(chunked, ParentServer.Config.CompressionLevel, false);
                            }
                            break;
                        //Default is no compression
                        case HttpRequestExtensions.CompressionType.None:
                        default:
                            //Since we know how long the response will be, we can submit it now (see note above for same issues)
                            outputStream = await Response.GetStreamAsync(ResponseBody.Length);
                            break;
                    }

                    //Write entity to output
                    await WriteEntityDataAsync(outputStream, token);
                }
            }
        }

        private async Task WriteEntityDataAsync(Stream outputStream, CancellationToken token)
        {
            try
            {
                //Determine if buffer is required
                if (ResponseBody.BufferRequired)
                {
                    //Get response data buffer, may be smaller than suggested size
                    Memory<byte> buffer = Buffers.GetResponseDataBuffer();

                    //Write response
                    await ResponseBody.WriteEntityAsync(outputStream, buffer, token);
                }
                //No buffer is required, write response directly
                else
                {
                    //Write without buffer
                    await ResponseBody.WriteEntityAsync(outputStream, null, token);
                }
            }
            finally
            {
                //always dispose output stream
                await outputStream.DisposeAsync();
            }
        }

        private async Task WriteEntityDataAsync(Stream outputStream, long length, CancellationToken token)
        {
            try
            {
                //Determine if buffer is required
                if (ResponseBody.BufferRequired)
                {
                    //Get response data buffer, may be smaller than suggested size
                    Memory<byte> buffer = Buffers.GetResponseDataBuffer();

                    //Write response
                    await ResponseBody.WriteEntityAsync(outputStream, length, buffer, token);
                }
                //No buffer is required, write response directly
                else
                {
                    //Write without buffer
                    await ResponseBody.WriteEntityAsync(outputStream, length, null, token);
                }
            }
            finally
            {
                //always dispose output stream
                await outputStream.DisposeAsync();
            }
        }
    }
}
