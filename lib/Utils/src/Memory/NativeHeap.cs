/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: NativeHeap.cs 
*
* NativeHeap.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils.Extensions;
using VNLib.Utils.Native;
using VNLib.Utils.Resources;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// <para>
    /// Allows for exposing a dynamically loaded native heap implementation.
    /// </para>
    /// </summary>
    public class NativeHeap : UnmanagedHeapBase
    {
        /// <summary>
        /// The name of the native function that will be called to create a heap instance
        /// </summary>
        public const string CREATE_METHOD_NAME = "heapCreate";
        /// <summary>
        /// The name of the native function that will be called to allocate memory in the native library
        /// </summary>
        public const string ALLOCATE_METHOD_NAME = "heapAlloc";
        /// <summary>
        /// The name of the native function that will be called to reallocate memory in the native library
        /// </summary>
        public const string REALLOC_METHOD_NAME = "heapRealloc";
        /// <summary>
        /// The name of the native function that will be called to free memory in the native library
        /// </summary>
        public const string FREE_METHOD_NAME = "heapFree";
        /// <summary>
        /// The name of the native function that will be called to destroy a heap instance in the native library
        /// </summary>
        public const string DESTROY_METHOD_NAME = "heapDestroy";

        /// <summary>
        /// <para>
        /// Loads an unmanaged heap at runtime, into the current process at the given path. The dll must conform
        /// to the unmanaged heap format. After the method table is loaded, the heapCreate function is called to 
        /// initialze the heap.
        /// </para>
        /// </summary>
        /// <param name="dllPath">The path to the heap's dll file to load into the process.</param>
        /// <param name="searchPath">The native library search path</param>
        /// <param name="creationFlags">Specifes the <see cref="HeapCreation"/> flags to pass to the heapCreate function</param>
        /// <param name="flags">Generic flags passed directly to the heapCreate function</param>
        /// <returns>The newly initialized <see cref="NativeHeap"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="DllNotFoundException"></exception>
        public unsafe static NativeHeap LoadHeap(string dllPath, DllImportSearchPath searchPath, HeapCreation creationFlags, ERRNO flags)
        {
            //Try to load the library
            SafeLibraryHandle library = SafeLibraryHandle.LoadLibrary(dllPath, searchPath);
            try
            {
                Trace.WriteLine($"Creating user defined native heap at {dllPath}");

                //Create the heap
                return CreateHeap(
                    Owned<SafeLibraryHandle>.OwnedValue(library),  // Owns the handle because it was created/loaded here
                    creationFlags, 
                    flags
                );
            }
            catch
            {
                //Cleanup
                library.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a new heap from a <see cref="SafeLibraryHandle"/> that is known to be a <see cref="NativeHeap"/>
        /// ELF library and safe to load named symbols from
        /// </summary>
        /// <param name="libHandle">The existing <see cref="SafeLibraryHandle"/></param>
        /// <param name="ownsHandle">A boolen value indicating if the heap owns the lifetime of the handle and should dispose it when disposed</param>
        /// <param name="creationFlags">Specifes the <see cref="HeapCreation"/> flags to pass to the heapCreate function</param>
        /// <param name="flags">Generic flags passed directly to the heapCreate function</param>
        /// <returns>The newly initialized <see cref="NativeHeap"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static NativeHeap CreateHeap(SafeLibraryHandle libHandle, bool ownsHandle, HeapCreation creationFlags, ERRNO flags) 
            => CreateHeap(new(libHandle, ownsHandle), creationFlags, flags);

        /// <summary>
        /// Creates a new heap from a <see cref="SafeLibraryHandle"/> that is known to be a <see cref="NativeHeap"/>
        /// ELF library and safe to load named symbols from
        /// </summary>
        /// <param name="libHandle">
        /// The existing <see cref="SafeLibraryHandle"/> wrapped in an <see cref="Owned{T}"/> structure
        /// </param>
        /// <param name="creationFlags">Specifies the <see cref="HeapCreation"/> flags to pass to the heapCreate function</param>
        /// <param name="flags">Generic flags passed directly to the heapCreate function</param>
        /// <returns>The newly initialized <see cref="NativeHeap"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public unsafe static NativeHeap CreateHeap(Owned<SafeLibraryHandle> libHandle, HeapCreation creationFlags, ERRNO flags)
        {
            //Create a flags structure with defaults
            UnmanagedHeapDescriptor hFlags = new()
            {
                CreationFlags   = creationFlags,
                Flags           = flags,
                HeapPointer     = IntPtr.Zero
            };

            return CreateHeap(libHandle, &hFlags);
        }

        private unsafe static NativeHeap CreateHeap(Owned<SafeLibraryHandle> libHandle, UnmanagedHeapDescriptor* flags)
        {
            //Open method table
            HeapMethods table = new(libHandle);           

            //Get the create method
            CreateHeapDelegate create = libHandle.Value.DangerousGetFunction<CreateHeapDelegate>();

            //Create the new heap
            if (!create(flags))
            {
                throw new NativeMemoryException("Failed to create the new heap, the heap create method returned a null pointer");
            }

            Trace.WriteLine($"Successfully created user defined native heap 0x{flags->HeapPointer:x} with flags 0x{flags->CreationFlags:x}");

            //Return the neap heap
            return new(flags, table);
        }
       
        private HeapMethods MethodTable;

        private unsafe NativeHeap(UnmanagedHeapDescriptor* flags, HeapMethods methodTable) 
            : base(flags->CreationFlags, ownsHandle: true)
        {
            //Store heap pointer
            SetHandle(flags->HeapPointer);

            //Copy method table
            MethodTable = methodTable;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override IntPtr AllocBlock(nuint elements, nuint size, bool zero) 
            => MethodTable.Alloc(handle, elements, size, zero);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override IntPtr ReAllocBlock(IntPtr block, nuint elements, nuint size, bool zero) 
            => MethodTable.Realloc(handle, block, elements, size, zero);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool FreeBlock(IntPtr block) 
            => MethodTable.Free(handle, block);

        ///<inheritdoc/>
        protected override bool ReleaseHandle()
        {
            //Destroy the heap
            bool ret = MethodTable.Destroy(handle);

            //Free the library
            MethodTable.Library.Dispose();

            //Cleanup the method table
            MethodTable = default;

            Trace.WriteLine($"Successfully destroyed user defined heap 0x{handle:x}");

            return ret;
        }

        /*
         * Delegate methods match the native header impl for unmanaged heaps
         */

        [SafeMethodName(CREATE_METHOD_NAME)]
        unsafe delegate ERRNO CreateHeapDelegate(UnmanagedHeapDescriptor* createFlags);

        [SafeMethodName(ALLOCATE_METHOD_NAME)]
        delegate IntPtr AllocDelegate(IntPtr handle, nuint elements, nuint alignment, [MarshalAs(UnmanagedType.Bool)] bool zero);

        [SafeMethodName(REALLOC_METHOD_NAME)]
        delegate IntPtr ReallocDelegate(IntPtr heap, IntPtr block, nuint elements, nuint alignment, [MarshalAs(UnmanagedType.Bool)] bool zero);

        [SafeMethodName(FREE_METHOD_NAME)]
        delegate ERRNO FreeDelegate(IntPtr heap, IntPtr block);

        [SafeMethodName(DESTROY_METHOD_NAME)]
        delegate ERRNO DestroyHeapDelegate(IntPtr heap);

        [StructLayout(LayoutKind.Sequential)]
        struct UnmanagedHeapDescriptor
        {
            public IntPtr HeapPointer;

            public ERRNO Flags;

            public HeapCreation CreationFlags;
        }

        readonly record struct HeapMethods(Owned<SafeLibraryHandle> Library)
        {
            public readonly AllocDelegate Alloc = Library.Value.DangerousGetFunction<AllocDelegate>();
            public readonly ReallocDelegate Realloc = Library.Value.DangerousGetFunction<ReallocDelegate>();
            public readonly FreeDelegate Free = Library.Value.DangerousGetFunction<FreeDelegate>();
            public readonly DestroyHeapDelegate Destroy = Library.Value.DangerousGetFunction<DestroyHeapDelegate>();
        }
    }
}