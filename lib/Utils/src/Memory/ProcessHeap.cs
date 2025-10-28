/*
* Copyright (c) 2025 Vaughn Nugent
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
    /// Provides a <see cref="IUnmanagedHeap"/> wrapper for the <see cref="Marshal"/> virtualalloc 
    /// global heap methods
    /// </summary>
    [ComVisible(false)]
    public unsafe class ProcessHeap : VnDisposeable, IUnmanagedHeap
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
        public ProcessHeap(HeapCreation flags = HeapCreation.None)
        {
            /*
             * Since this is just a wrapper around the NativeMemory class,
             * which uses a global/shared heap internally we need to inform 
             * the user of that. It can never be created as a non-shared heap.
             * 
             * Native memory should always support realloc, so always set that flag
             */
            CreationFlags = flags | HeapCreation.Shared | HeapCreation.SupportsRealloc;

            // Clear the syncronization flag since locking is never needed or used
            CreationFlags &= ~HeapCreation.UseSynchronization;

            Trace.WriteLine($"Default heap instnace created {GetHashCode():x} with flags {CreationFlags:x}");
        }

        ///<inheritdoc/>
        ///<exception cref="OverflowException"></exception>
        ///<exception cref="OutOfMemoryException"></exception>
        public IntPtr Alloc(nuint elements, nuint size, bool zero)
        {
            Debug.Assert(!Disposed, "alloc called on a ProcessHeap that has been disposed");

            // local zero or global zero flag
            zero |= (CreationFlags & HeapCreation.GlobalZero) > 0;

            return zero
                ? (IntPtr)NativeMemory.AllocZeroed(elements, size)
                : (IntPtr)NativeMemory.Alloc(elements, size);
        }

        ///<inheritdoc/>
        public bool Free(ref IntPtr block)
        {
            Debug.Assert(!Disposed, "free called on a ProcessHeap that has been disposed");

            //Free native mem from ptr
            NativeMemory.Free(block.ToPointer());

            block = IntPtr.Zero;

            return true;
        }

        ///<inheritdoc/>
        ///<exception cref="OverflowException"></exception>
        ///<exception cref="OutOfMemoryException"></exception>
        public void Resize(ref IntPtr block, nuint elements, nuint size, bool zero)
        {
            Debug.Assert(!Disposed, "resize called on a ProcessHeap that has been disposed");

            //Alloc
            nint newBlock = (nint)NativeMemory.Realloc(
                block.ToPointer(),
                byteCount: checked(elements * size)
            );

            //Check
            NativeMemoryOutOfMemoryException.ThrowIfNullPointer(newBlock, "Failed to resize the allocated block");

            //Assign block ptr
            block = newBlock;
        }

        ///<inheritdoc/>
        protected override void Free() => Trace.WriteLine($"Default heap instnace disposed {GetHashCode():x}");
    }
}
