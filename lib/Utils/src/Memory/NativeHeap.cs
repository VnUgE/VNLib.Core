/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Native;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// <para>
    /// Allows for exposing a dynamically loaded native heap implementation.
    /// </para>
    /// </summary>
    public class NativeHeap : UnmanagedHeapBase
    {
        public const string CREATE_METHOD_NAME = "heapCreate";
        public const string ALLOCATE_METHOD_NAME = "heapAlloc";
        public const string REALLOC_METHOD_NAME = "heapRealloc";
        public const string FREE_METHOD_NAME = "heapFree";
        public const string DESTROY_METHOD_NAME = "heapDestroy";

        /// <summary>
        /// <para>
        /// Loads an unmanaged heap at runtime, into the current process at the given path. The dll must conform
        /// to the unmanaged heap format. After the method table is loaded, the heapCreate method is called to 
        /// initialze the heap.
        /// </para>
        /// </summary>
        /// <param name="dllPath">The path to the heap's dll file to load into the process.</param>
        /// <param name="searchPath">The native library search path</param>
        /// <param name="creationFlags">Specifes the creation flags to pass to the heap creaetion method</param>
        /// <param name="flags">Generic flags passed directly to the heap creation method</param>
        /// <returns>The newly initialized <see cref="NativeHeap"/></returns>
        public unsafe static NativeHeap LoadHeap(string dllPath, DllImportSearchPath searchPath, HeapCreation creationFlags, ERRNO flags)
        {
            //Create a flags structure
            UnmanagedHeapDescriptor hf;
            UnmanagedHeapDescriptor* hFlags = &hf;

            //Set defaults
            hFlags->Flags = flags;
            hFlags->InternalFlags = creationFlags;
            hFlags->HeapPointer = IntPtr.Zero;

            //Create the heap
            return LoadHeapCore(dllPath, searchPath, hFlags);
        }

        private unsafe static NativeHeap LoadHeapCore(string path, DllImportSearchPath searchPath, UnmanagedHeapDescriptor* flags)
        {
            //Try to load the library
            SafeLibraryHandle library = SafeLibraryHandle.LoadLibrary(path, searchPath);
            try
            {
                //Open method table
                HeapMethods table = new()
                {
                    //Get method delegates
                    Alloc = library.DangerousGetMethod<AllocDelegate>(ALLOCATE_METHOD_NAME),

                    Destroy = library.DangerousGetMethod<DestroyHeapDelegate>(DESTROY_METHOD_NAME),

                    Free = library.DangerousGetMethod<FreeDelegate>(FREE_METHOD_NAME),

                    Realloc = library.DangerousGetMethod<ReallocDelegate>(REALLOC_METHOD_NAME),

                    Library = library
                };

                //Get the create method
                CreateHeapDelegate create = library.DangerousGetMethod<CreateHeapDelegate>(CREATE_METHOD_NAME);               

                //Create the new heap
                bool success = create(flags);
                
                if (!success)
                {
                    throw new NativeMemoryException("Failed to create the new heap, the heap create method returned a null pointer");
                }

                //Return the neap heap
                return new(flags, table);
            }
            catch
            {
                //Cleanup
                library.Dispose();
                throw;
            }
        }
       
        private HeapMethods MethodTable;

        private unsafe NativeHeap(UnmanagedHeapDescriptor* flags, HeapMethods methodTable) :base(flags->InternalFlags, true)
        {
            //Store heap pointer
            SetHandle(flags->HeapPointer);

            //Copy method table
            MethodTable = methodTable;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override IntPtr AllocBlock(nuint elements, nuint size, bool zero) => MethodTable.Alloc(handle, elements, size, zero);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override IntPtr ReAllocBlock(IntPtr block, nuint elements, nuint size, bool zero) => MethodTable.Realloc(handle, block, elements, size, zero);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool FreeBlock(IntPtr block) => MethodTable.Free(handle, block);

        ///<inheritdoc/>
        protected override bool ReleaseHandle()
        {
            //Destroy the heap
            bool ret = MethodTable.Destroy(handle);

            //Free the library
            MethodTable.Library.Dispose();

            //Cleanup the method table
            MethodTable = default;

            return ret;
        }

        /*
         * Delegate methods match the native header impl for unmanaged heaps
         */

        unsafe delegate ERRNO CreateHeapDelegate(UnmanagedHeapDescriptor* createFlags);

        delegate IntPtr AllocDelegate(IntPtr handle, nuint elements, nuint alignment, [MarshalAs(UnmanagedType.Bool)] bool zero);

        delegate IntPtr ReallocDelegate(IntPtr heap, IntPtr block, nuint elements, nuint alignment, [MarshalAs(UnmanagedType.Bool)] bool zero);

        delegate ERRNO FreeDelegate(IntPtr heap, IntPtr block);

        delegate ERRNO DestroyHeapDelegate(IntPtr heap);

        [StructLayout(LayoutKind.Sequential)]
        record struct UnmanagedHeapDescriptor
        {
            public IntPtr HeapPointer;

            public HeapCreation InternalFlags;

            public ERRNO Flags;
        }

        readonly record struct HeapMethods
        {
            public readonly SafeLibraryHandle Library { get; init; }

            public readonly AllocDelegate Alloc { get; init; }

            public readonly ReallocDelegate Realloc { get; init; }

            public readonly FreeDelegate Free { get; init; }

            public readonly DestroyHeapDelegate Destroy { get; init; }
        }
    }
}