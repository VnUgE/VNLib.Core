/*
* Copyright (c) 2024 Vaughn Nugent
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
    /// Represents a subset (or window) of data within a <see cref="IMemoryHandle{T}"/>
    /// </summary>
    /// <typeparam name="T">The unmanaged type to wrap</typeparam>
    public readonly record struct SubSequence<T>
    {
        readonly nuint _offset;

        /// <summary>
        /// The handle that owns the memory block
        /// </summary>
        public readonly IMemoryHandle<T> Handle { get; }

        /// <summary>
        /// The number of elements in the current sequence
        /// </summary>
        public readonly int Size { get; }

        /// <summary>
        /// Creates a new <see cref="SubSequence{T}"/> to the handle to get a window of the block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public SubSequence(IMemoryHandle<T> block, nuint offset, int size)
        {
            ArgumentNullException.ThrowIfNull(block);
            ArgumentOutOfRangeException.ThrowIfNegative(size);

            //Check handle bounds 
            MemoryUtil.CheckBounds(block, offset, (uint)size);

            Size = size;
            Handle = block;
            _offset = offset;
        }      

        /// <summary>
        /// Gets a <see cref="Span{T}"/> that is offset from the base of the handle
        /// and the size of the current sequence
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public readonly Span<T> Span => Size > 0 ? Handle.GetOffsetSpan(_offset, Size) : Span<T>.Empty;

        /// <summary>
        /// Gets a reference to the first element in the current sequence
        /// </summary>
        /// <returns>The element reference</returns>
        public readonly ref T GetReference() => ref Handle.GetOffsetRef(_offset);

        /// <summary>
        /// Slices the current sequence into a smaller <see cref="SubSequence{T}"/>
        /// </summary>
        /// <param name="offset">The relative offset from the current window offset</param>
        /// <param name="size">The size of the block</param>
        /// <returns>A <see cref="SubSequence{T}"/> of the current sequence</returns>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public readonly SubSequence<T> Slice(nuint offset, int size)
        {
            //Calc offset
            nuint newOffset = checked(_offset + offset);
            
            //Cal max size after the slice
            int newMaxSize = Size - (int)offset;

            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(size, newMaxSize);
            
            return new SubSequence<T>(Handle, newOffset, size > newMaxSize ? newMaxSize : size);
        }

        /// <summary>
        /// Slices the current sequence into a smaller <see cref="SubSequence{T}"/>
        /// </summary>
        /// <param name="offset">The relative offset from the current window offset</param>
        /// <returns>A <see cref="SubSequence{T}"/> of the current sequence</returns>
        public readonly SubSequence<T> Slice(nuint offset)
        {
            //Calc offset
            nuint newOffset = _offset + offset;
            
            //Calc the new max size of the block (let constructor handle the exception if less than 0)
            int newMaxSize = (int)((nuint)Size - offset);
            
            return new SubSequence<T>(Handle, newOffset, newMaxSize);
        }
    }
}