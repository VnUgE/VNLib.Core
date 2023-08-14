/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Net.Http.Core.Response;
using VNLib.Net.Http.Core.Compression;

namespace VNLib.Net.Http.Core
{

    internal sealed class ResponseWriter : IHttpResponseBody
    {
        private Stream? _streamResponse;
        private IMemoryResponseReader? _memoryResponse;
        
        ///<inheritdoc/>
        public bool HasData { get; private set; }

        //Buffering is required when a stream is set
        bool IHttpResponseBody.BufferRequired => _streamResponse != null;

        ///<inheritdoc/>
        public long Length { get; private set; }

        /// <summary>
        /// Attempts to set the response body as a stream
        /// </summary>
        /// <param name="response">The stream response body to read</param>
        /// <returns>True if the response entity could be set, false if it has already been set</returns>
        internal bool TrySetResponseBody(Stream response)
        {
            if (HasData)
            {
                return false;
            }

            //Get relative length of the stream, IE the remaning bytes in the stream if position has been modified
            Length = (response.Length - response.Position);
            //Store ref to stream
            _streamResponse = response;
            //update has-data flag
            HasData = true;
            return true;
        }

        /// <summary>
        /// Attempts to set the response entity
        /// </summary>
        /// <param name="response">The memory response to set</param>
        /// <returns>True if the response entity could be set, false if it has already been set</returns>
        internal bool TrySetResponseBody(IMemoryResponseReader response)
        {
            if (HasData)
            {
                return false;
            }

            //Get length
            Length = response.Remaining;
            //Store ref to stream
            _memoryResponse = response;
            //update has-data flag
            HasData = true;
            return true;
        }

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

        ReadOnlyMemory<byte> _readSegment;

        ///<inheritdoc/>
        async Task IHttpResponseBody.WriteEntityAsync(IDirectResponsWriter dest, long count, Memory<byte> buffer)
        {
            int remaining;

            //Write a sliding window response
            if (_memoryResponse != null)
            {
                if(count > 0)
                {
                    //Get min value from count/range length
                    remaining = (int)Math.Min(count, _memoryResponse.Remaining);

                    //Write response body from memory
                    while (remaining > 0)
                    {
                        //Get remaining segment
                        _readSegment = _memoryResponse.GetRemainingConstrained(remaining);

                        //Write segment to output stream
                        await dest.WriteAsync(_readSegment);

                        //Advance by the written ammount
                        _memoryResponse.Advance(_readSegment.Length);

                        //Update remaining
                        remaining -= _readSegment.Length;
                    }
                }
                else
                {
                    //Write response body from memory
                    while (_memoryResponse.Remaining > 0)
                    {
                        //Get remaining segment
                        _readSegment = _memoryResponse.GetMemory();

                        //Write segment to output stream
                        await dest.WriteAsync(_readSegment);

                        //Advance by the written amount
                        _memoryResponse.Advance(_readSegment.Length);
                    }
                }

                //Disposing of memory response can be deferred until the end of the request since its always syncrhonous
            }
            else
            {
                if (count > 0)
                {
                    //Buffer is required, and count must be supplied

                    long total = 0;
                    int read;
                    while (true)
                    {
                        //get offset wrapper of the total buffer or remaining count
                        Memory<byte> offset = buffer[..(int)Math.Min(buffer.Length, count - total)];
                        //read
                        read = await _streamResponse!.ReadAsync(offset);
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
                }
                else
                {
                    //Read in loop
                    do
                    {
                        //read
                        int read = await _streamResponse!.ReadAsync(buffer);
                        //Guard
                        if (read == 0)
                        {
                            break;
                        }

                        //write only the data that was read (slice)
                        await dest.WriteAsync(buffer[..read]);

                    } while (true);
                }

                //Try to dispose the response stream asyncrhonously since we are done with it
                await _streamResponse!.DisposeAsync();

                //remove ref so its not disposed again
                _streamResponse = null;
            }
        }

        ForwardOnlyMemoryReader<byte> _streamReader;

        ///<inheritdoc/>        
        async Task IHttpResponseBody.WriteEntityAsync(IResponseCompressor comp, IResponseDataWriter writer, Memory<byte> buffer)
        {
            //Locals
            int read;

            //Write a sliding window response
            if (_memoryResponse != null)
            {             
                while (_memoryResponse.Remaining > 0)
                {
                    //Commit output bytes
                    if (CompressNextSegment(_memoryResponse, comp, writer))
                    {
                        //Time to flush
                        await writer.FlushAsync(false);
                    }
                }

                //Disposing of memory response can be deferred until the end of the request since its always syncrhonous
            }
            else
            {
                //Trim buffer to block size if it is set by the compressor
                if (comp.BlockSize > 0)
                {
                    buffer = buffer[..comp.BlockSize];
                }

                //Process in loop
                do
                {
                    //read
                    read = await _streamResponse!.ReadAsync(buffer, CancellationToken.None);

                    //Guard
                    if (read == 0)
                    {
                        break;
                    }

                    //Track read bytes and loop uil all bytes are read
                    _streamReader = new(buffer[..read]);
                  
                    do
                    {
                        //Compress the buffered data and flush if required
                        if (CompressNextSegment(ref _streamReader, comp, writer))
                        {
                            //Time to flush
                            await writer.FlushAsync(false);
                        }

                    } while (_streamReader.WindowSize > 0);

                } while (true);

                /*
                 * Try to dispose the response stream asyncrhonously since we can safley here
                 * otherwise it will be deferred until the end of the request cleanup
                 */
                await _streamResponse!.DisposeAsync();

                //remove ref so its not disposed again
                _streamResponse = null;
            }


            /*
             * Once there is no more response data avialable to compress
             * we need to flush the compressor, then flush the writer
             * to publish all accumulated data to the client
             */

            do
            {
                //Get output buffer
                Memory<byte> output = writer.GetMemory();

                //Flush the compressor output
                int written = comp.Flush(output);

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
        
        private static bool CompressNextSegment(IMemoryResponseReader reader, IResponseCompressor comp, IResponseDataWriter writer)
        {
            //Read the next segment
            ReadOnlyMemory<byte> readSegment = comp.BlockSize > 0 ? reader.GetRemainingConstrained(comp.BlockSize) : reader.GetMemory();

            //Get output buffer
            Memory<byte> output = writer.GetMemory();

            //Compress the trimmed block
            CompressionResult res = comp.CompressBlock(readSegment, output);

            //Commit input bytes
            reader.Advance(res.BytesRead);

            return writer.Advance(res.BytesWritten) == 0;
        }

        private static bool CompressNextSegment(ref ForwardOnlyMemoryReader<byte> reader, IResponseCompressor comp, IResponseDataWriter writer)
        {
            //Get output buffer
            Memory<byte> output = writer.GetMemory();

            //Compress the trimmed block
            CompressionResult res = comp.CompressBlock(reader.Window, output);

            //Commit input bytes
            reader.Advance(res.BytesRead);

            return writer.Advance(res.BytesWritten) == 0;
        }

#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnComplete()
        {
            //Clear has data flag
            HasData = false;
            Length = 0;
            _readSegment = default;
            _streamReader = default;

            //Clear rseponse containers
            _streamResponse?.Dispose();
            _streamResponse = null;
            _memoryResponse?.Close();
            _memoryResponse = null;
        }
    }
}