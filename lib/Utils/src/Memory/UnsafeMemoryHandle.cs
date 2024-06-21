/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

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
        private enum HandleType : byte
        {
            None,
            Pool,
            PrivateHeap
        }

        private readonly int _length;

        private readonly IntPtr _memoryPtr;        
        private readonly HandleType _handleType;

        private readonly T[]? _poolArr;
        private readonly ArrayPool<T>? _pool;
        private readonly IUnmangedHeap? _heap;

        ///<inheritdoc/>
        public readonly Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AsSpan();  
        }

        /// <summary>
        /// Gets the integer number of elements of the block of memory pointed to by this handle
        /// </summary>
        public readonly int IntLength => _length;

        ///<inheritdoc/>
        public readonly nuint Length => (nuint)_length;

        /// <summary>
        /// Inializes a new <see cref="UnsafeMemoryHandle{T}"/> using the specified
        /// <see cref="ArrayPool{T}"/>
        /// </summary>
        /// <param name="elements">The number of elements to store</param>
        /// <param name="array">The array reference to store</param>
        /// <param name="pool">The explicit pool to alloc buffers from</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        internal UnsafeMemoryHandle(ArrayPool<T> pool, T[] array, int elements)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            //Pool and array is required
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _poolArr = array ?? throw new ArgumentNullException(nameof(array));
            //Set pool handle type
            _handleType = HandleType.Pool;
            //No heap being loaded
            _heap = null;
            _length = elements;
            _memoryPtr = IntPtr.Zero;
        }

        /// <summary>
        /// Intializes a new <see cref="UnsafeMemoryHandle{T}"/> for block of memory allocated from
        /// an <see cref="IUnmangedHeap"/>
        /// </summary>
        /// <param name="heap">The heap the initial memory block belongs to</param>
        /// <param name="initial">A pointer to the unmanaged memory block</param>
        /// <param name="elements">The number of elements this block points to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal UnsafeMemoryHandle(IUnmangedHeap heap, IntPtr initial, int elements)
        {
            //Never allow non-empty handles
            Debug.Assert(heap != null);
            Debug.Assert(initial != IntPtr.Zero);
            Debug.Assert(elements > 0);

            _pool = null;
            _poolArr = null;
            _heap = heap;
            _length = elements;
            _memoryPtr = initial;
            _handleType = HandleType.PrivateHeap;
        }

        /// <summary>
        /// Releases memory back to the pool or heap from which is was allocated.
        /// <para>
        /// After this method is called, this handle points to invalid memory
        /// </para>
        /// <para>
        /// Warning: Double Free -> Do not call more than once. Using statment is encouraged 
        /// </para>
        /// </summary>
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
                        bool unsafeFreed = _heap!.Free(ref unalloc);
                        Debug.Assert(unsafeFreed, "A previously allocated unsafe memhandle failed to free block");
                    }
                    break;
            }
        }      
        
        ///<inheritdoc/>
        public readonly MemoryHandle Pin(int elementIndex)
        {
            //Guard size
            ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elementIndex, _length);

            switch (_handleType)
            {
                case HandleType.Pool:
                    return MemoryUtil.PinArrayAndGetHandle(_poolArr!, elementIndex);
                case HandleType.PrivateHeap:
                    //Add an offset to the base address of the memory block
                    int byteOffset = MemoryUtil.ByteCount<T>(elementIndex);
                    IntPtr offset = IntPtr.Add(_memoryPtr, byteOffset);
                    //Unmanaged memory is always pinned, so no need to pass this as IPinnable, since it will cause a box
                    return MemoryUtil.GetMemoryHandleFromPointer(offset);
                default:
                    throw new InvalidOperationException("The handle is empty, and cannot be pinned");
            }
        }
        
        ///<inheritdoc/>
        public readonly void Unpin()
        {
            //Nothing to do since gc handle takes care of array, and unmanaged pointers are not pinned
        }
        
        ///<inheritdoc/>
        ///<exception cref="InvalidOperationException"></exception>
        public readonly ref T GetReference()
        {
            switch (_handleType)
            {
                case HandleType.None:
                    return ref Unsafe.NullRef<T>();
                case HandleType.Pool:
                    return ref MemoryMarshal.GetArrayDataReference(_poolArr!);
                case HandleType.PrivateHeap:
                    return ref MemoryUtil.GetRef<T>(_memoryPtr);
                default:
                    throw new InvalidOperationException("The handle is empty, and cannot capture a reference");
            }
        }

        /// <summary>
        /// Returns a <see cref="Span{T}"/> that represents the memory block pointed to by this handle
        /// </summary>
        /// <returns>The memory block that is held by the internl handle</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Span<T> AsSpan() => AsSpan(0, _length);

        /// <summary>
        /// Returns a <see cref="Span{T}"/> that represents the memory block pointed to by this handle
        /// </summary>
        /// <param name="start">A starting element offset to return the span at</param>
        /// <returns>The desired memory block at the desired element offset</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Span<T> AsSpan(int start) => AsSpan(start, _length - start);

        /// <summary>
        /// Returns a <see cref="Span{T}"/> that represents the memory block pointed to by this handle
        /// </summary>
        /// <param name="start">The starting element offset</param>
        /// <param name="length">The number of elements included in the returned span</param>
        /// <returns>The desired memory block at the desired element offset and length</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>"
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Span<T> AsSpan(int start, int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length - start, _length);

            /*
             * If the handle is empty, a null ref should be returned. The 
             * check above will gaurd against calling this function on non-empty
             * handles. So adding 0 to 0 on the reference should not cause any issues.
             */
            
            return MemoryMarshal.CreateSpan(
                ref Unsafe.Add(ref GetReference(), start), 
                length
            );
        }

        ///<inheritdoc/>
        public readonly override int GetHashCode()
        {
            //Get hashcode for the proper memory type
            return _handleType switch
            {
                HandleType.Pool => _poolArr!.GetHashCode(),
                HandleType.PrivateHeap => _memoryPtr.GetHashCode(),
                _ => base.GetHashCode(),
            };
        }

        /// <summary>
        /// Determines if the other handle represents the same memory block as the 
        /// current handle. 
        /// </summary>
        /// <param name="other">The other handle to test</param>
        /// <returns>True if the other handle points to the same block of memory as the current handle</returns>
        public readonly bool Equals(in UnsafeMemoryHandle<T> other)
        {
            return _handleType == other._handleType 
                && Length == other.Length 
                && GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Determines if the other handle represents the same memory block as the 
        /// current handle. 
        /// </summary>
        /// <param name="other">The other handle to test</param>
        /// <returns>True if the other handle points to the same block of memory as the current handle</returns>
        public readonly bool Equals(UnsafeMemoryHandle<T> other) => Equals(in other);

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
        public readonly override bool Equals([NotNullWhen(true)] object? obj) => obj is UnsafeMemoryHandle<T> other && Equals(in other);

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
