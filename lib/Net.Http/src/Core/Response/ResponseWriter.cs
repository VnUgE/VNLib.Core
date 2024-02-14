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
using System.Threading;
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

        //Buffering is required when a stream is set
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

            _userState = new(response, length);
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
            _userState = new(response);
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
            _userState = new(rawStream, length);
            return true;
        }

        private ReadOnlyMemory<byte> _readSegment;

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

        ///<inheritdoc/>
        public async Task WriteEntityAsync(IDirectResponsWriter dest, Memory<byte> buffer)
        {
            //Write a sliding window response
            if (_userState.MemResponse != null)
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

                //Disposing of memory response can be deferred until the end of the request since its always syncrhonous
            }
            else if(_userState.RawStream != null)
            {
                Debug.Assert(!buffer.IsEmpty, "Transfer buffer is required for streaming operations");

                await ProcessStreamDataAsync(_userState.GetRawStreamResponse(), dest, buffer);
            }
            else
            {
                Debug.Assert(!buffer.IsEmpty, "Transfer buffer is required for streaming operations");

                Debug.Assert(_userState.Stream != null, "Stream value is null, illegal operation");

                await ProcessStreamDataAsync(_userState.Stream!, dest, buffer);
            }
        }

        ///<inheritdoc/>        
        public async Task WriteEntityAsync<TComp>(TComp comp, IResponseDataWriter writer, Memory<byte> buffer) where TComp : IResponseCompressor
        {
            //try to clamp the buffer size to the compressor block size
            int maxBufferSize = Math.Min(buffer.Length, comp.BlockSize);
            if(maxBufferSize > 0)
            {
                buffer = buffer[..maxBufferSize];
            }

            ChunkedResponseWriter<TComp> output = new(writer, comp);

            //Write a sliding window response
            if (_userState.MemResponse != null)
            {
                while (_userState.MemResponse.Remaining > 0)
                {
                    //Get next segment
                    _readSegment = comp.BlockSize > 0 ? 
                        _userState.MemResponse.GetRemainingConstrained(comp.BlockSize) 
                        : _userState.MemResponse.GetMemory();

                    //Commit output bytes
                    await output.WriteAsync(_readSegment);

                    //Advance by the written amount
                    _userState.MemResponse.Advance(_readSegment.Length);
                }

                //Disposing of memory response can be deferred until the end of the request since its always syncrhonous
            }
            else if(_userState.RawStream != null)
            {
                Debug.Assert(!buffer.IsEmpty, "Transfer buffer is required for streaming operations");

                //Configure a raw stream response
                await ProcessStreamDataAsync(_userState.GetRawStreamResponse(), output, buffer);
            }
            else
            {
                Debug.Assert(!buffer.IsEmpty, "Transfer buffer is required for streaming operations");

                Debug.Assert(_userState.Stream != null, "Stream value is null, illegal operation");
                await ProcessStreamDataAsync(_userState.Stream, output, buffer);
            }
         

            /*
             * Once there is no more response data avialable to compress
             * we need to flush the compressor, then flush the writer
             * to publish all accumulated data to the client
             */

            do
            {
                //Flush the compressor output
                int written = comp.Flush(writer.GetMemory());

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

        private async Task ProcessStreamDataAsync<TStream, TWriter>(TStream stream, TWriter dest, Memory<byte> buffer) 
            where TStream: IHttpStreamResponse
            where TWriter: IDirectResponsWriter
        {
            /*
             * When streams are used, callers will submit an explict length value 
             * which must be respected. This allows the stream size to differ from
             * the actual content length. This is useful for when the stream is
             * non-seekable, or does not have a known length
             */

            long total = 0;
            while (total < Length)
            {
                //get offset wrapper of the total buffer or remaining count
                Memory<byte> offset = buffer[..(int)Math.Min(buffer.Length, Length - total)];

                //read
                int read = await stream.ReadAsync(offset);

                //Guard
                if (read == 0)
                {
                    break;
                }

                //write only the data that was read (slice)
                await dest.WriteAsync(offset[..read]);

                //Update total
                total += read;
            }

            //Try to dispose the response stream asyncrhonously since we are done with it
            await stream.DisposeAsync();
        }

        private static bool CompressNextSegment<TComp>(ref ForwardOnlyMemoryReader<byte> reader, TComp comp, IResponseDataWriter writer)
            where TComp: IResponseCompressor
        {
            //Get output buffer
            Memory<byte> output = writer.GetMemory();

            //Compress the trimmed block
            CompressionResult res = comp.CompressBlock(reader.Window, output);
            ValidateCompressionResult(in res);

            //Commit input bytes
            reader.Advance(res.BytesRead);

            return writer.Advance(res.BytesWritten) == 0;
        }

        [Conditional("DEBUG")]
        private static void ValidateCompressionResult(in CompressionResult result)
        {
            Debug.Assert(result.BytesRead > -1, "Compression result returned a negative bytes read value");
            Debug.Assert(result.BytesWritten > -1, "Compression result returned a negative bytes written value");
        }

#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnComplete()
        {
            //Clear rseponse containers
            _userState.Dispose();
            _userState = default;
            
            _readSegment = default;
        }


        private readonly struct ResponsBodyDataState
        {
            public readonly bool IsSet;
            public readonly bool BufferRequired;
            public readonly long Legnth;
            public readonly IHttpStreamResponse? Stream;
            public readonly IMemoryResponseReader? MemResponse;
            public readonly Stream? RawStream;           

            public ResponsBodyDataState(IHttpStreamResponse stream, long length)
            {
                Legnth = length;
                Stream = stream;
                MemResponse = null;
                RawStream = null;
                IsSet = true;
                BufferRequired = true;
            }

            public ResponsBodyDataState(IMemoryResponseReader reader)
            {
                Legnth = reader.Remaining;
                MemResponse = reader;
                Stream = null;
                RawStream = null;
                IsSet = true;
                BufferRequired = false;
            }

            public ResponsBodyDataState(Stream stream, long length)
            {
                Legnth = length;
                Stream = null;
                MemResponse = null;
                RawStream = stream;
                IsSet = true;
                BufferRequired = true;
            }

            public readonly HttpstreamResponse GetRawStreamResponse() => new(RawStream!);

            public readonly void Dispose()
            {
                Stream?.Dispose();
                MemResponse?.Close();
                RawStream?.Dispose();
            }
        }

        private readonly struct HttpstreamResponse(Stream stream) : IHttpStreamResponse
        {
            ///<inheritdoc/>
            public readonly void Dispose() => stream.Dispose();

            ///<inheritdoc/>
            public readonly ValueTask DisposeAsync() => stream.DisposeAsync();

            ///<inheritdoc/>
            public readonly ValueTask<int> ReadAsync(Memory<byte> buffer) => stream!.ReadAsync(buffer, CancellationToken.None);
        }

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
                    if (CompressNextSegment(ref streamReader, comp, writer))
                    {
                        //Time to flush
                        await writer.FlushAsync(false);
                    }

                } while (streamReader.WindowSize > 0);
            }
        }
    }
}