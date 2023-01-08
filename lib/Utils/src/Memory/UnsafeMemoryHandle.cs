/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: UnsafeMemoryHandle.cs 
*
* UnsafeMemoryHandle.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Represents an unsafe handle to managed/unmanaged memory that should be used cautiously.
    /// A referrence counter is not maintained.
    /// </summary>
    /// <typeparam name="T">Unmanaged memory type</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct UnsafeMemoryHandle<T> : IMemoryHandle<T>, IEquatable<UnsafeMemoryHandle<T>> where T : unmanaged
    {
        private enum HandleType
        {
            None,
            Pool,
            PrivateHeap
        }
       
        private readonly T[]? _poolArr;
        private readonly IntPtr _memoryPtr;
        private readonly ArrayPool<T>? _pool;
        private readonly IUnmangedHeap? _heap;
        private readonly HandleType _handleType;
        private readonly int _length;

        ///<inheritdoc/>
        public readonly unsafe Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _handleType == HandleType.Pool ? _poolArr.AsSpan(0, IntLength) : new (_memoryPtr.ToPointer(), IntLength);
        }
        ///<inheritdoc/>
        public readonly int IntLength => _length;
        ///<inheritdoc/>
        public readonly ulong Length => (ulong)_length;

        /// <summary>
        /// Creates an empty <see cref="UnsafeMemoryHandle{T}"/>
        /// </summary>
        public UnsafeMemoryHandle()
        {
            _pool = null;
            _heap = null;
            _poolArr = null;
            _memoryPtr = IntPtr.Zero;
            _handleType = HandleType.None;
            _length = 0;
        }

        /// <summary>
        /// Inializes a new <see cref="UnsafeMemoryHandle{T}"/> using the specified
        /// <see cref="ArrayPool{T}"/>
        /// </summary>
        /// <param name="elements">The number of elements to store</param>
        /// <param name="zero">Zero initial contents?</param>
        /// <param name="pool">The explicit pool to alloc buffers from</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public unsafe UnsafeMemoryHandle(ArrayPool<T> pool, int elements, bool zero)
        {
            if (elements < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elements));
            }
            //Pool is required
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            //Rent the array from the pool and hold referrence to it
            _poolArr = pool.Rent(elements, zero);
            //Cant store ref to array becase GC can move it
            _memoryPtr = IntPtr.Zero;
            //Set pool handle type
            _handleType = HandleType.Pool;
            //No heap being loaded
            _heap = null;
            _length = elements;
        }

        /// <summary>
        /// Intializes a new <see cref="UnsafeMemoryHandle{T}"/> for block of memory allocated from
        /// an <see cref="IUnmangedHeap"/>
        /// </summary>
        /// <param name="heap">The heap the initial memory block belongs to</param>
        /// <param name="initial">A pointer to the unmanaged memory block</param>
        /// <param name="elements">The number of elements this block points to</param>
        internal UnsafeMemoryHandle(IUnmangedHeap heap, IntPtr initial, int elements)
        {
            _pool = null;
            _poolArr = null;
            _heap = heap;
            _length = elements;
            _memoryPtr = initial;
            _handleType = HandleType.PrivateHeap;
        }

        /// <summary>
        /// Releases memory back to the pool or heap from which is was allocated.
        /// </summary>
        /// <remarks>After this method is called, this handle points to invalid memory</remarks>
        public readonly void Dispose()
        {
            switch (_handleType)
            {
                case HandleType.Pool:
                    {
                        //Return array to pool
                        _pool!.Return(_poolArr!);
                    }
                    break;
                case HandleType.PrivateHeap:
                    {
                        IntPtr unalloc = _memoryPtr;
                        //Free the unmanaged handle
                        _heap!.Free(ref unalloc);
                    }
                    break;
            }
        }

        ///<inheritdoc/>
        public readonly override int GetHashCode() => _handleType == HandleType.Pool ? _poolArr!.GetHashCode() : _memoryPtr.GetHashCode();
        ///<inheritdoc/>
        public readonly unsafe MemoryHandle Pin(int elementIndex)
        {
            //Guard
            if (elementIndex < 0 || elementIndex >= IntLength)
            {
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            }
            
            if (_handleType == HandleType.Pool)
            {
                //Pin the array
                GCHandle arrHandle = GCHandle.Alloc(_poolArr, GCHandleType.Pinned);
                //Get array base address
                void* basePtr = (void*)arrHandle.AddrOfPinnedObject();
                //Get element offset
                void* indexOffet = Unsafe.Add<T>(basePtr, elementIndex);
                return new (indexOffet, arrHandle);
            }
            else
            {
                //Get offset pointer and pass self as pinnable argument, (nothing happens but support it)
                void* basePtr = Unsafe.Add<T>(_memoryPtr.ToPointer(), elementIndex);
                //Unmanaged memory is always pinned, so no need to pass this as IPinnable, since it will cause a box
                return new (basePtr);
            }
        }
        ///<inheritdoc/>
        public readonly void Unpin()
        {
            //Nothing to do since gc handle takes care of array, and unmanaged pointers are not pinned
        }
        
        /// <summary>
        /// Determines if the other handle represents the same memory block as the 
        /// current handle. 
        /// </summary>
        /// <param name="other">The other handle to test</param>
        /// <returns>True if the other handle points to the same block of memory as the current handle</returns>
        public readonly bool Equals(UnsafeMemoryHandle<T> other)
        {
            return _handleType == other._handleType && Length == other.Length && GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Override for object equality operator, will cause boxing
        /// for structures
        /// </summary>
        /// <param name="obj">The other object to compare</param>
        /// <returns>
        /// True if the passed object is of type <see cref="UnsafeMemoryHandle{T}"/> 
        /// and uses the structure equality operator
        /// false otherwise.
        /// </returns>
        public readonly override bool Equals([NotNullWhen(true)] object? obj) => obj is UnsafeMemoryHandle<T> other && Equals(other);

        /// <summary>
        /// Casts the handle to it's <see cref="Span{T}"/> representation
        /// </summary>
        /// <param name="handle">the handle to cast</param>
        public static implicit operator Span<T>(in UnsafeMemoryHandle<T> handle) => handle.Span;

        /// <summary>
        /// Equality overload
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns>True if handles are equal, flase otherwise</returns>
        public static bool operator ==(in UnsafeMemoryHandle<T> left, in UnsafeMemoryHandle<T> right) => left.Equals(right);
        /// <summary>
        /// Equality overload
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns>True if handles are equal, flase otherwise</returns>
        public static bool operator !=(in UnsafeMemoryHandle<T> left, in UnsafeMemoryHandle<T> right) => !left.Equals(right);

    }
}