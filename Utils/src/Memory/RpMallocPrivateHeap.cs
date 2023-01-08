/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: RpMallocPrivateHeap.cs 
*
* RpMallocPrivateHeap.cs is part of VNLib.Utils which is part of the larger 
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using size_t = System.UInt64;
using LPVOID = System.IntPtr;
using LPHEAPHANDLE = System.IntPtr;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// A wrapper class for cross platform RpMalloc implementation.
    /// </summary>
    [ComVisible(false)]
    public sealed class RpMallocPrivateHeap : UnmanagedHeapBase
    {
        const string DLL_NAME = "rpmalloc";

        #region statics
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern int rpmalloc_initialize();
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern void rpmalloc_finalize();

        //Heap api
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPHEAPHANDLE rpmalloc_heap_acquire();
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern void rpmalloc_heap_release(LPHEAPHANDLE heap);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rpmalloc_heap_alloc(LPHEAPHANDLE heap, size_t size);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rpmalloc_heap_aligned_alloc(LPHEAPHANDLE heap, size_t alignment, size_t size);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rpmalloc_heap_calloc(LPHEAPHANDLE heap, size_t num, size_t size);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rpmalloc_heap_aligned_calloc(LPHEAPHANDLE heap, size_t alignment, size_t num, size_t size);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rpmalloc_heap_realloc(LPHEAPHANDLE heap, LPVOID ptr, size_t size, nuint flags);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rpmalloc_heap_aligned_realloc(LPHEAPHANDLE heap, LPVOID ptr, size_t alignment, size_t size, nuint flags);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern void rpmalloc_heap_free(LPHEAPHANDLE heap, LPVOID ptr);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern void rpmalloc_heap_free_all(LPHEAPHANDLE heap);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern void rpmalloc_heap_thread_set_current(LPHEAPHANDLE heap);

        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern void rpmalloc_thread_initialize();
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern int rpmalloc_is_thread_initialized();
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern void rpmalloc_thread_finalize(int release_caches);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rpmalloc(size_t size);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rpcalloc(size_t num, size_t size);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern LPVOID rprealloc(LPVOID ptr, size_t size);
        [DllImport(DLL_NAME, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        static extern void rpfree(LPVOID ptr);

        #endregion

        private class RpMallocGlobalHeap : IUnmangedHeap
        {
            IntPtr IUnmangedHeap.Alloc(ulong elements, ulong size, bool zero)
            {
                return RpMalloc(elements, (nuint)size, zero);
            }

            //Global heap does not need to be disposed
            void IDisposable.Dispose()
            { }

            bool IUnmangedHeap.Free(ref IntPtr block)
            {
                //Free the block
                RpFree(ref block);
                return true;
            }

            void IUnmangedHeap.Resize(ref IntPtr block, ulong elements, ulong size, bool zero)
            {
                //Try to resize the block
                IntPtr resize = RpRealloc(block, elements, (nuint)size);

                if (resize == IntPtr.Zero)
                {
                    throw new NativeMemoryOutOfMemoryException("Failed to resize the block");
                }
                //assign ptr
                block = resize;
            }
        }

        /// <summary>
        /// <para>
        /// A <see cref="IUnmangedHeap"/> API for the RPMalloc library if loaded. 
        /// </para>
        /// <para>
        /// This heap is thread safe and may be converted to a <see cref="MemoryManager{T}"/>
        /// infinitley and disposed safely.
        /// </para>
        /// <para>
        /// If the native library is not loaded, calls to this API will throw a <see cref="DllNotFoundException"/>.
        /// </para>
        /// </summary>
        public static IUnmangedHeap GlobalHeap { get; } = new RpMallocGlobalHeap();

        /// <summary>
        /// <para>
        /// Initializes RpMalloc for the current thread and alloctes a block of memory
        /// </para>
        /// </summary>
        /// <param name="elements">The number of elements to allocate</param>
        /// <param name="size">The number of bytes per element type (aligment)</param>
        /// <param name="zero">Zero the block of memory before returning</param>
        /// <returns>A pointer to the block, (zero if failed)</returns>
        public static LPVOID RpMalloc(size_t elements, nuint size, bool zero)
        {
            //See if the current thread has been initialized
            if (rpmalloc_is_thread_initialized() == 0)
            {
                //Initialize the current thread
                rpmalloc_thread_initialize();
            }
            //Alloc block
            LPVOID block;
            if (zero)
            {
                block = rpcalloc(elements, size);
            }
            else
            {
                //Calculate the block size
                ulong blockSize = checked(elements * size);
                block = rpmalloc(blockSize);
            }
            return block;
        }
        
        /// <summary>
        /// Frees a block of memory allocated by RpMalloc
        /// </summary>
        /// <param name="block">A ref to the pointer of the block to free</param>
        public static void RpFree(ref LPVOID block)
        {
            if (block != IntPtr.Zero)
            {
                rpfree(block);
                block = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Attempts to re-allocate the specified block on the global heap
        /// </summary>
        /// <param name="block">A pointer to a previously allocated block of memory</param>
        /// <param name="elements">The number of elements in the block</param>
        /// <param name="size">The number of bytes in the element</param>
        /// <returns>A pointer to the new block if the reallocation succeeded, null if the resize failed</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OverflowException"></exception>
        public static LPVOID RpRealloc(LPVOID block, size_t elements, nuint size)
        {
            if(block == IntPtr.Zero)
            {
                throw new ArgumentException("The supplied block is not valid", nameof(block));
            }
            //Calc new block size
            size_t blockSize = checked(elements * size);
            return rprealloc(block, blockSize);
        }

        #region instance

        /// <summary>
        /// Initializes a new RpMalloc first class heap to allocate memory blocks from
        /// </summary>
        /// <param name="zeroAll">A global flag to zero all blocks of memory allocated</param>
        /// <exception cref="NativeMemoryOutOfMemoryException"></exception>
        public RpMallocPrivateHeap(bool zeroAll):base(zeroAll, true)
        {
            //Alloc the heap
            handle = rpmalloc_heap_acquire();
            if(IsInvalid)
            {
                throw new NativeMemoryException("Failed to aquire a new heap");
            }
#if TRACE
            Trace.WriteLine($"RPMalloc heap {handle:x} created");
#endif
        }
        ///<inheritdoc/>
        protected override bool ReleaseHandle()
        {
#if TRACE
            Trace.WriteLine($"RPMalloc heap {handle:x} destroyed");
#endif
            //Release all heap memory
            rpmalloc_heap_free_all(handle);
            //Destroy the heap
            rpmalloc_heap_release(handle);
            //Release base
            return base.ReleaseHandle();
        }
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected sealed override LPVOID AllocBlock(ulong elements, ulong size, bool zero)
        {
            //Alloc or calloc and initalize
            return zero ? rpmalloc_heap_calloc(handle, elements, size) : rpmalloc_heap_alloc(handle, checked(size * elements));
        }
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected sealed override bool FreeBlock(LPVOID block)
        {
            //Free block
            rpmalloc_heap_free(handle, block);
            return true;
        }
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected sealed override LPVOID ReAllocBlock(LPVOID block, ulong elements, ulong size, bool zero)
        {
            //Realloc
            return rpmalloc_heap_realloc(handle, block, checked(elements * size), 0);
        }
        #endregion
    }
}
