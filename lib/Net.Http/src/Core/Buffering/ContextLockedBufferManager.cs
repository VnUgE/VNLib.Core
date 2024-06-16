/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Buffers;
using System.Diagnostics;

using VNLib.Utils.Memory;

namespace VNLib.Net.Http.Core.Buffering
{

    internal sealed class ContextLockedBufferManager(in HttpBufferConfig config, bool chunkingEnabled) : IHttpBufferManager
    {
        private readonly HttpBufferConfig Config = config;
        private readonly bool _chunkingEnabled = chunkingEnabled;
        private readonly int TotalBufferSize = ComputeTotalBufferSize(in config, chunkingEnabled);

        private readonly HeaderAccumulatorBuffer _requestHeaderBuffer = new(config.RequestHeaderBufferSize);
        private readonly HeaderAccumulatorBuffer _responseHeaderBuffer = new(config.ResponseHeaderBufferSize);
        private readonly ChunkAccBuffer _chunkAccBuffer = new();       

        private IMemoryOwner<byte>? _handle;
        private HttpBufferSegments<byte> _segments;

        ///<inheritdoc/>
        public void AllocateBuffer(IHttpMemoryPool allocator)
        {
            Debug.Assert(_handle == null, "Memory Leak: new http buffer alloacted when an existing buffer was not freed.");

            //Alloc a single buffer for the entire context
            _handle = allocator.AllocateBufferForContext(TotalBufferSize);

            try
            {
                Memory<byte> full = _handle.Memory;

                //Header parse buffer is a special case as it will be double the size due to the char buffer
                int headerParseBufferSize = GetMaxHeaderBufferSize(in Config);                
                int responseAndFormDataSize = ComputeResponseAndFormDataBuffer(in Config);

                //Shared header buffer
                _segments.HeaderAccumulator = GetNextSegment(ref full, headerParseBufferSize);
                _segments.ResponseAndFormData = GetNextSegment(ref full, responseAndFormDataSize);

                /*
                 * The chunk accumulator buffer cannot be shared. It is also only
                 * stored if chunking is enabled.
                 */
                _segments.ChunkedResponseAccumulator = _chunkingEnabled 
                    ? GetNextSegment(ref full, Config.ChunkedResponseAccumulatorSize) 
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

                _requestHeaderBuffer.SetBuffer(_segments.HeaderAccumulator);
                _responseHeaderBuffer.SetBuffer(_segments.HeaderAccumulator);

                //Chunk buffer will be used at the same time as the response buffer and discard buffers
                _chunkAccBuffer.SetBuffer(_segments.ChunkedResponseAccumulator);
            }
            catch
            {
                _segments = default;
                //Free buffer on error
                _handle.Dispose();
                _handle = null;
                throw;
            }
        }

        ///<inheritdoc/>
        public void FreeAll(bool zero)
        {
            if (zero)
            {
                MemoryUtil.InitializeBlock(_handle!.Memory);
            }

            //Clear buffer memory structs to allow gc
            _requestHeaderBuffer.FreeBuffer();
            _responseHeaderBuffer.FreeBuffer();
            _chunkAccBuffer.FreeBuffer();

            //Clear segments
            _segments = default;
          
            if (_handle != null)
            {
                _handle.Dispose();
                _handle = null;
            }
        }

        ///<inheritdoc/>
        public IHttpHeaderParseBuffer RequestHeaderParseBuffer => _requestHeaderBuffer;

        ///<inheritdoc/>
        public IResponseHeaderAccBuffer ResponseHeaderBuffer => _responseHeaderBuffer;

        ///<inheritdoc/>
        public IChunkAccumulatorBuffer ChunkAccumulatorBuffer => _chunkAccBuffer;

        public Memory<byte> GetInitStreamBuffer()
        {
            /*
            * Since this buffer must be shared with char buffers, size 
            * must be respected. Remember that split buffesr store binary
            * data at the head of the buffer and char data at the tail
            */

            Memory<byte> dataBuffer = RequestHeaderParseBuffer.GetMemory();

            return dataBuffer[..RequestHeaderParseBuffer.BinSize];
        }


        /*
         * Response buffer and form data buffer are shared because they are never 
         * used at the same time.
         */

        ///<inheritdoc/>
        public Memory<byte> GetFormDataBuffer() => _segments.ResponseAndFormData;

        ///<inheritdoc/>
        public Memory<byte> GetResponseDataBuffer() => _segments.ResponseAndFormData;

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


        struct HttpBufferSegments<T>
        {
            public Memory<T> HeaderAccumulator;
            public Memory<T> ChunkedResponseAccumulator;
            public Memory<T> ResponseAndFormData;
        }  
      

        private sealed class HeaderAccumulatorBuffer(int binSize) : 
            SplitHttpBufferElement(binSize), IResponseHeaderAccBuffer, IHttpHeaderParseBuffer
        { }

        private sealed class ChunkAccBuffer : HttpBufferElement, IChunkAccumulatorBuffer
        { }
    }
}
