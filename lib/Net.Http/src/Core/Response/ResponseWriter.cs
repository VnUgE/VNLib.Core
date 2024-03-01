/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ResponseWriter.cs 
*
* ResponseWriter.cs is part of VNLib.Net.Http which is part of the larger 
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

/*
 * This file handles response entity processing. It handles in-memory response
 * processing, as well as stream response processing. It handles constraints
 * such as content-range limits. I tried to eliminate or reduce the amount of
 * memory copying required to process the response entity.
 */

using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Net.Http.Core.Compression;

namespace VNLib.Net.Http.Core.Response
{
    internal sealed class ResponseWriter : IHttpResponseBody
    {
        private ResponsBodyDataState _userState;

        ///<inheritdoc/>
        public bool HasData => _userState.IsSet;
       
        ///<inheritdoc/>
        public bool BufferRequired => _userState.BufferRequired;

        ///<inheritdoc/>
        public long Length => _userState.Legnth;

        /// <summary>
        /// Attempts to set the response body as a stream
        /// </summary>
        /// <param name="response">The stream response body to read</param>
        /// <param name="length">Explicit length of the stream</param>
        /// <returns>True if the response entity could be set, false if it has already been set</returns>
        internal bool TrySetResponseBody(IHttpStreamResponse response, long length)
        {
            if (_userState.IsSet)
            {
                return false;
            }

            Debug.Assert(response != null, "Stream value is null, illegal operation");
            Debug.Assert(length > -1, "explicit length passed a negative value, illegal operation");

            _userState = ResponsBodyDataState.FromStream(response, length);
            return true;
        }

        /// <summary>
        /// Attempts to set the response entity
        /// </summary>
        /// <param name="response">The memory response to set</param>
        /// <returns>True if the response entity could be set, false if it has already been set</returns>
        internal bool TrySetResponseBody(IMemoryResponseReader response)
        {
            if (_userState.IsSet)
            {
                return false;
            }

            Debug.Assert(response != null, "Memory response argument was null and expected a value");

            //Assign user-state
            _userState = ResponsBodyDataState.FromMemory(response);
            return true;
        }

        /// <summary>
        /// Attempts to set the response entity
        /// </summary>
        /// <param name="rawStream">The raw stream response to set</param>
        /// <param name="length">Explicit length of the raw stream</param>
        /// <returns>True if the response entity could be set, false if it has already been set</returns>
        internal bool TrySetResponseBody(Stream rawStream, long length)
        {
            if (_userState.IsSet)
            {
                return false;
            }

            Debug.Assert(rawStream != null, "Raw stream value is null, illegal operation");
            Debug.Assert(length > -1, "explicit length passed a negative value, illegal operation");

            //Assign user-state
            _userState = ResponsBodyDataState.FromRawStream(rawStream, length);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnComplete()
        {
            //Clear response containers
            _userState.Dispose();
            _userState = default;

            _readSegment = default;
        }


        private ReadOnlyMemory<byte> _readSegment;

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

        ///<inheritdoc/>
        public Task WriteEntityAsync(IDirectResponsWriter dest, Memory<byte> buffer) => WriteEntityAsync(dest, buffer, 0);

        ///<inheritdoc/>        
        public async Task WriteEntityAsync<TComp>(TComp compressor, IResponseDataWriter writer, Memory<byte> buffer) 
            where TComp : IResponseCompressor
        {
            //Create a chunked response writer struct to pass to write async function
            ChunkedResponseWriter<TComp> output = new(writer, compressor);

            await WriteEntityAsync(output, buffer, compressor.BlockSize);

            /*
             * Once there is no more response data avialable to compress
             * we need to flush the compressor, then flush the writer
             * to publish all accumulated data to the client
             */

            do
            {
                //Flush the compressor output
                int written = compressor.Flush(writer.GetMemory());

                //No more data to buffer
                if (written == 0)
                {
                    //final flush and exit
                    await writer.FlushAsync(true);
                    break;
                }

                if (writer.Advance(written) == 0)
                {
                    //Flush because accumulator is full
                    await writer.FlushAsync(false);
                }

            } while (true);
        }

        private async Task WriteEntityAsync<TResWriter>(TResWriter dest, Memory<byte> buffer, int blockSize)
           where TResWriter : IDirectResponsWriter
        {
            //try to clamp the buffer size to the compressor block size
            if (blockSize > 0)
            {
                buffer = buffer[..Math.Min(buffer.Length, blockSize)];
            }

            //Write a sliding window response
            if (_userState.MemResponse != null)
            {
                if (blockSize > 0)
                {
                    while (_userState.MemResponse.Remaining > 0)
                    {
                        //Get next segment clamped to the block size
                        _readSegment = _userState.MemResponse.GetRemainingConstrained(blockSize);

                        //Commit output bytes
                        await dest.WriteAsync(_readSegment);

                        //Advance by the written amount
                        _userState.MemResponse.Advance(_readSegment.Length);
                    }
                }
                else
                {
                    //Write response body from memory
                    while (_userState.MemResponse.Remaining > 0)
                    {
                        //Get remaining segment
                        _readSegment = _userState.MemResponse.GetMemory();

                        //Write segment to output stream
                        await dest.WriteAsync(_readSegment);

                        //Advance by the written amount
                        _userState.MemResponse.Advance(_readSegment.Length);
                    }
                }

                //Disposing of memory response can be deferred until the end of the request since its always syncrhonous
            }
            else if (_userState.RawStream != null)
            {
                Debug.Assert(!buffer.IsEmpty, "Transfer buffer is required for streaming operations");

                await ProcessStreamDataAsync(_userState.GetRawStreamResponse(), dest, buffer, _userState.Legnth);
            }
            else
            {
                Debug.Assert(!buffer.IsEmpty, "Transfer buffer is required for streaming operations");
                Debug.Assert(_userState.Stream != null, "Stream value is null, illegal state");

                await ProcessStreamDataAsync(_userState.Stream, dest, buffer, _userState.Legnth);
            }
        }

        private static async Task ProcessStreamDataAsync<TStream, TWriter>(TStream stream, TWriter dest, Memory<byte> buffer, long length)
            where TStream : IHttpStreamResponse
            where TWriter : IDirectResponsWriter
        {
            /*
             * When streams are used, callers will submit an explict length value 
             * which must be respected. This allows the stream size to differ from
             * the actual content length. This is useful for when the stream is
             * non-seekable, or does not have a known length. Also used for 
             * content-range responses, that are shorter than the whole stream.
             */

            long sentBytes = 0;
            do
            {
                Memory<byte> offset = ClampCopyBuffer(buffer, length, sentBytes);

                //read only the amount of data that is required
                int read = await stream.ReadAsync(offset);

                if (read == 0)
                {
                    break;
                }

                //write only the data that was read (slice)
                await dest.WriteAsync(offset[..read]);

                sentBytes += read;

            } while (sentBytes < length);

            //Try to dispose the response stream asyncrhonously since we are done with it
            await stream.DisposeAsync();
        }
        
        private static Memory<byte> ClampCopyBuffer(Memory<byte> buffer, long contentLength, long sentBytes)
        {
            //get offset wrapper of the total buffer or remaining count
            int bufferSize = (int)Math.Min(buffer.Length, contentLength - sentBytes);
            return buffer[..bufferSize];
        }

        [Conditional("DEBUG")]
        private static void ValidateCompressionResult(in CompressionResult result, int segmentLen)
        {            
            Debug.Assert(result.BytesRead > -1, "Compression result returned a negative bytes read value");
            Debug.Assert(result.BytesWritten > -1, "Compression result returned a negative bytes written value");
            Debug.Assert(result.BytesWritten <= segmentLen, "Compression result wrote more bytes than the input segment length");
        }

#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
       

        private readonly struct ChunkedResponseWriter<TComp>(IResponseDataWriter writer, TComp comp) : IDirectResponsWriter
            where TComp : IResponseCompressor
        {

            public readonly async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer)
            {
                //Track read bytes and loop until all bytes are read
                ForwardOnlyMemoryReader<byte> streamReader = new (buffer);

                do
                {
                    //Compress the buffered data and flush if required
                    if (CompressNextSegment(ref streamReader))
                    {
                        //Time to flush
                        await writer.FlushAsync(false);
                    }

                } while (streamReader.WindowSize > 0);
            }

            private readonly bool CompressNextSegment(ref ForwardOnlyMemoryReader<byte> reader)
            {
                //Get output buffer
                Memory<byte> output = writer.GetMemory();

                //Compress the trimmed block
                CompressionResult res = comp.CompressBlock(reader.Window, output);
                ValidateCompressionResult(in res, output.Length);

                //Commit input bytes
                reader.Advance(res.BytesRead);

                return writer.Advance(res.BytesWritten) == 0;
            }
        }
    }
}