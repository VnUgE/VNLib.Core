﻿/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: UnmanagedHeapBase.cs 
*
* UnmanagedHeapBase.cs is part of VNLib.Utils which is part of the larger 
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
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using VNLib.Utils.Native;

using LPVOID = nint;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides a synchronized base methods for accessing unmanaged memory. Implements <see cref="SafeHandle"/>
    /// for safe disposal of heaps
    /// </summary>
    /// <param name="flags">The creation flags to obey</param>
    /// <param name="ownsHandle">A flag that indicates if the handle is owned by the instance</param>
    public abstract class UnmanagedHeapBase(HeapCreation flags, bool ownsHandle) : SafeHandleZeroOrMinusOneIsInvalid(ownsHandle), IUnmangedHeap
    {
        /// <summary>
        /// The heap synchronization handle
        /// </summary>
        protected readonly object HeapLock = new();      

        ///<inheritdoc/>
        public HeapCreation CreationFlags => flags;

        ///<inheritdoc/>
        ///<remarks>Increments the handle count, free must be called to decrement the handle count</remarks>
        ///<exception cref="OverflowException"></exception>
        ///<exception cref="OutOfMemoryException"></exception>
        ///<exception cref="ObjectDisposedException"></exception>
        public LPVOID Alloc(nuint elements, nuint size, bool zero)
        {
            //Check for overflow for size
            _ = checked(elements * size);

            //Force zero if global flag is set
            zero |= (flags & HeapCreation.GlobalZero) > 0;
            bool handleCountIncremented = false;

            //Increment handle count to prevent premature release
            DangerousAddRef(ref handleCountIncremented);

            //Failed to increment ref count, class has been disposed
            ObjectDisposedException.ThrowIf(handleCountIncremented == false, this);

            try
            {
                LPVOID block;

                //Check if lock should be used
                if ((flags & HeapCreation.UseSynchronization) > 0)
                {
                    //Enter lock
                    lock(HeapLock)
                    {
                        //Alloc block
                        block = AllocBlock(elements, size, zero);
                    }
                }
                else
                {
                    //Alloc block without lock
                    block = AllocBlock(elements, size, zero);
                }

                //Check block 
                NativeMemoryOutOfMemoryException.ThrowIfNullPointer(block);

                //Check if block was allocated
                return block;
            }
            catch
            {
                //Decrement handle count since allocation failed
                DangerousRelease();
                throw;
            }
        }

        ///<inheritdoc/>
        ///<exception cref="OverflowException"></exception>
        ///<remarks>Decrements the handle count</remarks>
        public bool Free(ref LPVOID block)
        {           
            bool result;

            //If disposed, set the block handle to zero and exit to avoid raising exceptions during finalization
            if (IsClosed)
            {
                block = LPVOID.Zero;
                return true;
            }

            /*
             * Checking for invalid is not really necesasry because 
             * the only way the handle can be invalidated is 
             * if some derrived class mutates the handle value
             * and doesn't close the handle
             */
            Debug.Assert(IsInvalid == false);

            if ((flags & HeapCreation.UseSynchronization) > 0)
            {
                //wait for lock
                lock (HeapLock)
                {                   
                    result = FreeBlock(block);
                    //Release lock before releasing handle
                }
            }
            else
            {
                //No lock needed
                result = FreeBlock(block);
            }

            //Decrement handle count
            DangerousRelease();
            //set block to invalid
            block = 0;
            return result;
        }
        
        ///<inheritdoc/>
        ///<exception cref="OverflowException"></exception>
        ///<exception cref="ObjectDisposedException"></exception>
        public void Resize(ref LPVOID block, nuint elements, nuint size, bool zero)
        {
            if ((flags & HeapCreation.SupportsRealloc) == 0)
            {
                throw new NotSupportedException("The underlying heap does not support block reallocation");
            }

            //Check for overflow for size
            _ = checked(elements * size);

            LPVOID newBlock;

            //Global zero flag will cause a zero
            zero |= (flags & HeapCreation.GlobalZero) > 0;

            /*
             * Realloc may return a null pointer if allocation fails
             * so check the results and only assign the block pointer
             * if the result is valid. Otherwise pointer block should 
             * be left untouched
             */

            if ((flags & HeapCreation.UseSynchronization) > 0)
            {
                lock (HeapLock)
                {
                    newBlock = ReAllocBlock(block, elements, size, zero);
                }
            }
            else
            {
                newBlock = ReAllocBlock(block, elements, size, zero);
            }

            //Check block 
            NativeMemoryOutOfMemoryException.ThrowIfNullPointer(newBlock, "The memory block could not be resized");

            //Set the new block
            block = newBlock;
        }

        /// <summary>
        /// Allocates a block of memory from the heap
        /// </summary>
        /// <param name="elements">The number of elements within the block</param>
        /// <param name="size">The size of the element type (in bytes)</param>
        /// <param name="zero">A flag to zero the allocated block</param>
        /// <returns>A pointer to the allocated block</returns>
        protected abstract LPVOID AllocBlock(nuint elements, nuint size, bool zero);
        
        /// <summary>
        /// Frees a previously allocated block of memory
        /// </summary>
        /// <param name="block">The block to free</param>
        protected abstract bool FreeBlock(LPVOID block);

        /// <summary>
        /// Resizes the previously allocated block of memory on the current heap
        /// </summary>
        /// <param name="block">The prevously allocated block</param>
        /// <param name="elements">The new number of elements within the block</param>
        /// <param name="size">The size of the element type (in bytes)</param>
        /// <param name="zero">A flag to indicate if the new region of the block should be zeroed</param>
        /// <returns>A pointer to the same block, but resized, null if the allocation falied</returns>
        /// <remarks>
        /// Heap base relies on the block pointer to remain unchanged if the resize fails so the
        /// block is still valid, and the return value is used to determine if the resize was successful
        /// </remarks>
        protected abstract LPVOID ReAllocBlock(LPVOID block, nuint elements, nuint size, bool zero);
        
        ///<inheritdoc/>
        public override int GetHashCode() => handle.GetHashCode();

        ///<inheritdoc/>
        public override bool Equals(object? obj) 
            => obj is UnmanagedHeapBase heap && !heap.IsInvalid && !heap.IsClosed && handle == heap.handle;
    }
}
