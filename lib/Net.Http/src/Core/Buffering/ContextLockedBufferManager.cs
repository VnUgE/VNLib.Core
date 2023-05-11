/*
* Copyright (c) 2023 Vaughn Nugent
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

using VNLib.Utils.Memory;

namespace VNLib.Net.Http.Core.Buffering
{

    internal class ContextLockedBufferManager : IHttpBufferManager
    {
        private readonly HttpBufferConfig Config;
        private readonly int TotalBufferSize;

        private readonly HeaderAccumulatorBuffer _requestHeaderBuffer;
        private readonly HeaderAccumulatorBuffer _responseHeaderBuffer;
        private readonly ChunkAccBuffer _chunkAccBuffer;

        public ContextLockedBufferManager(in HttpBufferConfig config)
        {
            Config = config;

            //Compute total buffer size from server config
            TotalBufferSize = ComputeTotalBufferSize(in config);

             /*
              * Individual instances of the header accumulator buffer are required
              * because the user controls the size of the binary buffer for responses 
              * and requests. The buffer segment is shared between the two instances.
              */
            _requestHeaderBuffer = new(config.RequestHeaderBufferSize);           
            _responseHeaderBuffer = new(config.ResponseHeaderBufferSize);

            _chunkAccBuffer = new();
        }

        private IMemoryOwner<byte>? _handle;
        private HttpBufferSegments<byte> _segments;

        #region LifeCycle

        ///<inheritdoc/>
        public void AllocateBuffer(IHttpMemoryPool allocator)
        {
            //Alloc a single buffer for the entire context
            _handle = allocator.AllocateBufferForContext(TotalBufferSize);

            try
            {
                Memory<byte> full = _handle.Memory;

                //Header parse buffer is a special case as it will be double the size due to the char buffer
                int headerParseBufferSize = GetMaxHeaderBufferSize(in Config);

                //Discard/form data buffer
                int discardAndFormDataSize = ComputeDiscardFormataBufferSize(in Config);

                //Slice and store the buffer segments
                _segments = new()
                {
                    //Shared header buffer
                    HeaderAccumulator = GetNextSegment(ref full, headerParseBufferSize),

                    //Shared discard buffer and form data buffer
                    DiscardAndFormData = GetNextSegment(ref full, discardAndFormDataSize),

                    //Buffers cannot be shared
                    ChunkedResponseAccumulator = GetNextSegment(ref full, Config.ChunkedResponseAccumulatorSize),

                    ResponseBuffer = GetNextSegment(ref full, Config.ResponseBufferSize),
                };

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
                //Free buffer on error
                _handle.Dispose();
                _handle = null;
                throw;
            }
        }

        ///<inheritdoc/>
        public void ZeroAll()
        {
            //Zero the buffer completely
            MemoryUtil.InitializeBlock(_handle.Memory);
        }

        ///<inheritdoc/>
        public void FreeAll()
        {
            //Clear buffer memory structs to allow gc
            _requestHeaderBuffer.FreeBuffer();
            _responseHeaderBuffer.FreeBuffer();
            _chunkAccBuffer.FreeBuffer();

            //Clear segments
            _segments = default;

            //Free buffer
            if (_handle != null)
            {
                _handle.Dispose();
                _handle = null;
            }
        }

        #endregion

        ///<inheritdoc/>
        public IHttpHeaderParseBuffer RequestHeaderParseBuffer => _requestHeaderBuffer;

        ///<inheritdoc/>
        public IResponseHeaderAccBuffer ResponseHeaderBuffer => _responseHeaderBuffer;

        ///<inheritdoc/>
        public IChunkAccumulatorBuffer ChunkAccumulatorBuffer => _chunkAccBuffer;


        /*
         * Discard buffer may be used for form-data parsing as they will never be used at 
         * the same time during normal operation
         */

        ///<inheritdoc/>
        public Memory<byte> GetFormDataBuffer() => _segments.DiscardAndFormData;

        ///<inheritdoc/>
        public Memory<byte> GetDiscardBuffer() => _segments.DiscardAndFormData;

        ///<inheritdoc/>
        public Memory<byte> GetResponseDataBuffer() => _segments.ResponseBuffer;



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

        static int ComputeTotalBufferSize(in HttpBufferConfig config)
        {
            return config.ResponseBufferSize
                + config.ChunkedResponseAccumulatorSize
                + ComputeDiscardFormataBufferSize(in config)
                + GetMaxHeaderBufferSize(in config);    //Header buffers are shared
        }

        static int ComputeDiscardFormataBufferSize(in HttpBufferConfig config)
        {
            //Get the larger of the two buffers, so it can be shared between the two
            return Math.Max(config.DiscardBufferSize, config.FormDataBufferSize);
        }


        readonly record struct HttpBufferSegments<T>
        {
            public readonly Memory<T> HeaderAccumulator { get; init; }
            public readonly Memory<T> ChunkedResponseAccumulator { get; init; }
            public readonly Memory<T> DiscardAndFormData { get; init; }
            public readonly Memory<T> ResponseBuffer { get; init; }
        }
      

        private sealed class HeaderAccumulatorBuffer: SplitHttpBufferElement, IResponseHeaderAccBuffer, IHttpHeaderParseBuffer
        {
            public HeaderAccumulatorBuffer(int binSize):base(binSize)
            { }
        }

        private sealed class ChunkAccBuffer : HttpBufferElement, IChunkAccumulatorBuffer
        { }
    }
}
