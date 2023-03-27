/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IUnmangedHeap.cs 
*
* IUnmangedHeap.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Abstraction for handling (allocating, resizing, and freeing) blocks of unmanaged memory from an unmanged heap
    /// </summary>
    public interface IUnmangedHeap : IDisposable
    {
        /// <summary>
        /// The creation flags the heap was initialized with
        /// </summary>
        HeapCreation CreationFlags { get; }

        /// <summary>
        /// Allocates a block of memory from the heap and returns a pointer to the new memory block
        /// </summary>
        /// <param name="size">The size (in bytes) of the element</param>
        /// <param name="elements">The number of elements to allocate</param>
        /// <param name="zero">An optional parameter to zero the block of memory</param>
        /// <returns></returns>
        IntPtr Alloc(nuint elements, nuint size, bool zero);

        /// <summary>
        /// Resizes the allocated block of memory to the new size
        /// </summary>
        /// <param name="block">The block to resize</param>
        /// <param name="elements">The new number of elements</param>
        /// <param name="size">The size (in bytes) of the type</param>
        /// <param name="zero">An optional parameter to zero the block of memory</param>
        void Resize(ref IntPtr block, nuint elements, nuint size, bool zero);

        /// <summary>
        /// Free's a previously allocated block of memory
        /// </summary>
        /// <param name="block">The memory to be freed</param>
        /// <returns>A value indicating if the free operation succeeded</returns>
        bool Free(ref IntPtr block);
    }
}
