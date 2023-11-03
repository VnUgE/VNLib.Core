/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: MemoryUtilAlloc.cs 
*
* MemoryUtilAlloc.cs is part of VNLib.Utils which is part of 
* the larger VNLib collection of libraries and utilities.
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
using System.Runtime.CompilerServices;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    public static unsafe partial class MemoryUtil
    {
        #region alloc

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static UnsafeMemoryHandle<T> UnsafeAlloc<T>(int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            if (elements == 0)
            {
                return default;
            }

            /*
             * We may allocate from the share heap only if the heap is not using locks
             * or if the element size could cause performance issues because its too large
             * to use a managed array.
             * 
             * We want to avoid allocations, that may end up in the LOH if we can 
             */

            if ((Shared.CreationFlags & HeapCreation.UseSynchronization) == 0 || ByteCount<T>((uint)elements) > MAX_UNSAFE_POOL_SIZE)
            {
                // Alloc from heap
                IntPtr block = Shared.Alloc((uint)elements, (uint)sizeof(T), zero);
                //Init new handle
                return new(Shared, block, elements);
            }
            else
            {
                //Rent the array from the pool
                return ArrayPool<T>.Shared.UnsafeAlloc(elements, zero);
            }
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static UnsafeMemoryHandle<T> UnsafeAllocNearestPage<T>(int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            //Round to nearest page (in bytes)
            nint np = NearestPage<T>(elements);
            return UnsafeAlloc<T>((int)np, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static IMemoryHandle<T> SafeAlloc<T>(int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            /*
            * We may allocate from the share heap only if the heap is not using locks
            * or if the element size could cause performance issues because its too large
            * to use a managed array.
            * 
            * We want to avoid allocations, that may end up in the LOH if we can 
            */

            if ((Shared.CreationFlags & HeapCreation.UseSynchronization) == 0 || ByteCount<T>((uint)elements) > MAX_UNSAFE_POOL_SIZE)
            {
                return Shared.Alloc<T>(elements, zero);
            }
            else
            {
                return new ArrayPoolBuffer<T>(ArrayPool<T>.Shared, elements, zero);
            }
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static IMemoryHandle<T> SafeAllocNearestPage<T>(int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            //Round to nearest page (in bytes)
            nint np = NearestPage<T>(elements);
            return SafeAlloc<T>((int)np, zero);
        }

        /// <summary>
        /// Allocates a structure of the specified type on the specified 
        /// unmanged heap and optionally zero's it's memory
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="heap">The heap to allocate structure memory from</param>
        /// <param name="zero">A value that indicates if the structure memory should be zeroed before returning</param>
        /// <returns>A pointer to the structure ready for use.</returns>
        /// <remarks>Allocations must be freed with <see cref="StructFree{T}(IUnmangedHeap, T*)"/></remarks>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* StructAlloc<T>(IUnmangedHeap heap, bool zero) where T : unmanaged
        {
            _ = heap ?? throw new ArgumentNullException(nameof(heap));
            return (T*)heap.Alloc(1, (nuint)sizeof(T), zero);
        }

        /// <summary>
        /// Allocates a structure of the specified type on the specified 
        /// unmanged heap and optionally zero's it's memory, then returns
        /// and reference to the heap allocated structure.
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="heap">The heap to allocate structure memory from</param>
        /// <param name="zero">A value that indicates if the structure memory should be zeroed before returning</param>
        /// <returns>A reference to the heap allocated structure</returns>
        /// <remarks>Allocations must be freed with <see cref="StructFreeRef{T}(IUnmangedHeap, ref T)"/></remarks>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T StructAllocRef<T>(IUnmangedHeap heap, bool zero) where T : unmanaged
        {
            //Alloc structure
            T* ptr = StructAlloc<T>(heap, zero);
            //Get a reference and assign it
            return ref Unsafe.AsRef<T>(ptr);
        }

        /// <summary>
        /// Frees a structure allocated with <see cref="StructAlloc{T}(IUnmangedHeap, bool)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="heap">Heap the structure was allocated from to free it back to</param>
        /// <param name="structPtr">A pointer to the unmanaged structure to free</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StructFree<T>(IUnmangedHeap heap, T* structPtr) where T : unmanaged => StructFree(heap, (void*)structPtr);

        /// <summary>
        /// Frees a structure allocated with <see cref="StructAllocRef{T}(IUnmangedHeap, bool)"/>
        /// by its reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="heap">Heap the structure was allocated from to free it back to</param>
        /// <param name="structRef">A reference to the unmanaged structure to free</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StructFreeRef<T>(IUnmangedHeap heap, ref T structRef) where T : unmanaged => StructFree(heap, Unsafe.AsPointer(ref structRef));

        /// <summary>
        /// Frees a structure allocated with <see cref="StructAlloc{T}(IUnmangedHeap, bool)"/>
        /// </summary>
        /// <param name="heap">Heap the structure was allocated from to free it back to</param>
        /// <param name="structPtr">A pointer to the unmanaged structure to free</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void StructFree(IUnmangedHeap heap, void* structPtr)
        {
            _ = heap ?? throw new ArgumentNullException(nameof(heap));
            if(structPtr == null)
            { 
                throw new ArgumentNullException(nameof(structPtr)); 
            }
            //Get intpointer
            IntPtr ptr = (IntPtr)structPtr;
            //Free
            bool isFree = heap.Free(ref ptr);
            Debug.Assert(isFree, $"Structure free failed for heap {heap.GetHashCode()}, struct address {ptr:x}");
        }

        #endregion

        #region ByteOptimimzations

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static UnsafeMemoryHandle<byte> UnsafeAlloc(int elements, bool zero = false)
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            if(elements == 0)
            {
                return default;
            }

            /*
             * We may allocate from the share heap only if the heap is not using locks
             * or if the element size could cause performance issues because its too large
             * to use a managed array.
             * 
             * We want to avoid allocations, that may end up in the LOH if we can 
             */

            if ((Shared.CreationFlags & HeapCreation.UseSynchronization) == 0 || elements > MAX_UNSAFE_POOL_SIZE)
            {
                // Alloc from heap
                IntPtr block = Shared.Alloc((uint)elements, 1, zero);
                //Init new handle
                return new(Shared, block, elements);
            }
            else
            {
                return ArrayPool<byte>.Shared.UnsafeAlloc(elements, zero);
            }
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeMemoryHandle<byte> UnsafeAllocNearestPage(int elements, bool zero = false)
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            //Round to nearest page (in bytes)
            nint np = NearestPage(elements);
            return UnsafeAlloc((int)np, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static IMemoryHandle<byte> SafeAlloc(int elements, bool zero = false)
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            /*
            * We may allocate from the share heap only if the heap is not using locks
            * or if the element size could cause performance issues because its too large
            * to use a managed array.
            * 
            * We want to avoid allocations, that may end up in the LOH if we can 
            */

            if ((Shared.CreationFlags & HeapCreation.UseSynchronization) == 0 || elements > MAX_UNSAFE_POOL_SIZE)
            {
                return Shared.Alloc<byte>(elements, zero);
            }
            else
            {
                return new ArrayPoolBuffer<byte>(ArrayPool<byte>.Shared, elements, zero);
            }
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static IMemoryHandle<byte> SafeAllocNearestPage(int elements, bool zero = false)
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            //Round to nearest page (in bytes)
            nint np = NearestPage(elements);
            return SafeAlloc((int)np, zero);
        }

        #endregion
    }

}
