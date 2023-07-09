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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;
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

        ///<inheritdoc/>
        async Task IHttpResponseBody.WriteEntityAsync(Stream dest, long count, Memory<byte> buffer)
        {
            int remaining;
            ReadOnlyMemory<byte> segment;

            //Write a sliding window response
            if (_memoryResponse != null)
            {
                //Get min value from count/range length
                remaining = (int)Math.Min(count, _memoryResponse.Remaining);

                //Write response body from memory
                while (remaining > 0)
                {
                    //Get remaining segment
                    segment = _memoryResponse.GetRemainingConstrained(remaining);
                    
                    //Write segment to output stream
                    await dest.WriteAsync(segment);
                  
                    //Advance by the written ammount
                    _memoryResponse.Advance(segment.Length);

                    //Update remaining
                    remaining -= segment.Length;
                }
            }
            else
            {
                //Buffer is required, and count must be supplied
                await _streamResponse!.CopyToAsync(dest, buffer, count);

                //Try to dispose the response stream asyncrhonously since we are done with it
                await _streamResponse!.DisposeAsync();

                //remove ref so its not disposed again
                _streamResponse = null;
            }
        }

        ///<inheritdoc/>
        async Task IHttpResponseBody.WriteEntityAsync(Stream dest, Memory<byte> buffer)
        {
            ReadOnlyMemory<byte> segment;

            //Write a sliding window response
            if (_memoryResponse != null)
            {
                //Write response body from memory
                while (_memoryResponse.Remaining > 0)
                {
                    //Get remaining segment
                    segment = _memoryResponse.GetMemory();

                    //Write segment to output stream
                    await dest.WriteAsync(segment);

                    //Advance by the written ammount
                    _memoryResponse.Advance(segment.Length);
                }
            }
            else
            {
                //Buffer is required, and count must be supplied
                await _streamResponse!.CopyToAsync(dest, buffer);

                //Try to dispose the response stream asyncrhonously since we are done with it
                await _streamResponse!.DisposeAsync();

                //remove ref so its not disposed again
                _streamResponse = null;
            }
        }

        ///<inheritdoc/>        
        async Task IHttpResponseBody.WriteEntityAsync(IResponseCompressor dest, Memory<byte> buffer)
        {
            //Locals
            bool remaining;
            int read;
            ReadOnlyMemory<byte> segment;

            //Write a sliding window response
            if (_memoryResponse != null)
            {
                /*
                 * It is safe to assume if a response body was set, that it contains data.
                 * So the cost or running a loop without data is not a concern.
                 * 
                 * Since any failed writes to the output will raise exceptions, it is safe
                 * to advance the reader before writing the data, so we can determine if the
                 * block is final. 
                 * 
                 * Since we are using a byte-stream reader for memory responses, we can optimize the 
                 * compression loop, if we know its operating block size, so we only compress blocks
                 * of the block size, then continue the loop without branching or causing nested
                 * loops
                 */            

                //Optimize for block size
                if (dest.BlockSize > 0)
                {
                    //Write response body from memory
                    do
                    {
                        segment = _memoryResponse.GetRemainingConstrained(dest.BlockSize);

                        //Advance by the trimmed segment length
                        _memoryResponse.Advance(segment.Length);

                        //Check if data is remaining after an advance
                        remaining = _memoryResponse.Remaining > 0;

                        //Compress the trimmed block
                        await dest.CompressBlockAsync(segment, !remaining);

                    } while (remaining);
                }
                else
                {
                    do
                    {
                        segment = _memoryResponse.GetMemory();

                        //Advance by the segment length, this should be safe even if its zero
                        _memoryResponse.Advance(segment.Length);

                        //Check if data is remaining after an advance
                        remaining = _memoryResponse.Remaining > 0;

                        //Write to output 
                        await dest.CompressBlockAsync(segment, !remaining);

                    } while (remaining);
                }

                //Disposing of memory response can be deferred until the end of the request since its always syncrhonous
            }
            else
            {
                //Trim buffer to block size if it is set by the compressor
                if (dest.BlockSize > 0)
                {
                    buffer = buffer[..dest.BlockSize];
                }

                //Read in loop
                do
                {
                    //read
                    read = await _streamResponse!.ReadAsync(buffer, CancellationToken.None);

                    //Guard
                    if (read == 0)
                    {
                        break;
                    }

                    //write only the data that was read, as a segment instead of a block
                    await dest.CompressBlockAsync(buffer[..read], read < buffer.Length);

                } while (true);

                /*
                 * Try to dispose the response stream asyncrhonously since we can safley here
                 * otherwise it will be deferred until the end of the request cleanup
                 */
                await _streamResponse!.DisposeAsync();

                //remove ref so its not disposed again
                _streamResponse = null;
            }
        }

#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
       

        public void OnComplete()
        {
            //Clear has data flag
            HasData = false;
            Length = 0;

            //Clear rseponse containers
            _streamResponse?.Dispose();
            _streamResponse = null;
            _memoryResponse?.Close();
            _memoryResponse = null;
        }
    }
}