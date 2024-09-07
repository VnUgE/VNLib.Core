/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: Win32PrivateHeap.cs 
*
* Win32PrivateHeap.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Native;

using DWORD = System.Int64;
using LPVOID = nint;

namespace VNLib.Utils.Memory
{
    ///<summary>
    ///<para>
    /// Provides a win32 private heap managed wrapper class
    ///</para>
    ///</summary>
    ///<remarks>
    /// <see cref="Win32PrivateHeap"/> implements <see cref="SafeHandle"/> and tracks allocated blocks by its 
    /// referrence counter. Allocations increment the count, and free's decrement the count, so the heap may 
    /// be disposed safely
    /// </remarks>
    [ComVisible(false)]
    [SupportedOSPlatform("Windows")]
    public sealed partial class Win32PrivateHeap : UnmanagedHeapBase
    {
        private const string KERNEL_DLL = "Kernel32";
       
        #region Extern
        //Heap flags
        public const DWORD HEAP_NO_FLAGS = 0x00;
        public const DWORD HEAP_GENERATE_EXCEPTIONS = 0x04;
        public const DWORD HEAP_NO_SERIALIZE = 0x01;
        public const DWORD HEAP_REALLOC_IN_PLACE_ONLY = 0x10;
        public const DWORD HEAP_ZERO_MEMORY = 0x08;


        [LibraryImport(KERNEL_DLL, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial LPVOID GetProcessHeap();

        [LibraryImport(KERNEL_DLL, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial LPVOID HeapAlloc(IntPtr hHeap, DWORD flags, nuint dwBytes);
        
        [LibraryImport(KERNEL_DLL, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial LPVOID HeapReAlloc(IntPtr hHeap, DWORD dwFlags, LPVOID lpMem, nuint dwBytes);
        
        [LibraryImport(KERNEL_DLL, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool HeapFree(IntPtr hHeap, DWORD dwFlags, LPVOID lpMem);

        [LibraryImport(KERNEL_DLL, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial LPVOID HeapCreate(DWORD flOptions, nuint dwInitialSize, nuint dwMaximumSize);
        
        [LibraryImport(KERNEL_DLL, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool HeapDestroy(IntPtr hHeap);
        
        [LibraryImport(KERNEL_DLL, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool HeapValidate(IntPtr hHeap, DWORD dwFlags, LPVOID lpMem);
        
        [LibraryImport(KERNEL_DLL, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial nuint HeapSize(IntPtr hHeap, DWORD flags, LPVOID lpMem);

        #endregion

        /// <summary>
        /// Create a new <see cref="Win32PrivateHeap"/> with the specified sizes and flags
        /// </summary>
        /// <param name="initialSize">Intial size of the heap</param>
        /// <param name="maxHeapSize">Maximum size allowed for the heap (disabled = 0, default)</param>
        /// <param name="flags">Defalt heap flags to set globally for all blocks allocated by the heap (default = 0)</param>
        /// <param name="cFlags">Flags to configure heap creation</param>
        /// <remarks>
        /// Win32 heaps are not thread safe, so synchronization is required, you may disabled internal locking if you use 
        /// your own synchronization.
        /// </remarks>
        public static Win32PrivateHeap Create(nuint initialSize, HeapCreation cFlags, nuint maxHeapSize = 0, DWORD flags = HEAP_NO_FLAGS)
        {
            if (cFlags.HasFlag(HeapCreation.Shared))
            {
                /*
                 * When using the process-wide heap, synchronization is enabled 
                 * so we should clear the flag to prevent UnmanagedHeapBase from
                 * using CLR mutual exclusion methods
                 */
                cFlags &= ~HeapCreation.UseSynchronization;

                return new Win32PrivateHeap(
                    GetProcessHeap(), 
                    cFlags, 
                    ownsHeandle: false  //The heap does not own the handle because it's shared, so it cannot be freed
                );
            }
            
            if (cFlags.HasFlag(HeapCreation.UseSynchronization))
            {    
                /*
                 * When the synchronization flag is set, we dont need to use 
                 * the win32 serialization method because the UnmanagedHeapBase
                 * class will handle locking using better/faster CLR methods
                 */

                flags |= HEAP_NO_SERIALIZE;
            }

            //Call create, throw exception if the heap falled to allocate
            ERRNO heapHandle = (ERRNO)HeapCreate(flags, initialSize, maxHeapSize);
            
            if (!heapHandle)
            {
                throw new NativeMemoryException("Heap could not be created");
            }

            Trace.WriteLine($"Win32 private heap {heapHandle:x} created");

            //Heap has been created so we can wrap it
            return new(heapHandle, cFlags, ownsHeandle: true);
        }

        /// <summary>
        /// LIFETIME WARNING. Consumes a valid win32 handle and will manage it's lifetime once constructed.
        /// Locking and memory blocks will attempt to be allocated from this heap handle.
        /// </summary>
        /// <param name="win32HeapHandle">An open and valid handle to a win32 private heap</param>
        /// <param name="flags">The heap creation flags to obey</param>
        /// <returns>A wrapper around the specified heap</returns>
        public static Win32PrivateHeap ConsumeExisting(IntPtr win32HeapHandle, HeapCreation flags) 
            => new (win32HeapHandle, flags, ownsHeandle: true);

        private Win32PrivateHeap(IntPtr heapPtr, HeapCreation flags, bool ownsHeandle) : base(flags, ownsHeandle) 
            => handle = heapPtr;

        /// <summary>
        /// Retrieves the size of a memory block allocated from the current heap.
        /// </summary>
        /// <param name="block">The pointer to a block of memory to get the size of</param>
        /// <returns>The size of the block of memory, (SIZE_T)-1 if the operation fails</returns>
        public nuint HeapSize(ref LPVOID block) => HeapSize(handle, HEAP_NO_FLAGS, block);

        /// <summary>
        /// Validates the specified block of memory within the current heap instance. This function will block hte 
        /// </summary>
        /// <param name="block">Pointer to the block of memory to validate</param>
        /// <returns>True if the block is valid, false otherwise</returns>
        public bool Validate(ref LPVOID block)
        {
            //Lock the heap before validating
            lock (HeapLock)
            {
                //validate the block on the current heap
                return HeapValidate(handle, HEAP_NO_FLAGS, block);
            }
        }

        /// <summary>
        /// Validates the current heap instance. The function scans all the memory blocks in the heap and verifies that the heap control structures maintained by 
        /// the heap manager are in a consistent state.
        /// </summary>
        /// <returns>If the specified heap or memory block is valid, the return value is nonzero.</returns>
        /// <remarks>This can be a consuming operation which will block all allocations</remarks>
        public bool Validate()
        {
            //Lock the heap before validating
            lock (HeapLock)
            {
                //validate the entire heap
                return HeapValidate(handle, HEAP_NO_FLAGS, IntPtr.Zero);
            }
        }

        ///<inheritdoc/>
        protected override bool ReleaseHandle()
        {
            Trace.WriteLine($"Win32 private heap {handle:x} destroyed");

            return HeapDestroy(handle);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override sealed LPVOID AllocBlock(nuint elements, nuint size, bool zero)
        {
            return HeapAlloc(
                handle, 
                flags: zero ? HEAP_ZERO_MEMORY : HEAP_NO_FLAGS, 
                dwBytes: checked(elements * size)
            );
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override sealed bool FreeBlock(LPVOID block) 
            => HeapFree(handle, HEAP_NO_FLAGS, block);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override sealed LPVOID ReAllocBlock(LPVOID block, nuint elements, nuint size, bool zero)
        {
            return HeapReAlloc(
                handle, 
                dwFlags: zero ? HEAP_ZERO_MEMORY : HEAP_NO_FLAGS, 
                lpMem: block, 
                dwBytes: checked(elements * size)
            );
        }
    }
}