/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ForwardOnlyReader.cs 
*
* ForwardOnlyReader.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Runtime.CompilerServices;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// A mutable structure used to implement a simple forward only 
    /// reader for a memory segment
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    public ref struct ForwardOnlyReader<T>
    {
        private readonly ReadOnlySpan<T> _segment;
        private readonly int _size;

        private int _position;

        /// <summary>
        /// Initializes a new <see cref="ForwardOnlyReader{T}"/>
        /// of the specified type using the specified internal buffer
        /// </summary>
        /// <param name="buffer">The buffer to read from</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ForwardOnlyReader(ReadOnlySpan<T> buffer)
        {
            _segment = buffer;
            _size = buffer.Length;
            _position = 0;
        }

        /// <summary>
        /// Initializes a new <see cref="ForwardOnlyReader{T}"/>
        /// of the specified type using the specified internal buffer
        /// beginning at the specified offset
        /// </summary>
        /// <param name="buffer">The buffer to read from</param>
        /// <param name="offset">The offset within the supplied buffer to begin the reader at</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ForwardOnlyReader(ReadOnlySpan<T> buffer, int offset)
        {
            _segment = buffer[offset..];
            _size = _segment.Length;
            _position = 0;
        }

        /// <summary>
        /// The remaining data window
        /// </summary>
        public readonly ReadOnlySpan <T> Window => _segment[_position..];

        /// <summary>
        /// The number of elements remaining in the window
        /// </summary>
        public readonly int WindowSize => _size - _position;

        /// <summary>
        /// Advances the window position the specified number of elements
        /// </summary>
        /// <param name="count">The number of elements to advance the window position</param>
        public void Advance(int count) => _position += count;

        /// <summary>
        /// Resets the sliding window to the beginning of the buffer
        /// </summary>
        public void Reset() => _position = 0;
    }
}
