/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ArrayPoolBuffer.cs 
*
* ArrayPoolBuffer.cs is part of VNLib.Utils which is part of the larger 
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
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// A disposable temporary buffer from shared ArrayPool
    /// </summary>
    /// <typeparam name="T">Type of buffer to create</typeparam>
    public sealed class ArrayPoolBuffer<T> : 
        VnDisposeable, 
        IIndexable<int, T>,
        IMemoryHandle<T>,
        IMemoryOwner<T>
    {
        private readonly ArrayPool<T> Pool;

        /// <summary>
        /// Reference to internal buffer
        /// </summary>
        public T[] Buffer { get; private set; }

        /// <summary>
        /// Inital/desired size of internal buffer
        /// </summary>
        public int InitSize { get; }
      
        /// <summary>
        /// Actual length of internal buffer
        /// </summary>
        public nuint Length => (nuint)Buffer.LongLength;

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public Span<T> Span
        {
            get
            {
                Check();
                return new Span<T>(Buffer, 0, InitSize);
            }
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetReference() => ref MemoryMarshal.GetArrayDataReference(Buffer);

        ///<inheritdoc/>
        Memory<T> IMemoryOwner<T>.Memory => AsMemory();

        /// <summary>
        /// Allocates a new <see cref="ArrayPoolBuffer{BufType}"/> with a new buffer from shared array-pool
        /// </summary>
        /// <param name="minSize">Minimum size of the buffer</param>
        /// <param name="zero">Set the zero memory flag on close</param>
        public ArrayPoolBuffer(int minSize, bool zero = false) :this(ArrayPool<T>.Shared, minSize, zero)
        { }
        
        /// <summary>
        /// Allocates a new <see cref="ArrayPoolBuffer{BufType}"/> with a new buffer from specified array-pool
        /// </summary>
        /// <param name="pool">The <see cref="ArrayPool{T}"/> to allocate from and return to</param>
        /// <param name="minSize">Minimum size of the buffer</param>
        /// <param name="zero">Set the zero memory flag on close</param>
        public ArrayPoolBuffer(ArrayPool<T> pool, int minSize, bool zero = false)
            :this(pool, pool.Rent(minSize, zero), minSize)
        { }

        /// <summary>
        /// Initializes a new <see cref="ArrayPoolBuffer{T}"/> from the specified rented array
        /// that belongs to the supplied pool
        /// </summary>
        /// <param name="pool">The pool the array was rented from</param>
        /// <param name="array">The rented array</param>
        /// <param name="size">The size of the buffer around the array. May be smaller or exact size of the array</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ArrayPoolBuffer(ArrayPool<T> pool, T[] array, int size)
        {
            ArgumentNullException.ThrowIfNull(pool);
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(size, array.Length);

            Pool = pool;
            Buffer = array;
            InitSize = size;
        }


        /// <summary>
        /// Gets an offset wrapper around the current buffer
        /// </summary>
        /// <param name="offset">Offset from beginning of current buffer</param>
        /// <param name="count">Number of <typeparamref name="T"/> from offset</param>
        /// <returns>An <see cref="ArraySegment{BufType}"/> wrapper around the current buffer containing the offset</returns>
        public ArraySegment<T> GetOffsetWrapper(int offset, int count)
        {
            Check();
            //Let arraysegment throw exceptions for checks 
            return new ArraySegment<T>(Buffer, offset, count);
        }
        
        ///<inheritdoc/>
        public T this[int index]
        {
            get
            {
                Check();
                return Buffer[index];
            }
            set
            {
                Check();
                Buffer[index] = value;
            }
        }

        /// <summary>
        /// Gets a memory structure around the internal buffer
        /// </summary>
        /// <returns>A memory structure over the buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Memory<T> AsMemory() => AsMemory(start: 0, InitSize);

        /// <summary>
        /// Gets a memory structure around the internal buffer
        /// </summary>
        /// <param name="start">The number of elements included in the result</param>
        /// <returns>A memory structure over the buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Memory<T> AsMemory(int start) => AsMemory(start, InitSize - start);

        /// <summary>
        /// Gets a memory structure around the internal buffer
        /// </summary>
        /// <param name="count">The number of elements included in the result</param>
        /// <param name="start">A value specifying the beginning index of the buffer to include</param>
        /// <returns>A memory structure over the buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Memory<T> AsMemory(int start, int count)
        {
            Check();
            //Memory constructor will check for array bounds
            return new(Buffer, start, count);
        }

        /// <summary>
        /// Gets an array segment around the internal buffer
        /// </summary>
        /// <returns>The internal array segment</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public ArraySegment<T> AsArraySegment() => GetOffsetWrapper(0, InitSize);
        
        
        /// <summary>
        /// Gets an array segment around the internal buffer
        /// </summary>
        /// <returns>The internal array segment</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ArraySegment<T> AsArraySegment(int start, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            MemoryUtil.CheckBounds(Buffer, (uint)start, (uint)count);

            Check();
            return new ArraySegment<T>(Buffer, start, count);
        }

        //Pin, will also check bounds
        ///<inheritdoc/>
        public MemoryHandle Pin(int elementIndex)
        {
            Check();
            return MemoryUtil.PinArrayAndGetHandle(Buffer, elementIndex);
        }

        void IPinnable.Unpin()
        {
            //Gchandle will manage the unpin
        }

        /// <summary>
        /// Returns buffer to shared array-pool
        /// </summary>
        protected override void Free()
        {
            //Return the buffer to the array pool
            Pool.Return(Buffer);
            
            //Set buffer to null,
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Buffer = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        ///<inheritdoc/>
        ~ArrayPoolBuffer() => Free();     
    }
}