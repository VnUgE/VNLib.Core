/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ContextLockedBufferManager.cs 
*
* ContextLockedBufferManager.cs is part of VNLib.Net.Http which is part of the larger 
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
 * This file implements the IHttpBufferManager interface, which provides 
 * all the required buffers for http processing flow. The design was to allocate
 * a single large buffer for the entire context and then slice it up into
 * smaller segments for each buffer use case. Some buffers are shared between
 * operations to reduce total memory usage when it is known that buffer usage
 * will not conflict.
 */

using System;
using System.Diagnostics;

using VNLib.Utils.Memory;

namespace VNLib.Net.Http.Core.Buffering
{

    internal sealed class ContextLockedBufferManager(ref readonly HttpBufferConfig config, bool chunkingEnabled) : IHttpBufferManager
    {
        private readonly bool _chunkingEnabled = chunkingEnabled;
        private readonly bool _zeroOnFree = config.ZeroBuffersOnDisconnect;
        private readonly int TotalBufferSize = ComputeTotalBufferSize(in config, chunkingEnabled);

        private readonly HeaderAccumulatorBuffer _requestHeaderBuffer = new(config.RequestHeaderBufferSize);
        private readonly HeaderAccumulatorBuffer _responseHeaderBuffer = new(config.ResponseHeaderBufferSize);
        private readonly ChunkAccBuffer _chunkAccBuffer = new();       

        private IHttpContextBuffer? _handle;
        private Memory<byte> _responseAndFormDataBuffer;

        ///<inheritdoc/>
        public void AllocateBuffer(IHttpMemoryPool allocator, ref readonly HttpBufferConfig config)
        {
            /*
              * NOTE:
              * If an exception is raised in the following code, it will raise to the 
              * parent context call to allocate. An undefined state is fine becase
              * the call to FreeAll is guaranteed to be called during all control flow
              * as long as the exception is not caught.
              * 
              * So as long as the FreeAll hook cleans up properly everything should be 
              * fine.
              */

            Debug.Assert(_handle == null, "Memory Leak: new http buffer alloacted when an existing buffer was not freed.");
            Debug.Assert(_responseAndFormDataBuffer.IsEmpty);

            //Alloc a single buffer for the entire context
            _handle = allocator.AllocateBufferForContext(TotalBufferSize);

            Memory<byte> full = _handle.Memory;

            //Header parse buffer is a special case as it will be double the size due to the char buffer
            int headerParseBufferSize = GetMaxHeaderBufferSize(in config);
            int responseAndFormDataSize = ComputeResponseAndFormDataBuffer(in config);

            //Shared header buffer
            Memory<byte> headerAccumulator = GetNextSegment(ref full, headerParseBufferSize);
            _responseAndFormDataBuffer = GetNextSegment(ref full, responseAndFormDataSize);

            /*
             * The chunk accumulator buffer cannot be shared. It is also only
             * stored if chunking is enabled.
             */
            Memory<byte> chunkedResponseAccumulator = _chunkingEnabled
                    ? GetNextSegment(ref full, config.ChunkedResponseAccumulatorSize)
                    : default;

            /*
             * ************* WARNING ****************
             * 
             * Request header and response header buffers are shared 
             * because they are assumed to be used in a single threaded context
             * and control flow never allows them to be used at the same time.
             * 
             * The bin buffer size is determined by the buffer config so the 
             * user may still configure the buffer size for restriction, so we
             * just alloc the largerest of the two and use it for requests and 
             * responses.
             * 
             * Control flow may change and become unsafe in the future!
             */

            _requestHeaderBuffer.SetBuffer(headerAccumulator);
            _responseHeaderBuffer.SetBuffer(headerAccumulator);

            /*
             * Chunk buffer will be used at the same time as the 
             * response buffer and discard buffers. 
             */
            _chunkAccBuffer.SetBuffer(chunkedResponseAccumulator);
        }

        ///<inheritdoc/>
        public void FreeAll(IHttpMemoryPool allocator)
        {
            if (_zeroOnFree)
            {
                MemoryUtil.InitializeBlock(_handle!.Memory);
            }

            //Clear buffer memory structs to allow gc
            _requestHeaderBuffer.FreeBuffer();
            _responseHeaderBuffer.FreeBuffer();
            _chunkAccBuffer.FreeBuffer();

            _responseAndFormDataBuffer = default;

            if (_handle != null)
            {
                allocator.FreeBufferForContext(_handle);
                _handle = null;
            }
        }

        ///<inheritdoc/>
        public IHttpHeaderParseBuffer RequestHeaderParseBuffer => _requestHeaderBuffer;

        ///<inheritdoc/>
        public IResponseHeaderAccBuffer ResponseHeaderBuffer => _responseHeaderBuffer;

        ///<inheritdoc/>
        public IChunkAccumulatorBuffer ChunkAccumulatorBuffer => _chunkAccBuffer;

        /// <summary>
        /// Gets the shared buffer used for http request initialization
        /// </summary>
        /// <returns>A memory block buffer incoming transport data to be read by the parsing reader</returns>
        public Memory<byte> GetInitStreamBuffer()
        {
            /*
            * Since this buffer must be shared with char buffers, size 
            * must be respected. Remember that split buffers store binary
            * data at the head of the buffer and char data at the tail
            */

            Memory<byte> dataBuffer = _requestHeaderBuffer.GetMemory();

            return dataBuffer[.._requestHeaderBuffer.BinSize];
        }


        /*
         * Response buffer and form data buffer are shared because they are never 
         * used at the same time.
         */

        ///<inheritdoc/>
        public Memory<byte> GetFormDataBuffer() => _responseAndFormDataBuffer;

        ///<inheritdoc/>
        public Memory<byte> GetResponseDataBuffer() => _responseAndFormDataBuffer;

        static Memory<byte> GetNextSegment(ref Memory<byte> buffer, int size)
        {
            //get segment from current slice
            Memory<byte> segment = buffer[..size];

            //Upshift buffer
            buffer = buffer[size..];

            return segment;
        }

        /*
         * Computes the correct size of the request header buffer from the config
         * so it is large enough to hold the binary buffer but also the split char 
         * buffer
         */
       
        static int GetMaxHeaderBufferSize(in HttpBufferConfig config)
        {
            int max = Math.Max(config.RequestHeaderBufferSize, config.ResponseHeaderBufferSize);

            //Compute the max size including the char buffer
            return SplitHttpBufferElement.GetfullSize(max);
        }

        static int ComputeTotalBufferSize(in HttpBufferConfig config, bool chunkingEnabled)
        {
            int baseSize = config.ResponseBufferSize
                + ComputeResponseAndFormDataBuffer(in config)
                + GetMaxHeaderBufferSize(in config);    //Header buffers are shared

            if (chunkingEnabled)
            {
                //Add chunking buffer
                baseSize += config.ChunkedResponseAccumulatorSize;
            }

            return baseSize;
        }

        static int ComputeResponseAndFormDataBuffer(in HttpBufferConfig config)
        {
            //Get the larger of the two buffers, so it can be shared between the two
            return Math.Max(config.ResponseBufferSize, config.FormDataBufferSize);
        }


        private sealed class HeaderAccumulatorBuffer(int binSize) : 
            SplitHttpBufferElement(binSize), IResponseHeaderAccBuffer, IHttpHeaderParseBuffer
        { }

        private sealed class ChunkAccBuffer : HttpBufferElement, IChunkAccumulatorBuffer
        { }
    }
}
