/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ProcessHeap.cs 
*
* ProcessHeap.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Native;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides a <see cref="IUnmangedHeap"/> wrapper for the <see cref="Marshal"/> virtualalloc 
    /// global heap methods
    /// </summary>
    [ComVisible(false)]
    public unsafe class ProcessHeap : VnDisposeable, IUnmangedHeap
    {
        /// <summary>
        /// Gets the shared process heap instance
        /// </summary>
        public static ProcessHeap Shared { get; } = new();

        /// <summary>
        /// <inheritdoc/>
        /// <para>
        /// Is always <see cref="HeapCreation.Shared"/> as this heap is the default 
        /// process heap. Meaining memory will be shared across the process
        /// </para>
        /// </summary>
        public HeapCreation CreationFlags { get; } = HeapCreation.Shared | HeapCreation.SupportsRealloc;

        /// <summary>
        /// Initalizes a new global (cross platform) process heap
        /// </summary>
        public ProcessHeap() => Trace.WriteLine($"Default heap instnace created {GetHashCode():x}");

        ///<inheritdoc/>
        ///<exception cref="OverflowException"></exception>
        ///<exception cref="OutOfMemoryException"></exception>
        public IntPtr Alloc(nuint elements, nuint size, bool zero)
        {
            return zero
                ? (IntPtr)NativeMemory.AllocZeroed(elements, size)
                : (IntPtr)NativeMemory.Alloc(elements, size);
        }
        ///<inheritdoc/>
        public bool Free(ref IntPtr block)
        {
            //Free native mem from ptr
            NativeMemory.Free(block.ToPointer());
            
            block = IntPtr.Zero;

            return true;
        }

        ///<inheritdoc/>
        protected override void Free() => Trace.WriteLine($"Default heap instnace disposed {GetHashCode():x}");

        ///<inheritdoc/>
        ///<exception cref="OverflowException"></exception>
        ///<exception cref="OutOfMemoryException"></exception>
        public void Resize(ref IntPtr block, nuint elements, nuint size, bool zero)
        {
            nuint bytes = checked(elements * size);
            
            //Alloc
            nint newBlock = (nint)NativeMemory.Realloc(block.ToPointer(), bytes);

            //Check
            NativeMemoryOutOfMemoryException.ThrowIfNullPointer(newBlock, "Failed to resize the allocated block");

            //Assign block ptr
            block = newBlock;
        }
    }
}
