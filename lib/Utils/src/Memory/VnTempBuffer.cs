/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnTempBuffer.cs 
*
* VnTempBuffer.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// A disposable temporary buffer from shared ArrayPool
    /// </summary>
    /// <typeparam name="T">Type of buffer to create</typeparam>
    public sealed class VnTempBuffer<T> : VnDisposeable, IIndexable<int, T>, IMemoryHandle<T>, IMemoryOwner<T>
    {
        private readonly ArrayPool<T> Pool;

        /// <summary>
        /// Referrence to internal buffer
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
        Memory<T> IMemoryOwner<T>.Memory => AsMemory();

        /// <summary>
        /// Allocates a new <see cref="VnTempBuffer{BufType}"/> with a new buffer from shared array-pool
        /// </summary>
        /// <param name="minSize">Minimum size of the buffer</param>
        /// <param name="zero">Set the zero memory flag on close</param>
        public VnTempBuffer(int minSize, bool zero = false) :this(ArrayPool<T>.Shared, minSize, zero)
        {}
        
        /// <summary>
        /// Allocates a new <see cref="VnTempBuffer{BufType}"/> with a new buffer from specified array-pool
        /// </summary>
        /// <param name="pool">The <see cref="ArrayPool{T}"/> to allocate from and return to</param>
        /// <param name="minSize">Minimum size of the buffer</param>
        /// <param name="zero">Set the zero memory flag on close</param>
        public VnTempBuffer(ArrayPool<T> pool, int minSize, bool zero = false)
        {
            Pool = pool;
            Buffer = pool.Rent(minSize, zero);
            InitSize = minSize;
        }
        
        /// <summary>
        /// Gets an offset wrapper around the current buffer
        /// </summary>
        /// <param name="offset">Offset from begining of current buffer</param>
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
        public Memory<T> AsMemory()
        {
            Check();
            return new Memory<T>(Buffer, 0, InitSize);
        }
        
        /// <summary>
        /// Gets a memory structure around the internal buffer
        /// </summary>
        /// <param name="count">The number of elements included in the result</param>
        /// <param name="start">A value specifying the begining index of the buffer to include</param>
        /// <returns>A memory structure over the buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Memory<T> AsMemory(int start, int count)
        {
            Check();
            return new Memory<T>(Buffer, start, count);
        }

        /// <summary>
        /// Gets a memory structure around the internal buffer
        /// </summary>
        /// <param name="count">The number of elements included in the result</param>
        /// <returns>A memory structure over the buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Memory<T> AsMemory(int count)
        {
            Check();
            return new Memory<T>(Buffer, 0, count);
        }

        /*
         * Allow implict casts to span/arrayseg/memory 
         */
        public static implicit operator Memory<T>(VnTempBuffer<T> buf) => buf == null ? Memory<T>.Empty : buf.ToMemory();
        public static implicit operator Span<T>(VnTempBuffer<T> buf) => buf == null ? Span<T>.Empty : buf.ToSpan();
        public static implicit operator ArraySegment<T>(VnTempBuffer<T> buf) => buf == null ? ArraySegment<T>.Empty : buf.ToArraySegment();
        
        public Memory<T> ToMemory() => Disposed ? Memory<T>.Empty : Buffer.AsMemory(0, InitSize);
        public Span<T> ToSpan() => Disposed ? Span<T>.Empty : Buffer.AsSpan(0, InitSize);
        public ArraySegment<T> ToArraySegment() => Disposed ? ArraySegment<T>.Empty : new(Buffer, 0, InitSize);

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

        //Pin, will also check bounds
        ///<inheritdoc/>
        public MemoryHandle Pin(int elementIndex) => MemoryUtil.PinArrayAndGetHandle(Buffer, elementIndex);

        void IPinnable.Unpin()
        {
            //Gchandle will manage the unpin
        }

        ///<inheritdoc/>
        ~VnTempBuffer() => Free();     
    }
}