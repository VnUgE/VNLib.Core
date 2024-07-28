/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: MemoryHandle.cs 
*
* MemoryHandle.cs is part of VNLib.Utils which is part of the larger 
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

using Microsoft.Win32.SafeHandles;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides a wrapper for using umanged memory handles from an assigned <see cref="Win32PrivateHeap"/> for <see cref="UnmanagedType"/> types
    /// </summary>
    /// <remarks>
    /// Handles are configured to address blocks larger than 2GB,
    /// so some properties may raise exceptions if large blocks are used.
    /// </remarks>
    public sealed class MemoryHandle<T> : 
        SafeHandleZeroOrMinusOneIsInvalid, 
        IResizeableMemoryHandle<T>, 
        IMemoryHandle<T>,
        IEquatable<MemoryHandle<T>> 
        where T : unmanaged
    {
        private readonly bool ZeroMemory;
        private readonly IUnmangedHeap Heap;
        private nuint _length;

        /// <summary>
        /// New <typeparamref name="T"/>* pointing to the base of the allocated block
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public unsafe T* Base
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetOffset(0);
        }

        /// <summary>
        /// New <see cref="IntPtr"/> pointing to the base of the allocated block
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public unsafe IntPtr BasePtr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (IntPtr)GetOffset(0);
        }

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="OverflowException"></exception>
        public unsafe Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int len = Convert.ToInt32(_length);
                return _length == 0 ? Span<T>.Empty : new Span<T>(Base, len);
            }
        }

        ///<inheritdoc/>
        public nuint Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>
        /// Number of bytes allocated to the current instance
        /// </summary>
        /// <exception cref="OverflowException"></exception>
        public nuint ByteLength
        {
            //Check for overflows when converting to bytes (should run out of memory before this is an issue, but just incase)
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MemoryUtil.ByteCount<T>(_length);
        }

        ///<inheritdoc/>
        public bool CanRealloc
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !IsClosed && Heap != null && Heap.CreationFlags.HasFlag(HeapCreation.SupportsRealloc);
        }

        /// <summary>
        /// Creates a new memory handle, for which is holds ownership, and allocates the number of elements specified on the heap.
        /// </summary>
        /// <param name="heap">The heap to allocate/deallocate memory from</param>
        /// <param name="elements">Number of elements to allocate</param>
        /// <param name="zero">Zero all memory during allocations from heap</param>
        /// <param name="initial">The initial block of allocated memory to wrap</param>
        internal MemoryHandle(IUnmangedHeap heap, IntPtr initial, nuint elements, bool zero) : base(true)
        {
            //Set element size
            _length = elements;
            ZeroMemory = zero;
            //assign heap ref
            Heap = heap;
            SetHandle(initial);
        }

        /*
         * Empty handle will disable release, and because the 
         * handle pointer is 0, its considered invalid and will now 
         * allow 
         */
        /// <summary>
        /// Initialzies an empty memory handle. Properties will raise exceptions
        /// when accessed, however <see cref="IMemoryHandle{T}"/> operations are 
        /// considered "safe" meaning they should never raise excpetions
        /// </summary>
        public MemoryHandle() : base(false)
        {
            _length = 0;
            Heap = null!;
        }

        /// <inheritdoc/>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public unsafe void Resize(nuint elements)
        {
            this.ThrowIfClosed();
            //Re-alloc (Zero if required)
            try
            {
                /*
                 * If resize raises an exception the current block pointer
                 * should still be valid, if its not, the pointer should 
                 * be set to 0/-1, which will be considered invalid anyway
                 */

                Heap.Resize(ref handle, elements, (nuint)sizeof(T), ZeroMemory);

                //Update size only if succeeded
                _length = elements;
            }
            //Catch the disposed exception so we can invalidate the current ptr
            catch (ObjectDisposedException)
            {
                SetHandle(IntPtr.Zero);
                //Set as invalid so release does not get called
                SetHandleAsInvalid();
                //Propagate the exception
                throw;
            }
        }

        /// <summary>
        /// Gets an offset pointer from the base postion to the number of bytes specified. Performs bounds checks
        /// </summary>
        /// <param name="elements">Number of elements of type to offset</param>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns><typeparamref name="T"/> pointer to the memory offset specified</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T* GetOffset(nuint elements)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elements, _length);
            this.ThrowIfClosed();

            //Get ptr and offset it
            return ((T*)handle) + elements;
        }

        ///<inheritdoc/>
        public ref T GetReference()
        {
            this.ThrowIfClosed();
            return ref MemoryUtil.GetRef<T>(handle);
        }

        /// <summary>
        /// Gets a reference to the element at the specified offset from the base 
        /// address of the <see cref="MemoryHandle{T}"/>
        /// </summary>
        /// <param name="offset">The element offset from the base address to add to the returned reference</param>
        /// <returns>The reference to the item at the desired offset</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref T GetOffsetRef(nuint offset) => ref Unsafe.AsRef<T>(GetOffset(offset));

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        ///<exception cref="ArgumentOutOfRangeException"></exception>
        ///<remarks>
        ///Calling this method increments the handle's referrence count. 
        ///Disposing the returned handle decrements the handle count.
        ///</remarks>
        public unsafe MemoryHandle Pin(int elementIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);

            //Get ptr and guard checks before adding the referrence
            T* ptr = GetOffset((nuint)elementIndex);

            bool addRef = false;

            //use the pinned field as success val
            DangerousAddRef(ref addRef);

            //If adding ref failed, the handle is closed
            ObjectDisposedException.ThrowIf(!addRef, this);
         
            return new MemoryHandle(ptr, pinnable: this);
        }

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public void Unpin() => DangerousRelease();

        ///<inheritdoc/>
        protected override bool ReleaseHandle() => Heap.Free(ref handle);

        /// <summary>
        /// Determines if the memory blocks are equal by comparing their base addresses.
        /// </summary>
        /// <param name="other"><see cref="MemoryHandle{T}"/> to compare</param>
        /// <returns>true if the block of memory is the same, false if the handle's size does not 
        /// match or the base addresses do not match even if they point to an overlapping address space</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public bool Equals(MemoryHandle<T>? other)
        {
            return other != null 
                && (IsClosed | other.IsClosed) == false 
                && _length == other._length 
                && handle == other.handle;
        }

        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is MemoryHandle<T> oHandle && Equals(oHandle);

        ///<inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), handle.GetHashCode(), _length);
    }
}