/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: SubSequence.cs 
*
* SubSequence.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Represents a subset (or window) of data within a <see cref="MemoryHandle{T}"/>
    /// </summary>
    /// <typeparam name="T">The unmanaged type to wrap</typeparam>
    public readonly struct SubSequence<T> : IEquatable<SubSequence<T>> where T: unmanaged 
    {
        private readonly MemoryHandle<T> _handle;
        /// <summary>
        /// The number of elements in the current window
        /// </summary>
        public readonly int Size { get; }

        /// <summary>
        /// Creates a new <see cref="SubSequence{T}"/> to the handle to get a window of the block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
#if TARGET_64_BIT
        public SubSequence(MemoryHandle<T> block, ulong offset, int size)
#else
        public SubSequence(MemoryHandle<T> block, int offset, int size)
#endif
        {
            _offset = offset;
            Size = size >= 0 ? size : throw new ArgumentOutOfRangeException(nameof(size));
            _handle = block ?? throw new ArgumentNullException(nameof(block));
        }


#if TARGET_64_BIT
        private readonly ulong _offset;
#else
        private readonly int _offset;
#endif
        /// <summary>
        /// Gets a <see cref="Span{T}"/> that is offset from the base of the handle
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>

#if TARGET_64_BIT 
        public readonly Span<T> Span => Size > 0 ? _handle.GetOffsetSpan(_offset, Size) : Span<T>.Empty; 
#else
        public readonly Span<T> Span => Size > 0 ? _handle.Span.Slice(_offset, Size) : Span<T>.Empty;
#endif

        /// <summary>
        /// Slices the current sequence into a smaller <see cref="SubSequence{T}"/>
        /// </summary>
        /// <param name="offset">The relative offset from the current window offset</param>
        /// <param name="size">The size of the block</param>
        /// <returns>A <see cref="SubSequence{T}"/> of the current sequence</returns>
        public readonly SubSequence<T> Slice(uint offset, int size) => new (_handle, _offset + checked((int)offset), size);

        /// <summary>
        /// Returns the signed 32-bit hashcode
        /// </summary>
        /// <returns>A signed 32-bit integer that represents the hashcode for the current instance</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly override int GetHashCode() => _handle.GetHashCode() + _offset.GetHashCode();

        ///<inheritdoc/>
        public readonly bool Equals(SubSequence<T> other) => Span.SequenceEqual(other.Span);

        ///<inheritdoc/>
        public readonly override bool Equals(object? obj) => obj is SubSequence<T> other && Equals(other);

        /// <summary>
        /// Determines if two <see cref="SubSequence{T}"/> are equal
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns>True if the sequences are equal, false otherwise</returns>
        public static bool operator ==(SubSequence<T> left, SubSequence<T> right) => left.Equals(right);
        /// <summary>
        /// Determines if two <see cref="SubSequence{T}"/> are not equal
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns>True if the sequences are not equal, false otherwise</returns>
        public static bool operator !=(SubSequence<T> left, SubSequence<T> right) => !left.Equals(right);
    }
}