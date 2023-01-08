/*
* Copyright (c) 2022 Vaughn Nugent
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


namespace VNLib.Net.Http.Core
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
    internal sealed class ResponseWriter : IHttpResponseBody, IHttpLifeCycle
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

        ///<inheritdoc/>
        async Task IHttpResponseBody.WriteEntityAsync(Stream dest, long count, Memory<byte>? buffer, CancellationToken token)
        {
            //Write a sliding window response
            if (_memoryResponse != null)
            {
                //Get min value from count/range length
                int remaining = (int)Math.Min(count, _memoryResponse.Remaining);

                //Write response body from memory
                while (remaining > 0)
                {
                    //Get remaining segment
                    ReadOnlyMemory<byte> segment = _memoryResponse.GetRemainingConstrained(remaining);

                    //Write segment to output stream
                    await dest.WriteAsync(segment, token);

                    int written = segment.Length;

                    //Advance by the written ammount
                    _memoryResponse.Advance(written);

                    //Update remaining
                    remaining -= written;
                }
            }
            else
            {
                //Buffer is required, and count must be supplied
                await _streamResponse!.CopyToAsync(dest, buffer!.Value, count, token);
            }
        }

        ///<inheritdoc/>        
        async Task IHttpResponseBody.WriteEntityAsync(Stream dest, Memory<byte>? buffer, CancellationToken token)
        {
            //Write a sliding window response
            if (_memoryResponse != null)
            {
                //Write response body from memory
                while (_memoryResponse.Remaining > 0)
                {
                    //Get segment
                    ReadOnlyMemory<byte> segment = _memoryResponse.GetMemory();

                    await dest.WriteAsync(segment, token);

                    //Advance by
                    _memoryResponse.Advance(segment.Length);
                }
            }
            else
            {
                //Buffer is required
                await _streamResponse!.CopyToAsync(dest, buffer!.Value, token);

                //Try to dispose the response stream
                await _streamResponse!.DisposeAsync();
                
                //remove ref
                _streamResponse = null;
            }
        }

        ///<inheritdoc/>
        void IHttpLifeCycle.OnPrepare()
        {}
        
        ///<inheritdoc/>
        void IHttpLifeCycle.OnRelease()
        {}

        ///<inheritdoc/>
        void IHttpLifeCycle.OnNewRequest()
        {}

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