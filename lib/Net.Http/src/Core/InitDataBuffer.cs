/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory;

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// A structure that buffers data remaining from an initial transport read. Stored 
    /// data will be read by copying.
    /// </summary>
    internal readonly struct InitDataBuffer(ArrayPool<byte> pool, byte[] buffer, int size)
    {
        const int POSITION_SEG_SIZE = sizeof(int);

        readonly int _dataSize = size;
        readonly byte[] _buffer = buffer;
        readonly ArrayPool<byte> _pool = pool;
     

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

        private readonly Span<byte> _positionSegment
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
            set => MemoryMarshal.Write(_positionSegment, in value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly int GetDataPosition()
        {
            Debug.Assert(Position >= 0 && Position <= _dataSize, "Invalid position value");
            //Points to the first byte of the data segment to read from
            return POSITION_SEG_SIZE + Position;
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
        /// Performs a discard in a single operation by setting the 
        /// position to the end of the data buffer
        /// </summary>
        /// <returns>The number of bytes that were remaining in the buffer before the discard</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly int DiscardRemaining()
        {
            int remaining = Remaining;
            Position = _dataSize;
            return remaining;
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
           
            MemoryUtil.Memmove(
                ref MemoryMarshal.GetArrayDataReference(_buffer),
                (nuint)GetDataPosition(),
                ref MemoryMarshal.GetReference(buffer),
                0, 
                (nuint)bytesToRead
            );

            //Update position pointer
            Position += bytesToRead;

            //Return the number of bytes read
            return bytesToRead;
        }

        /// <summary>
        /// Releases the internal buffer back to its pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void Release() => _pool.Return(_buffer);
    }
}
