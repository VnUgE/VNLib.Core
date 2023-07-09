/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ChunkDataAccumulator.cs 
*
* ChunkDataAccumulator.cs is part of VNLib.Net.Http which is part of the larger 
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

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core.Response
{
    /// <summary>
    /// A specialized <see cref="IDataAccumulator{T}"/> for buffering data 
    /// in Http/1.1 chunks
    /// </summary>
    internal class ChunkDataAccumulator : IDataAccumulator<byte>
    {
        public const int RESERVED_CHUNK_SUGGESTION = 32;
       
        private readonly int ReservedSize;
        private readonly IHttpContextInformation Context;
        private readonly IChunkAccumulatorBuffer Buffer;
        

        public ChunkDataAccumulator(IChunkAccumulatorBuffer buffer, IHttpContextInformation context)
        {
            ReservedSize = RESERVED_CHUNK_SUGGESTION;

            Context = context;
            Buffer = buffer;
        }
      
        /*
         * Reserved offset is a pointer to the first byte of the reserved chunk window
         * that actually contains the size segment data.
         */
      
        private int _reservedOffset;
        

        ///<inheritdoc/>
        public int RemainingSize => Buffer.Size - AccumulatedSize;

        ///<inheritdoc/>
        public Span<byte> Remaining => Buffer.GetBinSpan()[AccumulatedSize..];

        ///<inheritdoc/>
        public Span<byte> Accumulated => Buffer.GetBinSpan()[_reservedOffset.. AccumulatedSize];

        ///<inheritdoc/>
        public int AccumulatedSize { get; set; }

        /*
         * Completed chunk is the segment of the buffer that contains the size segment
         * followed by the accumulated chunk data, and the trailing crlf.
         * 
         * AccumulatedSize points to the end of the accumulated chunk data. The reserved
         * offset points to the start of the size segment.
         */
        private Memory<byte> GetCompleteChunk() => Buffer.GetMemory()[_reservedOffset..AccumulatedSize];

        /// <summary>
        /// Attempts to buffer as much data as possible from the specified data
        /// </summary>
        /// <param name="data">The data to copy</param>
        /// <returns>The number of bytes that were buffered</returns>
        public ERRNO TryBufferChunk(ReadOnlySpan<byte> data)
        {
            //Calc data size and reserve space for final crlf
            int dataToCopy = Math.Min(data.Length, RemainingSize - Context.EncodedSegments.CrlfBytes.Length);

            //Write as much data as possible
            data[..dataToCopy].CopyTo(Remaining);

            //Advance buffer
            Advance(dataToCopy);

            //Return number of bytes not written
            return dataToCopy;
        }

        ///<inheritdoc/>
        public void Advance(int count) => AccumulatedSize += count;

        private void InitReserved()
        {
            //First reserve the chunk window by advancing the accumulator to the reserved size
            Advance(ReservedSize);
        }
       
        ///<inheritdoc/>
        public void Reset()
        {
            //zero offsets
            _reservedOffset = 0;
            AccumulatedSize = 0;
            //Init reserved segment
            InitReserved();
        }

        /// <summary>
        /// Complets and returns the memory segment containing the chunk data to send 
        /// to the client. This also resets the accumulator.
        /// </summary>
        /// <returns></returns>
        public Memory<byte> GetChunkData()
        {
            //Update the chunk size
            UpdateChunkSize();

            //Write trailing chunk delimiter
            this.Append(Context.EncodedSegments.CrlfBytes.Span);

            return GetCompleteChunk();
        }

        /// <summary>
        /// Complets and returns the memory segment containing the chunk data to send 
        /// to the client.
        /// </summary>
        /// <returns></returns>
        public Memory<byte> GetFinalChunkData()
        {
            //Update the chunk size
            UpdateChunkSize();

            //Write trailing chunk delimiter
            this.Append(Context.EncodedSegments.CrlfBytes.Span);

            //Write final chunk to the end of the accumulator
            this.Append(Context.EncodedSegments.FinalChunkTermination.Span);

            return GetCompleteChunk();
        }


        /*
         * UpdateChunkSize method updates the running total of the chunk size
         * in the reserved segment of the buffer. This is because http chunking 
         * requires hex encoded chunk sizes to be written as the first bytes of 
         * the chunk. So when the flush methods are called, the chunk size 
         * at the beginning of the chunk is updated to reflect the total size.
         * 
         * Because we need to store space at the head of the chunk for the size
         * we need to reserve space for the size segment.
         * 
         * The size sigment bytes abutt the chunk data bytes, so the size segment
         * is stored at the end of the reserved segment, which is directly before
         * the start of the chunk data.
         * 
         * [reserved segment] [chunk data] [eoc]
         * [...0a\r\n] [10 bytes of data] [eoc]
         */

        private void UpdateChunkSize()
        {
            const int CharBufSize = 2 * sizeof(int);

            /*
             * Alloc stack buffer to store chunk size hex chars
             * the size of the buffer should be at least the number 
             * of bytes of the max chunk size
             */
            Span<char> s = stackalloc char[CharBufSize];
            
            //Chunk size is the accumulated size without the reserved segment
            int chunkSize = (AccumulatedSize - ReservedSize);

            //format the chunk size
            chunkSize.TryFormat(s, out int written, "x");

            //temp buffer to store encoded data in
            Span<byte> encBuf = stackalloc byte[ReservedSize];
            //Encode the chunk size chars
            int initOffset = Context.Encoding.GetBytes(s[..written], encBuf);

            Span<byte> encoded = encBuf[..initOffset];

            /*
             * We need to calcuate how to store the encoded buffer directly
             * before the accumulated chunk data.
             * 
             * This requires us to properly upshift the reserved buffer to 
             * the exact size required to store the encoded chunk size
             */

            _reservedOffset = (ReservedSize - (initOffset + Context.EncodedSegments.CrlfBytes.Length));
            
            Span<byte> upshifted = Buffer.GetBinSpan()[_reservedOffset..ReservedSize];

            //First write the chunk size
            encoded.CopyTo(upshifted);

            //Upshift again to write the crlf
            upshifted = upshifted[initOffset..];

            //Copy crlf
            Context.EncodedSegments.CrlfBytes.Span.CopyTo(upshifted);
        }


        public void OnNewRequest()
        {
            InitReserved();
        }

        public void OnComplete()
        {
            //Zero offsets
            _reservedOffset = 0;
            AccumulatedSize = 0;
        }
    }
}