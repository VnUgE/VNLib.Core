/*
* Copyright (c) 2025 Vaughn Nugent
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

namespace VNLib.Utils.Memory
{
    public static unsafe partial class MemoryUtil
    {
        #region alloc

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanUseUnmanagedHeap<T>(IUnmanagedHeap heap, nuint elements)
        {
            /*
            * We may allocate from the share heap only if the heap is not using locks
            * or if the element size could cause performance issues because its too large
            * to use a managed array.
            * 
            * We want to avoid allocations, that may end up in the LOH if we can 
            */

            return (heap.CreationFlags & HeapCreation.UseSynchronization) == 0 
                || ByteCount<T>((uint)elements) > MAX_UNSAFE_POOL_SIZE;
        }

        /// <summary>
        /// Allocates a block of unmanaged memory of the number of elements to store of an Unmanaged type
        /// </summary>
        /// <typeparam name="T">Unmanaged data type to create a block of</typeparam>
        /// <param name="heap"></param>
        /// <param name="elements">The size of the block (number of elements)</param>
        /// <param name="zero">A flag that zeros the allocated block before returned</param>
        /// <returns>The unmanaged <see cref="UnsafeMemoryHandle{T}"/></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static UnsafeMemoryHandle<T> UnsafeAlloc<T>(IUnmanagedHeap heap, int elements, bool zero = false) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(heap);
            ArgumentOutOfRangeException.ThrowIfNegative(elements);

            if (elements == 0)
            {
                //Return an empty handle
                return default;
            }

            //If zero flag is set then specify zeroing memory (safe case because of the above check)
            IntPtr block = heap.Alloc((nuint)elements, (nuint)sizeof(T), zero);

            return ToUnsafeHandle<T>(block, elements, heap);
        }

        /// <summary>
        /// Rents a new array and stores it as a resource within an <see cref="UnsafeMemoryHandle{T}"/> to return the 
        /// array when work is completed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pool"></param>
        /// <param name="size">The minimum size array to allocate</param>
        /// <param name="zero">Should elements from 0 to size be set to default(T)</param>
        /// <returns>A new <see cref="UnsafeMemoryHandle{T}"/> encapsulating the rented array</returns>
        public static UnsafeMemoryHandle<T> UnsafeAlloc<T>(ArrayPool<T> pool, int size, bool zero = false) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(pool);           

            if (size == 0)
            {
                return default;
            }

            T[] array = pool.Rent(size);

            if (zero)
            {
                InitializeBlock(array, (uint)size);
            }

            return ToUnsafeHandle(array, size, pool);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static UnsafeMemoryHandle<T> UnsafeAlloc<T>(int elements, bool zero = false) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);

            if (elements == 0)
            {
                return default;
            }

            return CanUseUnmanagedHeap<T>(Shared, (uint)elements)
                ? UnsafeAlloc<T>(Shared, elements, zero)
                : UnsafeAlloc(ArrayPool<T>.Shared, elements, zero);
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
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeMemoryHandle<T> UnsafeAllocNearestPage<T>(int elements, bool zero = false) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return UnsafeAlloc<T>(elements: (int)NearestPage<T>(elements), zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged memory of the number of elements to store of an Unmanaged type
        /// </summary>
        /// <typeparam name="T">Unmanaged data type to create a block of</typeparam>
        /// <param name="heap"></param>
        /// <param name="elements">The size of the block (number of elements)</param>
        /// <param name="zero">A flag that zeros the allocated block before returned</param>
        /// <returns>The unmanaged <see cref="MemoryHandle{T}"/></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static MemoryHandle<T> SafeAlloc<T>(IUnmanagedHeap heap, nuint elements, bool zero = false) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(heap);

            //Return empty handle if no elements were specified
            if (elements == 0)
            {
                return new MemoryHandle<T>();
            }
            
            IntPtr block = heap.Alloc(elements, (nuint)sizeof(T), zero);

            return ToHandle<T>(block, elements, heap, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged memory of the number of elements to store of an Unmanaged type
        /// </summary>
        /// <typeparam name="T">Unmanaged data type to create a block of</typeparam>
        /// <param name="heap"></param>
        /// <param name="elements">The size of the block (number of elements)</param>
        /// <param name="zero">A flag that zeros the allocated block before returned</param>
        /// <returns>The unmanaged <see cref="MemoryHandle{T}"/></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle<T> SafeAlloc<T>(IUnmanagedHeap heap, nint elements, bool zero = false) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return SafeAlloc<T>(heap, (nuint)elements, zero);
        }

        /// <summary>
        /// Rents a new array and stores it as a resource within an <see cref="OpenResourceHandle{T}"/> to return the 
        /// array when work is completed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pool"></param>
        /// <param name="size">The minimum size array to allocate</param>
        /// <param name="zero">Should elements from 0 to size be set to default(T)</param>
        /// <returns>A new <see cref="OpenResourceHandle{T}"/> encapsulating the rented array</returns>
        public static ArrayPoolBuffer<T> SafeAlloc<T>(ArrayPool<T> pool, int size, bool zero = false) where T : struct
        {
            ArgumentNullException.ThrowIfNull(pool);

            T[] array = pool.Rent(size);

            if (zero)
            {
                InitializeBlock(array, (uint)size);
            }

            //Use the array pool buffer wrapper to return the array to the pool when the handle is disposed
            return new ArrayPoolBuffer<T>(pool, array, size);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IMemoryHandle<T> SafeAlloc<T>(nuint elements, bool zero = false) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);

            if (CanUseUnmanagedHeap<T>(Shared, elements))
            {
                return SafeAlloc<T>(Shared, elements, zero);
            }
            else
            {
                //Should never happen because max pool size guards against this
                Debug.Assert(elements <= int.MaxValue);
                
                return SafeAlloc(ArrayPool<T>.Shared, (int)elements, zero);
            }
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IMemoryHandle<T> SafeAlloc<T>(int elements, bool zero = false) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return SafeAlloc<T>((nuint)elements, zero);
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
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IMemoryHandle<T> SafeAllocNearestPage<T>(nuint elements, bool zero = false) where T : unmanaged
            => SafeAlloc<T>(elements: NearestPage<T>(elements), zero);

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IMemoryHandle<T> SafeAllocNearestPage<T>(int elements, bool zero = false) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);         
            return SafeAllocNearestPage<T>((nuint)elements, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged memory from the specified heap rounded
        /// to the nearest page for the desired number of elements of the specified type
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <param name="heap">The heap to allocate the block of memory from</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle<T> SafeAllocNearestPage<T>(IUnmanagedHeap heap, int elements, bool zero = false) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);

            return SafeAllocNearestPage<T>(heap, (nuint)elements, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <param name="heap">The heap to allocate the block of memory from</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle<T> SafeAllocNearestPage<T>(IUnmanagedHeap heap, nuint elements, bool zero = false) where T : unmanaged
          => SafeAlloc<T>(heap, elements: NearestPage<T>(elements), zero);

        /// <summary>
        /// Allocates a block of unmanaged memory from the specified heap rounded
        /// to the nearest page for the desired number of elements of the specified type
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <param name="heap">The heap to allocate the block of memory from</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeMemoryHandle<T> UnsafeAllocNearestPage<T>(IUnmanagedHeap heap, int elements, bool zero = false) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);

            return UnsafeAlloc<T>(heap, elements: (int)NearestPage<T>(elements), zero);
        }

        /// <summary>
        /// Allocates a structure of the specified type on the specified 
        /// Unmanaged heap and optionally zero's it's memory
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="heap">The heap to allocate structure memory from</param>
        /// <param name="zero">A value that indicates if the structure memory should be zeroed before returning</param>
        /// <returns>A pointer to the structure ready for use.</returns>
        /// <remarks>Allocations must be freed with <see cref="StructFree{T}(IUnmanagedHeap, T*)"/></remarks>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* StructAlloc<T>(IUnmanagedHeap heap, bool zero) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(heap);
            return (T*)heap.Alloc(1, (nuint)sizeof(T), zero);
        }

        /// <summary>
        /// Allocates a structure of the specified type on the specified 
        /// Unmanaged heap and optionally zero's it's memory, then returns
        /// and reference to the heap allocated structure.
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="heap">The heap to allocate structure memory from</param>
        /// <param name="zero">A value that indicates if the structure memory should be zeroed before returning</param>
        /// <returns>A reference to the heap allocated structure</returns>
        /// <remarks>Allocations must be freed with <see cref="StructFreeRef{T}(IUnmanagedHeap, ref T)"/></remarks>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T StructAllocRef<T>(IUnmanagedHeap heap, bool zero) where T : unmanaged 
            => ref Unsafe.AsRef<T>(StructAlloc<T>(heap, zero));

        /// <summary>
        /// Frees a structure allocated with <see cref="StructAlloc{T}(IUnmanagedHeap, bool)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="heap">Heap the structure was allocated from to free it back to</param>
        /// <param name="structPtr">A pointer to the unmanaged structure to free</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StructFree<T>(IUnmanagedHeap heap, T* structPtr) where T : unmanaged 
            => StructFree(heap, (void*)structPtr);

        /// <summary>
        /// Frees a structure allocated with <see cref="StructAllocRef{T}(IUnmanagedHeap, bool)"/>
        /// by its reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="heap">Heap the structure was allocated from to free it back to</param>
        /// <param name="structRef">A reference to the unmanaged structure to free</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StructFreeRef<T>(IUnmanagedHeap heap, ref T structRef) where T : unmanaged 
            => StructFree(heap, Unsafe.AsPointer(ref structRef));

        /// <summary>
        /// Frees a structure allocated with <see cref="StructAlloc{T}(IUnmanagedHeap, bool)"/>
        /// </summary>
        /// <param name="heap">Heap the structure was allocated from to free it back to</param>
        /// <param name="structPtr">A pointer to the unmanaged structure to free</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void StructFree(IUnmanagedHeap heap, void* structPtr)
        {
            ArgumentNullException.ThrowIfNull(heap);
            ArgumentNullException.ThrowIfNull(structPtr);
            
            //Get intpointer
            IntPtr ptr = (IntPtr)structPtr;
          
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
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static UnsafeMemoryHandle<byte> UnsafeAlloc(int elements, bool zero = false)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);

            if(elements == 0)
            {
                return default;
            }

            return CanUseUnmanagedHeap<byte>(Shared, (uint)elements)
                ? UnsafeAlloc<byte>(Shared, elements, zero)
                : UnsafeAlloc(ArrayPool<byte>.Shared, elements, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeMemoryHandle<byte> UnsafeAllocNearestPage(int elements, bool zero = false)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);

            //Round to nearest page (in bytes)
            return UnsafeAlloc(elements: (int)NearestPage(elements), zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static IMemoryHandle<byte> SafeAlloc(int elements, bool zero = false)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);

            return CanUseUnmanagedHeap<byte>(Shared, (uint)elements)
                ? SafeAlloc<byte>(Shared, (nuint)elements, zero)
                : SafeAlloc(ArrayPool<byte>.Shared, elements, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IMemoryHandle<byte> SafeAllocNearestPage(int elements, bool zero = false)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return SafeAlloc(elements: (int)NearestPage(elements), zero);
        }

        /// <summary>
        /// DANGEROUS: Creates a new <see cref="MemoryHandle{T}"/> from an existing pointer that 
        /// belongs to the specified heap. This handle owns the pointer and will free it back 
        /// to the heap when disposed. No validation is performed on the pointer, so it is
        /// the responsibility of the caller to ensure the pointer is valid and points to a
        /// block of memory that was allocated from the specified heap.
        /// </summary>
        /// <param name="blockPtr">The pointer to the existing block of memory</param>
        /// <param name="elements">The element size of the block (NOT byte size)</param>
        /// <param name="heap">The unmanaged heap this block was allocated from (and can be freed back to)</param>
        /// <param name="zero">A flag that indicates if block should be zeroed during reallocations</param>
        /// <returns>A <see cref="MemoryHandle{T}"/> wrapper</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle<T> ToHandle<T>(nint blockPtr, nuint elements, IUnmanagedHeap heap, bool zero) 
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(heap);

            return new MemoryHandle<T>(heap, blockPtr, elements, zero);
        }

        /// <summary>
        /// DANGEROUS: Creates a new <see cref="UnsafeMemoryHandle{T}"/> from an existing pointer that 
        /// belongs to the specified heap. This handle owns the pointer and will free it back 
        /// to the heap when disposed. No validation is performed on the pointer, so it is
        /// the responsibility of the caller to ensure the pointer is valid and points to a
        /// block of memory that was allocated from the specified heap.
        /// </summary>
        /// <param name="blockPtr">The pointer to the existing block of memory</param>
        /// <param name="elements">The element size of the block (NOT byte size)</param>
        /// <param name="heap">The unmanaged heap this block was allocated from (and can be freed back to)</param>
        /// <returns>A <see cref="UnsafeMemoryHandle{T}"/> wrapper</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeMemoryHandle<T> ToUnsafeHandle<T>(nint blockPtr, int elements, IUnmanagedHeap heap)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(heap);

            return new UnsafeMemoryHandle<T>(heap, blockPtr, elements);
        }


        /// <summary>
        /// DANGEROUS: Creates a new <see cref="UnsafeMemoryHandle{T}"/> from an existing pointer that 
        /// belongs to the specified heap. This handle owns the pointer and will free it back 
        /// to the heap when disposed. No validation is performed on the pointer, so it is
        /// the responsibility of the caller to ensure the pointer is valid and points to a
        /// block of memory that was allocated from the specified heap.
        /// </summary>
        /// <param name="block">The array buffer to wrap</param>
        /// <param name="elements">The element size of the block (NOT byte size)</param>
        /// <param name="pool">The pool which the array can be free back to</param>
        /// <returns>A <see cref="UnsafeMemoryHandle{T}"/> wrapper</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeMemoryHandle<T> ToUnsafeHandle<T>(T[] block, int elements, ArrayPool<T> pool)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(block);
            ArgumentNullException.ThrowIfNull(pool);

            return new UnsafeMemoryHandle<T>(pool, block, elements);
        }

        /// <summary>
        /// Determines if two memory handles are equal by comparing their 
        /// lengths and their base block addresses. This method is useful
        /// for comparing memory handles that may have been reallocated
        /// or copied.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle1">The first handle to compare</param>
        /// <param name="handle2">The secondary handle to compare against</param>
        /// <returns>
        /// True if the handle lengths are equal and the base block addresses
        /// are the same, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreHandlesEqual<T>(IMemoryHandle<T> handle1, IMemoryHandle<T> handle2)
        {
            ArgumentNullException.ThrowIfNull(handle1);
            ArgumentNullException.ThrowIfNull(handle2);

            return handle1.Length == handle2.Length 
                && Unsafe.AreSame(ref handle1.GetReference(), ref handle2.GetReference());
        }

        #endregion
    }

}
