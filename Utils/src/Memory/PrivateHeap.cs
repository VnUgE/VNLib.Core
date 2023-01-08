/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: PrivateHeap.cs 
*
* PrivateHeap.cs is part of VNLib.Utils which is part of the larger 
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

using DWORD = System.Int64;
using SIZE_T = System.UInt64;
using LPVOID = System.IntPtr;

namespace VNLib.Utils.Memory
{
    ///<summary>
    ///<para>
    /// Provides a win32 private heap managed wrapper class
    ///</para>
    ///</summary>
    ///<remarks>
    /// <see cref="PrivateHeap"/> implements <see cref="SafeHandle"/> and tracks allocated blocks by its 
    /// referrence counter. Allocations increment the count, and free's decrement the count, so the heap may 
    /// be disposed safely
    /// </remarks>
    [ComVisible(false)]
    [SupportedOSPlatform("Windows")]
    public sealed class PrivateHeap : UnmanagedHeapBase
    {
        private const string KERNEL_DLL = "Kernel32";
       
        #region Extern
        //Heap flags
        public const DWORD HEAP_NO_FLAGS = 0x00;
        public const DWORD HEAP_GENERATE_EXCEPTIONS = 0x04;
        public const DWORD HEAP_NO_SERIALIZE = 0x01;
        public const DWORD HEAP_REALLOC_IN_PLACE_ONLY = 0x10;
        public const DWORD HEAP_ZERO_MEMORY = 0x08;

        [DllImport(KERNEL_DLL, SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern LPVOID HeapAlloc(IntPtr hHeap, DWORD flags, SIZE_T dwBytes);
        [DllImport(KERNEL_DLL, SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern LPVOID HeapReAlloc(IntPtr hHeap, DWORD dwFlags, LPVOID lpMem, SIZE_T dwBytes);
        [DllImport(KERNEL_DLL, SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool HeapFree(IntPtr hHeap, DWORD dwFlags, LPVOID lpMem);

        [DllImport(KERNEL_DLL, SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern LPVOID HeapCreate(DWORD flOptions, SIZE_T dwInitialSize, SIZE_T dwMaximumSize);
        [DllImport(KERNEL_DLL, SetLastError = true, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool HeapDestroy(IntPtr hHeap);
        [DllImport(KERNEL_DLL, SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool HeapValidate(IntPtr hHeap, DWORD dwFlags, LPVOID lpMem);
        [DllImport(KERNEL_DLL, SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.U8)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern SIZE_T HeapSize(IntPtr hHeap, DWORD flags, LPVOID lpMem);
        
        #endregion

        /// <summary>
        /// Create a new <see cref="PrivateHeap"/> with the specified sizes and flags
        /// </summary>
        /// <param name="initialSize">Intial size of the heap</param>
        /// <param name="maxHeapSize">Maximum size allowed for the heap (disabled = 0, default)</param>
        /// <param name="flags">Defalt heap flags to set globally for all blocks allocated by the heap (default = 0)</param>
        public static PrivateHeap Create(SIZE_T initialSize, SIZE_T maxHeapSize = 0, DWORD flags = HEAP_NO_FLAGS)
        {
            //Call create, throw exception if the heap falled to allocate
            IntPtr heapHandle = HeapCreate(flags, initialSize, maxHeapSize);
            if (heapHandle == IntPtr.Zero)
            {
                throw new NativeMemoryException("Heap could not be created");
            }
#if TRACE
            Trace.WriteLine($"Win32 private heap {heapHandle:x} created");
#endif
            //Heap has been created so we can wrap it
            return new(heapHandle);
        }
        /// <summary>
        /// LIFETIME WARNING. Consumes a valid win32 handle and will manage it's lifetime once constructed.
        /// Locking and memory blocks will attempt to be allocated from this heap handle.
        /// </summary>
        /// <param name="win32HeapHandle">An open and valid handle to a win32 private heap</param>
        /// <returns>A wrapper around the specified heap</returns>
        public static PrivateHeap ConsumeExisting(IntPtr win32HeapHandle) => new (win32HeapHandle);

        private PrivateHeap(IntPtr heapPtr) : base(false, true) => handle = heapPtr;

        /// <summary>
        /// Retrieves the size of a memory block allocated from the current heap.
        /// </summary>
        /// <param name="block">The pointer to a block of memory to get the size of</param>
        /// <returns>The size of the block of memory, (SIZE_T)-1 if the operation fails</returns>
        public SIZE_T HeapSize(ref LPVOID block) => HeapSize(handle, HEAP_NO_FLAGS, block);

        /// <summary>
        /// Validates the specified block of memory within the current heap instance. This function will block hte 
        /// </summary>
        /// <param name="block">Pointer to the block of memory to validate</param>
        /// <returns>True if the block is valid, false otherwise</returns>
        public bool Validate(ref LPVOID block)
        {
            bool result;
            //Lock the heap before validating
            HeapLock.Wait();
            //validate the block on the current heap
            result = HeapValidate(handle, HEAP_NO_FLAGS, block);
            //Unlock the heap
            HeapLock.Release();
            return result;
           
        }
        /// <summary>
        /// Validates the current heap instance. The function scans all the memory blocks in the heap and verifies that the heap control structures maintained by 
        /// the heap manager are in a consistent state.
        /// </summary>
        /// <returns>If the specified heap or memory block is valid, the return value is nonzero.</returns>
        /// <remarks>This can be a consuming operation which will block all allocations</remarks>
        public bool Validate()
        {
            bool result;
            //Lock the heap before validating
            HeapLock.Wait();
            //validate the entire heap
            result = HeapValidate(handle, HEAP_NO_FLAGS, IntPtr.Zero);
            //Unlock the heap
            HeapLock.Release();
            return result;
        }

        ///<inheritdoc/>
        protected override bool ReleaseHandle()
        {
#if TRACE
            Trace.WriteLine($"Win32 private heap {handle:x} destroyed");
#endif
            return HeapDestroy(handle) && base.ReleaseHandle();
        }
        ///<inheritdoc/>
        protected override sealed LPVOID AllocBlock(ulong elements, ulong size, bool zero)
        {
            ulong bytes = checked(elements * size);
            return HeapAlloc(handle, zero ? HEAP_ZERO_MEMORY : HEAP_NO_FLAGS, bytes);
        }
        ///<inheritdoc/>
        protected override sealed bool FreeBlock(LPVOID block) => HeapFree(handle, HEAP_NO_FLAGS, block);
        ///<inheritdoc/>
        protected override sealed LPVOID ReAllocBlock(LPVOID block, ulong elements, ulong size, bool zero)
        {
            ulong bytes = checked(elements * size);
            return HeapReAlloc(handle, zero ? HEAP_ZERO_MEMORY : HEAP_NO_FLAGS, block, bytes);
        }
    }
}