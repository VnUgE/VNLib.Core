/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: InitDataBuffer.cs 
*
* InitDataBuffer.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// A structure that buffers data remaining from an initial transport read. Stored 
    /// data will be read by copying.
    /// </summary>
    internal readonly record struct InitDataBuffer
    {
        const int POSITION_SEG_SIZE = sizeof(int);

        readonly int _dataSize;
        readonly byte[] _buffer;
        readonly ArrayPool<byte> _pool;
               

        InitDataBuffer(ArrayPool<byte> pool, byte[] buffer, int size)
        {
            _pool = pool;
            _buffer = buffer;
            _dataSize = size;
        }

        /// <summary>
        /// Allocates the correct size buffer for the given data size
        /// </summary>
        /// <param name="pool">The pool to allocate the array from</param>
        /// <param name="dataSize">The size of the remaining data segment</param>
        /// <returns>The newly allocated data buffer</returns>
        internal static InitDataBuffer AllocBuffer(ArrayPool<byte> pool, int dataSize)
        {
            //Alloc buffer, must be zeroed
            byte[] buffer = pool.Rent(dataSize + POSITION_SEG_SIZE, true);

            return new(pool, buffer, dataSize);
        }

        readonly Span<byte> _positionSegment
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.AsSpan(0, POSITION_SEG_SIZE);
        }

        /// <summary>
        /// Gets the entire internal data segment to read/write data to/from
        /// </summary>
        internal readonly Span<byte> DataSegment
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.AsSpan(POSITION_SEG_SIZE, _dataSize);
        }

        private readonly int Position
        {
            //Reading/wriging the data buffe postion segment
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MemoryMarshal.Read<int>(_positionSegment);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => MemoryMarshal.Write(_positionSegment, ref value);
        }

        /// <summary>
        /// Get the amount of data remaining in the data buffer
        /// </summary>
        internal readonly int Remaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dataSize - Position;
        }

        /// <summary>
        /// Reads data from the internal buffer into the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to write data to</param>
        /// <returns>The number of bytes written to the output buffer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly ERRNO Read(Span<byte> buffer)
        {
            //Calc how many bytes can be read into the output buffer
            int bytesToRead = Math.Min(Remaining, buffer.Length);

            Span<byte> btr = DataSegment.Slice(Position, bytesToRead);

            //Write data to output buffer
            btr.CopyTo(buffer);

            //Update position pointer
            Position += bytesToRead;

            //Return the number of bytes read
            return bytesToRead;
        }

        /// <summary>
        /// Releases the internal buffer back to its pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void Release()
        {
            //Return buffer back to pool
            _pool.Return(_buffer);
        }
    }
}
