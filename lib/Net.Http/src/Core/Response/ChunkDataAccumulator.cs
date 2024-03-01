/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Diagnostics;
using System.Runtime.InteropServices;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;

using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core.Response
{

    /// <summary>
    /// A specialized <see cref="IDataAccumulator{T}"/> for buffering data 
    /// in Http/1.1 chunks
    /// </summary>
    internal readonly struct ChunkDataAccumulator(IChunkAccumulatorBuffer Buffer, IHttpContextInformation Context)
    {
        /*
         * The number of bytes to reserve at the beginning of the buffer
         * for the chunk size segment. This is the maximum size of the
         */
        public const int ReservedSize = 16;
      
        /*
        * Must always leave enough room for trailing crlf at the end of 
        * the buffer
        */
        private readonly int TotalMaxBufferSize => Buffer.Size - Context.CrlfSegment.Length;

        /// <summary>
        /// Complets and returns the memory segment containing the chunk data to send 
        /// to the client.
        /// </summary>
        /// <returns></returns>
        public readonly Memory<byte> GetChunkData(int accumulatedSize, bool isFinalChunk)
        {
            //Update the chunk size
            int reservedOffset = UpdateChunkSize(Buffer, Context, accumulatedSize);
            int endPtr = GetPointerToEndOfUsedBuffer(accumulatedSize);

            //Write trailing chunk delimiter
            endPtr += Context.CrlfSegment.DangerousCopyTo(Buffer, endPtr);

            if (isFinalChunk)
            {
                //Write final chunk to the end of the accumulator
                endPtr += Context.FinalChunkSegment.DangerousCopyTo(Buffer, endPtr);
            }

            return Buffer.GetMemory()[reservedOffset..endPtr];
        }

        /// <summary>
        /// Gets the remaining segment of the buffer to write chunk data to.
        /// </summary>
        /// <returns>The chunk buffer to write data to</returns>
        public readonly Memory<byte> GetRemainingSegment(int accumulatedSize)
        {
            int endOfDataOffset = GetPointerToEndOfUsedBuffer(accumulatedSize);
            return Buffer.GetMemory()[endOfDataOffset..TotalMaxBufferSize];
        }

        /// <summary>
        /// Calculates the usable remaining size of the chunk buffer.
        /// </summary>
        /// <returns>The number of bytes remaining in the buffer</returns>
        public readonly int GetRemainingSegmentSize(int accumulatedSize)
            => TotalMaxBufferSize - GetPointerToEndOfUsedBuffer(accumulatedSize);


        private static int GetPointerToEndOfUsedBuffer(int accumulatedSize) => accumulatedSize + ReservedSize;

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

        private static int UpdateChunkSize(IChunkAccumulatorBuffer buffer, IHttpContextInformation context, int chunkSize)
        {
            const int CharBufSize = 2 * sizeof(int) + 2; //2 hex chars per byte + crlf

            /*
             * Alloc stack buffer to store chunk size hex chars
             * the size of the buffer should be at least the number 
             * of bytes of the max chunk size
             */
            Span<char> intFormatBuffer = stackalloc char[CharBufSize];


            //temp buffer to store binary encoded data in
            Span<byte> chunkSizeBinBuffer = stackalloc byte[ReservedSize];

            //format the chunk size
            bool formatSuccess = chunkSize.TryFormat(intFormatBuffer, out int bytesFormatted, "x", null);
            Debug.Assert(formatSuccess, "Failed to write integer chunk size to temp buffer");

            //Write the trailing crlf to the end of the encoded chunk size
            intFormatBuffer[bytesFormatted++] = '\r';
            intFormatBuffer[bytesFormatted++] = '\n';

            //Encode the chunk size chars
            int totalChunkBufferBytes = context.Encoding.GetBytes(intFormatBuffer[..bytesFormatted], chunkSizeBinBuffer);
            Debug.Assert(totalChunkBufferBytes <= ReservedSize, "Chunk size buffer offset is greater than reserved size. Encoding failure");

            /*
             * We need to calcuate how to store the encoded buffer directly
             * before the accumulated chunk data.
             * 
             * This requires us to properly upshift the reserved buffer to 
             * the exact size required to store the encoded chunk size
             */

            int reservedOffset = ReservedSize - totalChunkBufferBytes;

            //Copy encoded chunk size to the reserved segment
            MemoryUtil.SmallMemmove(
                in MemoryMarshal.GetReference(chunkSizeBinBuffer), 
                0, 
                ref buffer.DangerousGetBinRef(reservedOffset), 
                0, 
                (ushort)totalChunkBufferBytes
            );

            return reservedOffset;
        }
    }
}