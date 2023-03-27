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
                return new(ArrayPool<T>.Shared, elements, zero);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeMemoryHandle<T> UnsafeAllocNearestPage<T>(int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            //Round to nearest page (in bytes)
            nint np = NearestPage(elements * sizeof(T));

            //Resize to element size
            np /= sizeof(T);

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
                return new VnTempBuffer<T>(ArrayPool<T>.Shared, elements, zero);
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
            nint np = NearestPage(elements * sizeof(T));

            //Resize to element size
            np /= sizeof(T);

            return SafeAlloc<T>((int)np, zero);
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
                return new(ArrayPool<byte>.Shared, elements, zero);
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
                return new VnTempBuffer<byte>(ArrayPool<byte>.Shared, elements, zero);
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
