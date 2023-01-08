/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Utils.IO;

using static VNLib.Net.Http.Core.CoreBufferHelpers;

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// A specialized <see cref="IDataAccumulator{T}"/> for buffering data 
    /// in Http/1.1 chunks
    /// </summary>
    internal class ChunkDataAccumulator : IDataAccumulator<byte>, IHttpLifeCycle
    {
        public const int RESERVED_CHUNK_SUGGESTION = 32;

        private readonly int BufferSize;
        private readonly int ReservedSize;
        private readonly Encoding Encoding;
        private readonly ReadOnlyMemory<byte> CRLFBytes;

        public ChunkDataAccumulator(Encoding encoding, int bufferSize)
        {
            Encoding = encoding;
            CRLFBytes = encoding.GetBytes(HttpHelpers.CRLF);
           
            ReservedSize = RESERVED_CHUNK_SUGGESTION;
            BufferSize = bufferSize;
        }

        private byte[]? _buffer;
        private int _reservedOffset;
        

        ///<inheritdoc/>
        public int RemainingSize => _buffer!.Length - AccumulatedSize;
        ///<inheritdoc/>
        public Span<byte> Remaining => _buffer!.AsSpan(AccumulatedSize);
        ///<inheritdoc/>
        public Span<byte> Accumulated => _buffer!.AsSpan(_reservedOffset, AccumulatedSize);
        ///<inheritdoc/>
        public int AccumulatedSize { get; set; }

        private Memory<byte> CompleteChunk => _buffer.AsMemory(_reservedOffset, (AccumulatedSize - _reservedOffset));

        /// <summary>
        /// Attempts to buffer as much data as possible from the specified data
        /// </summary>
        /// <param name="data">The data to copy</param>
        /// <returns>The number of bytes that were buffered</returns>
        public ERRNO TryBufferChunk(ReadOnlySpan<byte> data)
        {
            //Calc data size and reserve space for final crlf
            int dataToCopy = Math.Min(data.Length, RemainingSize - CRLFBytes.Length);

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
            //First reserve the chunk window by advancing the accumulator to the size
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
        /// Writes the buffered data as a single chunk to the stream asynchronously. The internal
        /// state is reset if writing compleded successfully
        /// </summary>
        /// <param name="output">The stream to write data to</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A value task that resolves when the data has been written to the stream</returns>
        public async ValueTask FlushAsync(Stream output, CancellationToken cancellation)
        {
            //Update the chunk size
            UpdateChunkSize();

            //Write trailing chunk delimiter
            this.Append(CRLFBytes.Span);

            //write to stream
            await output.WriteAsync(CompleteChunk, cancellation);

            //Reset for next chunk
            Reset();
        }

        /// <summary>
        /// Writes the buffered data as a single chunk to the stream. The internal
        /// state is reset if writing compleded successfully
        /// </summary>
        /// <param name="output">The stream to write data to</param>
        /// <returns>A value task that resolves when the data has been written to the stream</returns>
        public void Flush(Stream output)
        {
            //Update the chunk size
            UpdateChunkSize();

            //Write trailing chunk delimiter
            this.Append(CRLFBytes.Span);

            //write to stream
            output.Write(CompleteChunk.Span);

            //Reset for next chunk
            Reset();
        }

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
            int initOffset = Encoding.GetBytes(s[..written], encBuf);

            Span<byte> encoded = encBuf[..initOffset];

            /*
             * We need to calcuate how to store the encoded buffer directly
             * before the accumulated chunk data.
             * 
             * This requires us to properly upshift the reserved buffer to 
             * the exact size required to store the encoded chunk size
             */

            _reservedOffset = (ReservedSize - (initOffset + CRLFBytes.Length));
            
            Span<byte> upshifted = _buffer!.AsSpan(_reservedOffset, ReservedSize);

            //First write the chunk size
            encoded.CopyTo(upshifted);

            //Upshift again to write the crlf
            upshifted = upshifted[initOffset..];

            //Copy crlf
            CRLFBytes.Span.CopyTo(upshifted);
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

        public void OnPrepare()
        {
            //Alloc buffer
            _buffer = HttpBinBufferPool.Rent(BufferSize);
        }

        public void OnRelease()
        {
            HttpBinBufferPool.Return(_buffer!);
            _buffer = null;
        }

    }
}