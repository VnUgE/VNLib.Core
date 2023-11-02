/*
* Copyright (c) 2023 Vaughn Nugent
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

        /// <summary>
        /// Gets a span over the entire allocated block
        /// </summary>
        /// <returns>A <see cref="Span{T}"/> over the internal data</returns>
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
            get => Heap != null && Heap.CreationFlags.HasFlag(HeapCreation.SupportsRealloc);
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
            //Set element size (always allocate at least 1 object)
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

        /// <summary>
        /// Resizes the current handle on the heap
        /// </summary>
        /// <param name="elements">Positive number of elemnts the current handle should referrence</param>
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
            if (elements >= _length)
            {
                throw new ArgumentOutOfRangeException(nameof(elements), "Element offset cannot be larger than allocated size");
            }

            this.ThrowIfClosed();

            //Get ptr and offset it
            T* bs = ((T*)handle) + elements;
            return bs;
        }

        ///<inheritdoc/>
        public ref T GetReference()
        {
            this.ThrowIfClosed();
            return ref MemoryUtil.GetRef<T>(handle);
        }

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        ///<exception cref="ArgumentOutOfRangeException"></exception>
        ///<remarks>
        ///Calling this method increments the handle's referrence count. 
        ///Disposing the returned handle decrements the handle count.
        ///</remarks>
        public unsafe MemoryHandle Pin(int elementIndex)
        {
            if (elementIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            }

            //Get ptr and guard checks before adding the referrence
            T* ptr = GetOffset((nuint)elementIndex);

            bool addRef = false;
            //use the pinned field as success val
            DangerousAddRef(ref addRef);
            //Create a new system.buffers memory handle from the offset ptr address
            return !addRef
                ? throw new ObjectDisposedException("Failed to increase referrence count on the memory handle because it was released")
                : new MemoryHandle(ptr, pinnable: this);
        }

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public void Unpin()
        {
            //Dec count on release
            DangerousRelease();
        }

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
            return other != null && (IsClosed | other.IsClosed) == false && _length == other._length && handle == other.handle;
        }

        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is MemoryHandle<T> oHandle && Equals(oHandle);

        ///<inheritdoc/>
        public override int GetHashCode() => base.GetHashCode();

        ///<inheritdoc/>
        public static implicit operator Span<T>(MemoryHandle<T> handle)
        {
            //If the handle is invalid or closed return an empty span 
            return handle.IsClosed || handle.IsInvalid || handle._length == 0 ? Span<T>.Empty : handle.Span;
        }
    }
}