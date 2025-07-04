﻿/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: MemoryUtil.cs 
*
* MemoryUtil.cs is part of VNLib.Utils which is part of the larger 
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
using System.Security;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

using VNLib.Utils.Memory.Diagnostics;
using VNLib.Utils.Resources;

namespace VNLib.Utils.Memory
{

    /// <summary>
    /// Provides optimized cross-platform maanged/umanaged safe/unsafe memory operations
    /// </summary>
    [SecurityCritical]
    [ComVisible(false)]
    public static unsafe partial class MemoryUtil
    {
        /// <summary>
        /// The environment variable name used to specify the shared heap type
        /// to create
        /// </summary>
        public const string SHARED_HEAP_FILE_PATH = "VNLIB_SHARED_HEAP_FILE_PATH";       

        /// <summary>
        /// When creating a heap that accepts an initial size, this value is passed
        /// to it, otherwise no initial heap size is set.
        /// </summary>
        public const string SHARED_HEAP_INTIAL_SIZE_ENV = "VNLIB_SHARED_HEAP_SIZE";

        /// <summary>
        /// The environment variable name used to enable share heap diagnostics
        /// </summary>
        public const string SHARED_HEAP_ENABLE_DIAGNOISTICS_ENV = "VNLIB_SHARED_HEAP_DIAGNOSTICS";

        /// <summary>
        /// The environment variable name used to specify the raw flags to pass to the shared heap
        /// creation method
        /// </summary>
        public const string SHARED_HEAP_RAW_FLAGS = "VNLIB_SHARED_HEAP_RAW_FLAGS";

        /// <summary>
        /// The environment variable name used to specify the shared heap type
        /// </summary>
        public const string SHARED_HEAP_GLOBAL_ZERO = "VNLIB_SHARED_HEAP_GLOBAL_ZERO";

        /// <summary>
        /// Initial shared heap size (bytes)
        /// </summary>
        public const nuint SHARED_HEAP_INIT_SIZE = 20971520;

        /// <summary>
        /// A central constant to specify the maximum buffer size for buffer operations
        /// within the Utils library. 
        /// </summary>
        public const int MAX_BUF_SIZE = 2097152;
        
        /// <summary>
        /// Also a central constant to specify the minimum buffer size for buffer operations
        /// within the Utils library.
        /// </summary>
        public const int MIN_BUF_SIZE = 16000;
        
        /// <summary>
        /// The maximum buffer size requested by <see cref="UnsafeAlloc{T}(int, bool)"/>
        /// that will use the array pool before falling back to the <see cref="Shared"/>.
        /// heap.
        /// </summary>
        /// <remarks>
        /// This value is chosen to be just under the size the CLR will promote an array to the 
        /// LOH, we can assume any heap impl will have long-term performance than the LOH for
        /// large allocations.
        /// </remarks>
        public const int MAX_UNSAFE_POOL_SIZE = 80 * 1024;

        //Cache the system page size
        private static readonly int SystemPageSize = Environment.SystemPageSize;

        /// <summary>
        /// Provides a shared heap instance for the process to allocate memory from.
        /// </summary>
        /// <remarks>
        /// The backing heap
        /// is determined by the OS type and process environment varibles.
        /// </remarks>
        public static IUnmangedHeap Shared => _lazyHeap.Instance;       


        private static readonly LazyInitializer<IUnmangedHeap> _lazyHeap = new(InitSharedHeapInternal);

        //Avoiding static initializer
        private static IUnmangedHeap InitSharedHeapInternal()
        {
            //Get env for heap diag
            _ = ERRNO.TryParse(Environment.GetEnvironmentVariable(SHARED_HEAP_ENABLE_DIAGNOISTICS_ENV), out ERRNO diagEnable);
            _ = ERRNO.TryParse(Environment.GetEnvironmentVariable(SHARED_HEAP_GLOBAL_ZERO), out ERRNO globalZero);

            Trace.WriteLineIf(diagEnable, "Shared heap diagnostics enabled");
            Trace.WriteLineIf(globalZero, "Shared heap global zero enabled");

            //Init shared heap instance
            IUnmangedHeap heap = InitHeapInternal(
                //Supply suggested arguments to the heap library, it can clear or set them as needed
                HeapCreation.UseSynchronization | HeapCreation.SupportsRealloc | HeapCreation.Shared,
                globalZero
            );

            //Register domain unload event
            AppDomain.CurrentDomain.DomainUnload += (_, _) => heap.Dispose();

            // Enable diagnostics if requested
            return diagEnable ? new TrackedHeapWrapper(heap, true) : heap;
        }

        /// <summary>
        /// Gets the shared heap statistics if stats are enabled
        /// </summary>
        /// <returns>
        /// The <see cref="HeapStatistics"/> of the shared heap, or an empty 
        /// <see cref="HeapStatistics"/> if diagnostics are not enabled.
        /// </returns>
        public static HeapStatistics GetSharedHeapStats()
        {
            /*
             * If heap is allocated and the heap type is a tracked heap, 
             * get the heap's stats, otherwise return an empty handle
             */
            return _lazyHeap.IsLoaded && _lazyHeap.Instance is TrackedHeapWrapper h
                ? h.GetCurrentStats() 
                : default;
        }

        /// <summary>
        /// Initializes a new <see cref="IUnmangedHeap"/> determined by compilation/runtime flags
        /// and operating system type for the current proccess.
        /// </summary>
        /// <param name="globalZero">If true, sets the <see cref="HeapCreation.GlobalZero"/> flag</param>
        /// <returns>An <see cref="IUnmangedHeap"/> for the current process</returns>
        /// <exception cref="SystemException"></exception>
        /// <exception cref="DllNotFoundException"></exception>
        public static IUnmangedHeap InitializeNewHeapForProcess(bool globalZero = false) 
            => InitHeapInternal(
                // Set default flags, the heap lib can clear or set them as needed. Default is a private heap.
                HeapCreation.UseSynchronization | HeapCreation.SupportsRealloc,
                globalZero
            );

        private static IUnmangedHeap InitHeapInternal(HeapCreation defaultFlags, bool globalZero)
        {
            //Get environment varable
            string? heapDllPath = Environment.GetEnvironmentVariable(SHARED_HEAP_FILE_PATH);
            string? rawFlagsEnv = Environment.GetEnvironmentVariable(SHARED_HEAP_RAW_FLAGS);
        
            //Set global zero flag if requested
            defaultFlags |= globalZero ? HeapCreation.GlobalZero : HeapCreation.None;

            ERRNO userFlags = 0;

            //Try to parse the raw flags to pass to the heap
            if (nint.TryParse(rawFlagsEnv, NumberStyles.HexNumber, null, out nint result))
            {
                userFlags = new(result);
            }

            //Check for heap api dll
            if (!string.IsNullOrWhiteSpace(heapDllPath))
            {
                //Attempt to load the heap
                return NativeHeap.LoadHeap(heapDllPath, DllImportSearchPath.SafeDirectories, defaultFlags, userFlags);
            }
            //No user heap was specified, use fallback on windows
            else if (OperatingSystem.IsWindows())
            {
                //We can use win32 heaps
              
                //Get inital size
                string? sharedSize = Environment.GetEnvironmentVariable(SHARED_HEAP_INTIAL_SIZE_ENV);

                //Try to parse the shared size from the env
                if (!nuint.TryParse(sharedSize, out nuint defaultSize))
                {
                    defaultSize = SHARED_HEAP_INIT_SIZE;
                }

                //Create win32 private heap
                return Win32PrivateHeap.Create(defaultSize, defaultFlags, flags:userFlags); 
            }
            else
            {
                //Finally fallback to .NET native mem impl 
                return new ProcessHeap(defaultFlags);
            }
        }

        /// <summary>
        /// Gets a value that indicates if the use defined a custom heap
        /// implementation
        /// </summary>
        public static bool IsUserDefinedHeap { get; } = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SHARED_HEAP_FILE_PATH));

        #region Zero

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void ZeroByRef<T>(ref T src, uint elements)
        {
            Debug.Assert(Unsafe.IsNullRef(ref src) == false, "Null reference passed to ZeroByRef");

            //Call init block on bytes
            Unsafe.InitBlock(
                startAddress: ref Refs.AsByte(ref src, 0),
                value: 0,
                byteCount: ByteCount<T>(elements)
            );
        }

        /// <summary>
        /// Zeros a block of memory of umanged type.  If Windows is detected at runtime, calls RtlSecureZeroMemory Win32 function
        /// </summary>
        /// <typeparam name="T">Unmanged datatype</typeparam>
        /// <param name="block">Block of memory to be cleared</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void UnsafeZeroMemory<T>(ReadOnlySpan<T> block) where T : struct
        {
            if (block.IsEmpty)
            {
                return;
            }

            ZeroByRef(
               src: ref MemoryMarshal.GetReference(block),  //Get typed reference
               elements: (uint)block.Length  //block must be a positive value
           );
        }

        /// <summary>
        /// Zeros a block of memory of umanged type.  If Windows is detected at runtime, calls RtlSecureZeroMemory Win32 function
        /// </summary>
        /// <typeparam name="T">Unmanged datatype</typeparam>
        /// <param name="block">Block of memory to be cleared</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void UnsafeZeroMemory<T>(ReadOnlyMemory<T> block) where T : struct
        {
            if (block.IsEmpty)
            {
                return;
            }

            //Pin memory and get pointer
            using MemoryHandle handle = block.Pin();

            //Calls memset
            Unsafe.InitBlock(
                startAddress: handle.Pointer, 
                value: 0, 
                byteCount: ByteCount<T>((uint)block.Length)
            );
        }
      
        /*
         * Initializing a non-readonly span/memory as of .NET 6.0 is a reference 
         * reintpretation, essentially a pointer cast, so there is little/no cost 
         * to implicitely casting to a readonly span/memory types to reduce complexity
         */

        /// <summary>
        /// Initializes a block of memory with zeros 
        /// </summary>
        /// <typeparam name="T">The unmanaged</typeparam>
        /// <param name="block">The block of memory to initialize</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(Span<T> block) where T : struct 
            => UnsafeZeroMemory<T>(block);

        /// <summary>
        /// Initializes a block of memory with zeros 
        /// </summary>
        /// <typeparam name="T">The unmanaged</typeparam>
        /// <param name="block">The block of memory to initialize</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(Memory<T> block) where T : struct 
            => UnsafeZeroMemory<T>(block);

        /// <summary>
        /// Initializes the entire array with zeros 
        /// </summary>
        /// <typeparam name="T">A structure type to initialize</typeparam>
        /// <param name="array">The array to zero</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(T[] array) where T : struct
        {
            ArgumentNullException.ThrowIfNull(array);
            InitializeBlock(array, (uint)array.Length);
        }

        /// <summary>
        /// Initializes the array with zeros up to the specified count
        /// </summary>
        /// <typeparam name="T">A structure type to initialize</typeparam>
        /// <param name="array">The array to zero</param>
        /// <param name="count">The number of elements in the array to zero</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(T[] array, uint count) where T: struct
        {
            ArgumentNullException.ThrowIfNull(array);

            //Check bounds
            CheckBounds(array, 0, count);

            //Get array data reference
            ZeroByRef(
                ref MemoryMarshal.GetArrayDataReference(array), 
                count
            );
        }

        /// <summary>
        /// Zeroes a block of memory of the given unmanaged type
        /// </summary>
        /// <typeparam name="T">The unmanaged type to zero</typeparam>
        /// <param name="block">A pointer to the block of memory to zero</param>
        /// <param name="itemCount">The number of elements in the block to zero</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void InitializeBlock<T>(ref T block, int itemCount) where T : struct
        {
            if (itemCount <= 0)
            {
                return;
            }

            InitializeBlock(ref block, (uint)itemCount);
        }

        /// <summary>
        /// Zeroes a block of memory of the given unmanaged type
        /// </summary>
        /// <typeparam name="T">The unmanaged type to zero</typeparam>
        /// <param name="block">A pointer to the block of memory to zero</param>
        /// <param name="itemCount">The number of elements in the block to zero</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void InitializeBlock<T>(ref T block, uint itemCount) where T : struct
        {
            ThrowIfNullRef(in block, nameof(block));

            if (itemCount == 0)
            {
                return;
            }

            ZeroByRef(ref block, itemCount);
        }

        /// <summary>
        /// Zeroes a block of memory of the given unmanaged type
        /// </summary>
        /// <typeparam name="T">The unmanaged type to zero</typeparam>
        /// <param name="block">A pointer to the block of memory to zero</param>
        /// <param name="itemCount">The number of elements in the block to zero</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void InitializeBlock<T>(T* block, int itemCount) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(block);

            InitializeBlock(ref *block, itemCount);
        }

        /// <summary>
        /// Zeroes a block of memory of the given unmanaged type
        /// </summary>
        /// <typeparam name="T">The unmanaged type to zero</typeparam>
        /// <param name="block">A pointer to the block of memory to zero</param>
        /// <param name="itemCount">The number of elements in the block to zero</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(IntPtr block, int itemCount) where T : unmanaged 
            => InitializeBlock((T*)block, itemCount);

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="structRef">The reference to the allocated structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroStruct<T>(ref T structRef) where T : unmanaged
        {
            ThrowIfNullRef(ref structRef, nameof(structRef));

            //Get a byte reference to the structure
            ref byte byteRef = ref Unsafe.As<T, byte>(ref structRef);

            Unsafe.InitBlockUnaligned(ref byteRef, 0, (uint)sizeof(T));
        }

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="structPtr">The pointer to the allocated structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroStruct<T>(T* structPtr) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(structPtr);
            ZeroStruct(ref Unsafe.AsRef<T>(structPtr));
        }

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="structPtr">The pointer to the allocated structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroStruct<T>(void* structPtr) where T: unmanaged 
            => ZeroStruct((T*)structPtr);

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="block">The pointer to the allocated structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroStruct<T>(IntPtr block) where T : unmanaged 
            => ZeroStruct<T>(block.ToPointer());


        #endregion

        #region Copy

        /// <summary>
        /// Copies structure data from a source byte reference that points to a sequence of 
        /// of data to the target structure reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">An initialized target structure to copy data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void CopyStruct<T>(ref readonly byte source, ref T target) where T : unmanaged
        {
            ThrowIfNullRef(in source, nameof(target));
            ThrowIfNullRef(ref target, nameof(target));

            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref target),    //Recover byte reference of target struct
                in source,
                byteCount: ByteCount<T>(1u)
            );
        }

        /// <summary>
        /// Copies the memory of the structure pointed to by the source reference to the target 
        /// reference data sequence
        /// </summary>
        /// <remarks>
        /// Warning: This is a low level api that cannot do bounds checking on the target sequence. It must 
        /// be large enough to hold the structure data.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">A reference to the first byte of the memory location to copy the struct data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void CopyStruct<T>(scoped ref readonly T source, ref byte target) where T : unmanaged
        {
            ThrowIfNullRef(in source, nameof(source));
            ThrowIfNullRef(in target, nameof(target));

            //Memmove
            Unsafe.CopyBlockUnaligned(
                destination: ref target, 
                source: in Unsafe.As<T, byte>(ref Unsafe.AsRef(in source)),     //Recover byte reference to struct
                byteCount: ByteCount<T>(1u)
            );
        }


        /// <summary>
        /// Copies structure data from a source byte reference that points to a sequence of 
        /// of data to the target structure reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">A pointer to initialized target structure to copy data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(ref readonly byte source, T* target) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(target);
            CopyStruct(in source, ref *target);
        }

        /// <summary>
        /// Copies structure data from a source byte reference that points to a sequence of 
        /// of data to the target structure reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">A pointer to initialized target structure to copy data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(ref readonly byte source, void* target) where T : unmanaged 
            => CopyStruct(in source, (T*)target);

        /// <summary>
        /// Copies structure data from a source sequence of data to the target structure reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceData">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">A pointer to initialized target structure to copy data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(ReadOnlySpan<byte> sourceData, ref T target) where T : unmanaged
        {
            if (sourceData.Length < sizeof(T))
            {
                throw new ArgumentException("Source data is smaller than the size of the structure");
            }

            CopyStruct(ref MemoryMarshal.GetReference(sourceData), ref target);
        }

        /// <summary>
        /// Copies structure data from a source sequence of data to the target structure reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceData">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">A pointer to initialized target structure to copy data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(ReadOnlySpan<byte> sourceData, T* target) where T : unmanaged 
            => CopyStruct(sourceData, ref *target);

        /// <summary>
        /// Copies structure data from a source sequence of data to the target structure reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceData">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">A pointer to initialized target structure to copy data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(ReadOnlySpan<byte> sourceData, void* target) where T: unmanaged 
            => CopyStruct(sourceData, (T*)target);


        /// <summary>
        /// Copies the memory of the structure pointed to by the source pointer to the target 
        /// reference data sequence
        /// </summary>
        /// <remarks>
        /// Warning: This is a low level api that cannot do bounds checking on the target sequence. It must 
        /// be large enough to hold the structure data.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A pointer to the first byte of source data to copy from</param>
        /// <param name="target">A reference to the first byte of the memory location to copy the struct data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(T* source, ref byte target) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source);
            CopyStruct(ref *source, ref target);
        }

        /// <summary>
        /// Copies the memory of the structure pointed to by the source pointer to the target 
        /// reference data sequence
        /// </summary>
        /// <remarks>
        /// Warning: This is a low level api that cannot do bounds checking on the target sequence. It must 
        /// be large enough to hold the structure data.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A pointer to the first byte of source data to copy from</param>
        /// <param name="target">A reference to the first byte of the memory location to copy the struct data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(void* source, ref byte target) where T : unmanaged 
            => CopyStruct((T*)source, ref target);

        /// <summary>
        /// Copies the memory of the structure pointed to by the source pointer to the target 
        /// reference data sequence
        /// </summary>
        /// <remarks>
        /// Warning: This is a low level api that cannot do bounds checking on the target sequence. It must 
        /// be large enough to hold the structure data.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A pointer to the first byte of source data to copy from</param>
        /// <param name="target">A reference to the first byte of the memory location to copy the struct data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(scoped ref readonly T source, Span<byte> target) where T : unmanaged
        {
            //check that the span is large enough to hold the structure
            ArgumentOutOfRangeException.ThrowIfLessThan(target.Length, sizeof(T), nameof(target));

            CopyStruct(
                in source, 
                ref Refs.AsByte(target, 0)
            );
        }

        /// <summary>
        /// Copies the memory of the structure pointed to by the source pointer to the target 
        /// reference data sequence
        /// </summary>
        /// <remarks>
        /// Warning: This is a low level api that cannot do bounds checking on the target sequence. It must 
        /// be large enough to hold the structure data.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A pointer to the first byte of source data to copy from</param>
        /// <param name="target">A reference to the first byte of the memory location to copy the struct data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(T* source, Span<byte> target) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentOutOfRangeException.ThrowIfLessThan(target.Length, sizeof(T), nameof(target));
            
            CopyStruct(
                ref Unsafe.AsRef<T>(source), 
                target
            );
        }

        /// <summary>
        /// Copies the memory of the structure pointed to by the source pointer to the target 
        /// reference data sequence
        /// </summary>
        /// <remarks>
        /// Warning: This is a low level api that cannot do bounds checking on the target sequence. It must 
        /// be large enough to hold the structure data.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A pointer to the first byte of source data to copy from</param>
        /// <param name="target">A reference to the first byte of the memory location to copy the struct data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(void* source, Span<byte> target) where T : unmanaged 
            => CopyStruct((T*)source, target);

        /// <summary>
        /// Copies the memory of the structure pointed to by the source pointer to the target 
        /// reference data sequence
        /// </summary>
        /// <remarks>
        /// Warning: This is a low level api that cannot do bounds checking on the target sequence. It must 
        /// be large enough to hold the structure data.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A pointer to the first byte of source data to copy from</param>
        /// <param name="target">A reference to the first byte of the memory location to copy the struct data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(IntPtr source, ref byte target) where T : unmanaged 
            => CopyStruct(ref GetRef<T>(source), ref target);
        

        /// <summary>
        /// Copies the memory of the structure pointed to by the source reference to the target
        /// structure reference
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A reference to the source structure to copy from</param>
        /// <param name="target">A reference to the target structure to copy to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloneStruct<T>(scoped ref readonly T source, ref T target) where T : unmanaged
        {
            ThrowIfNullRef(in source, nameof(source));
            ThrowIfNullRef(ref target, nameof(target));

            Unsafe.CopyBlockUnaligned(
                destination: ref Refs.AsByte(ref target, 0), 
                source: in Refs.AsByteR(in source, 0), 
                byteCount: ByteCount<T>(1u)
            );
        }

        /// <summary>
        /// Copies the memory of the structure pointed to by the source pointer to the target
        /// structure pointer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A pointer to the source structure to copy from</param>
        /// <param name="target">A pointer to the target structure to copy to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloneStruct<T>(T* source, T* target) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);

            Unsafe.CopyBlockUnaligned(
                destination: target, 
                source, 
                byteCount: ByteCount<T>(1u)
            );
        }


        /// <summary>
        /// Copies data from source memory to destination memory of an umanged data type
        /// </summary>
        /// <typeparam name="T">Unmanged type</typeparam>
        /// <param name="source">Source data <see cref="ReadOnlySpan{T}"/></param>
        /// <param name="dest">Destination <see cref="MemoryHandle{T}"/></param>
        /// <param name="count">Number of elements to copy</param>
        /// <param name="sourceOffset">Source offset</param>
        /// <param name="destOffset">Dest offset</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(ReadOnlySpan<T> source, int sourceOffset, IMemoryHandle<T> dest, nuint destOffset, int count)
            where T : struct
        {
            ArgumentNullException.ThrowIfNull(dest);

            if (count == 0)
            {
                return;
            }

            //Check bounds (will verify that count is a positive integer)
            CheckBounds(source, sourceOffset, count);
            CheckBounds(dest, destOffset, (uint)count);

            //Use memmove by ref
            CopyUtilCore.Memmove(
                srcByte: ref Refs.AsByte(source, (nuint)sourceOffset),
                dstByte: ref Refs.AsByte(dest, destOffset),
                byteCount: ByteCount<T>((uint)count),
                forceAcceleration: false
            );
        }

        /// <summary>
        /// Copies data from source memory to destination memory of an umanged data type
        /// </summary>
        /// <typeparam name="T">Unmanged type</typeparam>
        /// <param name="source">Source data <see cref="ReadOnlyMemory{T}"/></param>
        /// <param name="sourceOffset">The element offset in the source memory</param>
        /// <param name="dest">Destination <see cref="IMemoryHandle{T}"/></param>
        /// <param name="destOffset">Dest element offset</param>
        /// <param name="count">The number of elements to copy</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(ReadOnlyMemory<T> source, int sourceOffset, IMemoryHandle<T> dest, nuint destOffset, int count) 
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(dest);

            //Dest offset will never be negative so defer that to the validation stage
            ValidateCopyArgs(sourceOffset, 0, count);

            if (count == 0)
            {
                return;
            }

            //Create copy handles
            RMemCopyHandle<T> src = new(source, (nuint)sourceOffset);
            MemhandleCopyHandle<T> dst = new(dest, destOffset);

            MemmoveInternal<T, RMemCopyHandle<T>, MemhandleCopyHandle<T>>(in src, in dst, (nuint)count, forceAcceleration: false);
        }

        /// <summary>
        /// Copies data from source memory to destination memory of an umanged data type
        /// </summary>
        /// <typeparam name="T">Unmanged type</typeparam>
        /// <param name="source">Source data <see cref="MemoryHandle{T}"/></param>
        /// <param name="sourceOffset">Number of elements to offset source data</param>
        /// <param name="dest">Destination <see cref="Span{T}"/></param>
        /// <param name="destOffset">Dest offset</param>
        /// <param name="count">Number of elements to copy</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(IMemoryHandle<T> source, nint sourceOffset, Span<T> dest, int destOffset, int count) where T : struct
        {
            ArgumentNullException.ThrowIfNull(source);

            //Validate source/dest/count
            ValidateCopyArgs(sourceOffset, destOffset, count);

            //Check count last for debug reasons
            if (count == 0)
            {
                return;
            }

            //Check source bounds
            CheckBounds(source, (nuint)sourceOffset, (nuint)count);
            CheckBounds(dest, destOffset, count);
            
            //Use memmove by ref
            CopyUtilCore.Memmove(
                srcByte: ref Refs.AsByte(source, (nuint)sourceOffset), 
                dstByte: ref Refs.AsByte(dest, (nuint)destOffset),
                byteCount: ByteCount<T>((uint)count),
                forceAcceleration: false
            );
        }

        /// <summary>
        /// Copies data from source memory to destination memory of an umanged data type
        /// </summary>
        /// <typeparam name="T">Unmanged type</typeparam>
        /// <param name="source">Source data <see cref="MemoryHandle{T}"/></param>
        /// <param name="sourceOffset">Number of elements to offset source data</param>
        /// <param name="dest">Destination <see cref="Memory{T}"/></param>
        /// <param name="destOffset">Dest offset</param>
        /// <param name="count">Number of elements to copy</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(IMemoryHandle<T> source, nint sourceOffset, Memory<T> dest, int destOffset, int count) 
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source);

            //Validate source/dest/count
            ValidateCopyArgs(sourceOffset, destOffset, count);

            //Check count last for debug reasons
            if (count == 0)
            {
                return;
            }

            //Create copy handles
            MemhandleCopyHandle<T> src = new(source, (nuint)sourceOffset);
            WMemCopyHandle<T> dst = new(dest, (nuint)destOffset);

            MemmoveInternal<T, MemhandleCopyHandle<T>, WMemCopyHandle<T>>(in src, in dst, (nuint)count, forceAcceleration: false);
        }

        /// <summary>
        /// Copies data from source memory to destination memory of an umanged data type 
        /// using references for blocks smaller than <see cref="UInt32.MaxValue"/> and 
        /// pinning for larger blocks
        /// </summary>
        /// <typeparam name="T">Unmanged type</typeparam>
        /// <param name="source">Source data <see cref="MemoryHandle{T}"/></param>
        /// <param name="sourceOffset">Number of elements to offset source data</param>
        /// <param name="dest">Destination <see cref="Memory{T}"/></param>
        /// <param name="destOffset">Dest offset</param>
        /// <param name="count">Number of elements to copy</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(IMemoryHandle<T> source, nuint sourceOffset, IMemoryHandle<T> dest, nuint destOffset, nuint count) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(dest);

            //Check count last for debug reasons
            if (count == 0)
            {
                return;
            }

            MemhandleCopyHandle<T> src = new(source, sourceOffset);
            MemhandleCopyHandle<T> dst = new(dest, destOffset);

            MemmoveInternal<T, MemhandleCopyHandle<T>, MemhandleCopyHandle<T>>(in src, in dst, count, forceAcceleration: false);
        }

        /// <summary>
        /// Performs a fast reference based copy on very large blocks of memory 
        /// using pinning and pointers only when the number of bytes to copy is 
        /// larger than <see cref="UInt32.MaxValue"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source memory handle to copy data from</param>
        /// <param name="sourceOffset">The element offset to begin reading from</param>
        /// <param name="dest">The destination array to write data to</param>
        /// <param name="destOffset">The destination element offset</param>
        /// <param name="count">The number of elements to copy</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void CopyArray<T>(IMemoryHandle<T> source, nuint sourceOffset, T[] dest, nuint destOffset, nuint count) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(dest);

            if (count == 0)
            {
                return;
            }

            MemhandleCopyHandle<T> src = new(source, sourceOffset);
            ArrayCopyHandle<T> dst = new(dest, destOffset);

            MemmoveInternal<T, MemhandleCopyHandle<T>, ArrayCopyHandle<T>>(in src, in dst, count, forceAcceleration: false);
        }

        /// <summary>
        /// Performs a fast reference based copy on very large blocks of memory 
        /// using pinning and pointers only when the number of bytes to copy is 
        /// larger than <see cref="UInt32.MaxValue"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source memory handle to copy data from</param>
        /// <param name="sourceOffset">The element offset to begin reading from</param>
        /// <param name="dest">The destination array to write data to</param>
        /// <param name="destOffset">The detination element offset</param>
        /// <param name="count">The number of elements to copy</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void CopyArray<T>(T[] source, nuint sourceOffset, IMemoryHandle<T> dest, nuint destOffset, nuint count) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(dest);

            if (count == 0)
            {
                return;
            }

            ArrayCopyHandle<T> ach = new(source, sourceOffset);
            MemhandleCopyHandle<T> mch = new(dest, destOffset);

            MemmoveInternal<T, ArrayCopyHandle<T>, MemhandleCopyHandle<T>>(in ach, in mch, count, forceAcceleration: false);
        }

        /// <summary>
        /// Copies data from one managed array to another using the fastest method 
        /// available and hardware acceleration if supported.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source memory handle to copy data from</param>
        /// <param name="sourceOffset">The element offset to begin reading from</param>
        /// <param name="dest">The destination array to write data to</param>
        /// <param name="destOffset">The detination element offset</param>
        /// <param name="count">The number of elements to copy</param>
        public static void CopyArray<T>(T[] source, nuint sourceOffset, T[] dest, nuint destOffset, nuint count)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(dest);

            //Check count last for debug reasons
            if (count == 0)
            {
                return;
            }

            //Init copy handles and call memmove
            ArrayCopyHandle<T> srcH = new(source, sourceOffset);
            ArrayCopyHandle<T> dstH = new(dest, destOffset);

            MemmoveInternal<T, ArrayCopyHandle<T>, ArrayCopyHandle<T>>(in srcH, in dstH, count, forceAcceleration: false);
        }

        /// <summary>
        /// Optimized memmove for known small memory blocks. This method is faster than
        /// <see cref="Memmove{T}(ref readonly T, nuint, ref T, nuint, nuint)"/> when the 
        /// number of elements to copy is known to be small. Pointers to src and dst may be 
        /// overlapping regions of memory.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="src">A readonly reference to the first element in the source memory sequence</param>
        /// <param name="srcOffset">The number of elements to offset the source sequence by</param>
        /// <param name="dst">A reference to the first element in the target memory sequence</param>
        /// <param name="dstOffset">The number of elements to offset the destination pointer by</param>
        /// <param name="elementCount">The number of elements to copy from source to destination memory</param>
        /// <remarks>
        /// WARNING: This is a low level api that cannot do bounds checking when using references. Be sure you
        /// know what you are doing!
        /// </remarks>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SmallMemmove<T>(ref readonly T src, nuint srcOffset, ref T dst, nuint dstOffset, ushort elementCount)
            where T: struct
        {
            ThrowIfNullRef(in src, nameof(src));
            ThrowIfNullRef(in dst, nameof(dst));

            if (elementCount == 0)
            {
                return;
            }

            //Keep all core memory related optimizations to the core class
            CopyUtilCore.SmallMemmove(
                srcByte: in Refs.AsByteR(in src, srcOffset),
                dstByte: ref Refs.AsByte(ref dst, dstOffset),
                byteCount: (uint)ByteCount<T>(elementCount)
            );
        }

        /// <summary>
        /// Low level api for copying data from source memory to destination memory of an
        /// umanged data type. Pointers to src and dst may be overlapping regions of memory.
        /// </summary>
        /// <remarks>
        /// WARNING: It's not possible to do bounds checking when using references. Be sure you 
        /// know what you are doing!
        /// </remarks>
        /// <typeparam name="T">The unmanaged or structure type to copy</typeparam>
        /// <param name="src">A reference to the source data to copy from</param>
        /// <param name="srcOffset">The offset (in elements) from the reference to begin the copy from</param>
        /// <param name="dst">The detination</param>
        /// <param name="dstOffset"></param>
        /// <param name="elementCount"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Memmove<T>(ref readonly T src, nuint srcOffset, ref T dst, nuint dstOffset, nuint elementCount) where T : struct
        {
            ThrowIfNullRef(in src, nameof(src));
            ThrowIfNullRef(in dst, nameof(dst));

            if (elementCount == 0)
            {
                return;
            }

            CopyUtilCore.Memmove(
                srcByte: in Refs.AsByteR(in src, srcOffset),
                dstByte: ref Refs.AsByte(ref dst, dstOffset),
                byteCount: ByteCount<T>(elementCount),
                forceAcceleration: false
            );
        }

        /// <summary>
        /// Low level api for copying data from source memory to destination memory of an
        /// umanged data type. This call attempts to force hadrware acceleration if supported.
        /// Pointers to src and dst may be overlapping regions of memory.
        /// <para>
        /// Understand that using this function attempts to force hardware acceleration, which 
        /// may hurt performance if the data is not large enough to justify the overhead.
        /// </para>
        /// <para>
        /// If the <see cref="Avx.IsSupported"/> flag is false, this function will fallback to
        /// the default method used by <see cref="Memmove{T}(ref readonly T, nuint, ref T, nuint, nuint)"/>
        /// </para>
        /// </summary>
        /// <remarks>
        /// WARNING: It's not possible to do bounds checking when using references. Be sure you 
        /// know what you are doing!
        /// </remarks>
        /// <typeparam name="T">The unmanaged or structure type to copy</typeparam>
        /// <param name="src">A reference to the source data to copy from</param>
        /// <param name="srcOffset">The offset (in elements) from the reference to begin the copy from</param>
        /// <param name="dst">The detination</param>
        /// <param name="dstOffset"></param>
        /// <param name="elementCount"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AcceleratedMemmove<T>(ref readonly T src, nuint srcOffset, ref T dst, nuint dstOffset, nuint elementCount) where T : struct
        {
            ThrowIfNullRef(in src, nameof(src));
            ThrowIfNullRef(in dst, nameof(dst));

            if(elementCount == 0)
            {
                return;
            }

            CopyUtilCore.Memmove(
                srcByte: in Refs.AsByteR(in src, srcOffset),
                dstByte: ref Refs.AsByte(ref dst, dstOffset),
                byteCount: ByteCount<T>(elementCount),
                forceAcceleration: CopyUtilCore.IsHwAccelerationSupported
            );
        }

        private static void MemmoveInternal<T, TSrc, TDst>(ref readonly TSrc src, ref readonly TDst dst, nuint elementCount, bool forceAcceleration)
            where T : unmanaged
            where TSrc : I64BitBlock
            where TDst : I64BitBlock
        {
            //Validate source/dest arguments
            src.Validate(elementCount);
            dst.Validate(elementCount);

            nuint byteCount = ByteCount<T>(elementCount);

            /*
             * The internal copy strategy may require buffers to be pinned in memory 
             * instead of references based. We can pin the handles now to use a 
             * memoryHandle pointer instead of fixing a reference. This can offer
             * better pinning performance for handles that that have zero-cost pinning
             * such as unmanaged blocks.
             */
            if (CopyUtilCore.RequiresPinning(byteCount, forceAcceleration))
            {
                //Pin before calling memmove
                using MemoryHandle srcH = src.Pin();
                using MemoryHandle dstH = dst.Pin();

                CopyUtilCore.Memmove(
                    srcByte: in Refs.AsByte<T>(srcH.Pointer, src.Offset),
                    dstByte: ref Refs.AsByte<T>(dstH.Pointer, dst.Offset),
                    byteCount,
                    forceAcceleration
                );
            }
            else
            {
                //Reference based memmove
                CopyUtilCore.Memmove(
                    srcByte: in src.GetOffsetRef(),
                    dstByte: ref dst.GetOffsetRef(),
                    byteCount,
                    forceAcceleration
                );
            }
        }

        private static void ValidateCopyArgs(nint sourceOffset, nint destOffset, nint count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
            ArgumentOutOfRangeException.ThrowIfNegative(destOffset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfNullRef<T>(ref readonly T value, string argName)
        {
            if (Unsafe.IsNullRef(in value))
            {
                throw new ArgumentNullException(argName);
            }
        }

        #endregion

        #region Mlock

        /// <summary>
        /// A value that indicates whether memory locking is supported on the current 
        /// operating system
        /// </summary>
        public static bool MemoryLockSupported { get; } = OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

        [SupportedOSPlatform("linux")]
        [LibraryImport("libc")]
        private static partial ERRNO mlock(void* addr, nuint size);

        [SupportedOSPlatform("linux")]
        [LibraryImport("libc")]
        private static partial ERRNO munlock(void* addr, nuint size);

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool VirtualLock(void* lpAddress, nuint dwSize);

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool VirtualUnlock(void* lpAddress, nuint dwSize);

        /// <summary>
        /// Locks a region of virtual memory into physical memory, preventing it from being paged to disk
        /// </summary>
        /// <param name="addr">The base address of the memory region to lock</param>
        /// <param name="size">The size in bytes of the memory region to lock</param>
        /// <returns>True if the memory was successfully locked, false otherwise</returns>
        /// <remarks>
        /// This method is only available on Linux and Windows platforms.
        /// On Linux, it calls the mlock function.
        /// On Windows, it calls the VirtualLock function.
        /// Returns false if the address is null or the size is zero.
        /// </remarks>
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public static bool LockMemory(void* addr, nuint size)
        {
            // cant lock zero bytes or null address
            if (addr == null || size == 0)
            {
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                return VirtualLock(addr, size);
            }

            if (OperatingSystem.IsLinux())
            {
                // munlock returns 0 when successful
                return mlock(addr, size) == 0;
            }

            return false;
        }

        /// <summary>
        /// Locks a region of virtual memory into physical memory, preventing it from being paged to disk
        /// </summary>
        /// <param name="addr">The base address of the memory region to lock</param>
        /// <param name="size">The size in bytes of the memory region to lock</param>
        /// <returns>True if the memory was successfully locked, false otherwise</returns>
        /// <remarks>
        /// This method is only available on Linux and Windows platforms.
        /// On Linux, it calls the mlock function.
        /// On Windows, it calls the VirtualLock function.
        /// Returns false if the address is null or the size is zero.
        /// </remarks>
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public static bool LockMemory(nint addr, nuint size) 
            => LockMemory(addr.ToPointer(), size);

        /// <summary>
        /// Locks a memory handle's memory region into physical memory, preventing it from being paged to disk
        /// </summary>
        /// <typeparam name="T">The unmanaged datatype</typeparam>
        /// <param name="handle">The memory handle to lock</param>
        /// <returns>True if the memory was successfully locked, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null</exception>
        /// <remarks>
        /// This method is only available on Linux and Windows platforms.
        /// Returns false if the handle is empty, closed, or invalid.
        /// </remarks>
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public static bool LockMemory<T>(MemoryHandle<T> handle) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(handle);

            if (handle.IsClosed || handle.IsInvalid)
            {
                return false;
            }

            return LockMemory(handle.BasePtr, handle.ByteLength);
        }

        /// <summary>
        /// Locks a memory region referenced by a MemoryHandle into physical memory, 
        /// preventing it from being paged to disk
        /// </summary>
        /// <param name="addr">A reference to the memory handle pointing to memory to lock</param>
        /// <param name="size">The size in bytes of the memory region to lock</param>
        /// <returns>True if the memory was successfully locked, false otherwise</returns>
        /// <remarks>
        /// This method is only available on Linux and Windows platforms.
        /// It extracts the pointer from the memory handle and delegates to the
        /// <see cref="LockMemory(void*, nuint)"/> method.
        /// </remarks>
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public static bool LockMemory(ref readonly MemoryHandle addr, nuint size)
            => LockMemory(addr.Pointer, size);


        /// <summary>
        /// Unlocks a previously locked region of virtual memory on the system
        /// </summary>
        /// <param name="addr">The base address of the memory region to unlock</param>
        /// <param name="size">The size in bytes of the memory region to unlock</param>
        /// <returns>True if the memory was successfully unlocked, false otherwise</returns>
        /// <remarks>
        /// This method is only available on Linux and Windows platforms.
        /// On Linux, it calls the munlock function.
        /// On Windows, it calls the VirtualUnlock function.
        /// </remarks>
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public static bool UnlockMemory(void* addr, nuint size)
        {
            // cant unlock zero bytes or null address
            if (addr == null || size == 0)
            {
                return false;
            }

            if (OperatingSystem.IsLinux())
            {
                //munlock returns 0 when successful
                return munlock(addr, size) == 0;
            }

            if (OperatingSystem.IsWindows())
            {
                return VirtualUnlock(addr, size);
            }

            return false;
        }

        /// <summary>
        /// Unlocks a previously locked region of virtual memory on the system
        /// </summary>
        /// <param name="addr">The base address of the memory region to unlock</param>
        /// <param name="size">The size in bytes of the memory region to unlock</param>
        /// <returns>True if the memory was successfully unlocked, false otherwise</returns>
        /// <remarks>
        /// This method is only available on Linux and Windows platforms.
        /// On Linux, it calls the munlock function.
        /// On Windows, it calls the VirtualUnlock function.
        /// </remarks>
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public static bool UnlockMemory(nint addr, nuint size)
            => UnlockMemory(addr.ToPointer(), size);

        /// <summary>
        /// Unlocks a previously locked region of virtual memory on the system
        /// that was locked by calling <see cref="LockMemory{T}(MemoryHandle{T})"/>
        /// </summary>
        /// <typeparam name="T">The unmanaged datatype</typeparam>
        /// <param name="handle">The memory handle that was previously locked</param>
        /// <returns>True if the memory was successfully unlocked, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null</exception>
        /// <remarks>
        /// This method is only available on Linux and Windows platforms.
        /// Returns false if the handle is empty, closed, or invalid.
        /// </remarks>
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public static bool UnlockMemory<T>(MemoryHandle<T> handle) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(handle);

            if (handle.IsClosed || handle.IsInvalid)
            {
                return false;
            }

            return UnlockMemory(handle.BasePtr, handle.ByteLength);
        }

        /// <summary>
        /// Unlocks a previously locked region of virtual memory on the system
        /// </summary>
        /// <param name="memHandle">A reference to the memory handle to unlock</param>
        /// <param name="size">The size in bytes of the memory region to unlock</param>
        /// <returns>True if the memory was successfully unlocked, false otherwise</returns>
        /// <remarks>
        /// This method is only available on Linux and Windows platforms.
        /// It extracts the pointer from the memory handle and delegates to the
        /// <see cref="UnlockMemory(void*, nuint)"/> method.
        /// </remarks>
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("windows")]
        public static bool UnlockMemory(ref readonly MemoryHandle memHandle, nuint size)
            => UnlockMemory(memHandle.Pointer, size);
        #endregion

        #region Validation

        /// <summary>
        /// Gets the size in bytes of the handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle">The handle to get the byte size of</param>
        /// <returns>The number of bytes pointed to by the handle</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint ByteSize<T>(IMemoryHandle<T> handle)
        {
            ArgumentNullException.ThrowIfNull(handle);
            return ByteCount<T>(handle.Length);
        }

        /// <summary>
        /// Gets the size in bytes of the handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle">The handle to get the byte size of</param>
        /// <returns>The number of bytes pointed to by the handle</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint ByteSize<T>(in UnsafeMemoryHandle<T> handle) where T : unmanaged
            => ByteCount<T>(handle.Length);

        /// <summary>
        /// Gets the byte multiple of the length parameter
        /// </summary>
        /// <typeparam name="T">The type to get the byte offset of</typeparam>
        /// <param name="elementCount">The number of elements to get the byte count of</param>
        /// <returns>The byte multiple of the number of elments</returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint ByteCount<T>(nuint elementCount) 
            => checked(elementCount * (nuint)Unsafe.SizeOf<T>());

        /// <summary>
        /// Gets the byte multiple of the length parameter
        /// </summary>
        /// <typeparam name="T">The type to get the byte offset of</typeparam>
        /// <param name="elementCount">The number of elements to get the byte count of</param>
        /// <returns>The byte multiple of the number of elments</returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ByteCount<T>(uint elementCount) 
            => checked(elementCount * (uint)Unsafe.SizeOf<T>());

        /// <summary>
        /// Gets the byte multiple of the length parameter. NOTE: Does not verify negative values
        /// </summary>
        /// <typeparam name="T">The type to get the byte offset of</typeparam>
        /// <param name="elementCount">The number of elements to get the byte count of</param>
        /// <returns>The byte multiple of the number of elments</returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint ByteCount<T>(nint elementCount) 
            => checked(elementCount * Unsafe.SizeOf<T>());

        /// <summary>
        /// Gets the byte multiple of the length parameter. NOTE: Does not verify negative values
        /// </summary>
        /// <typeparam name="T">The type to get the byte offset of</typeparam>
        /// <param name="elementCount">The number of elements to get the byte count of</param>
        /// <returns>The byte multiple of the number of elments</returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ByteCount<T>(int elementCount) 
            => checked(elementCount * Unsafe.SizeOf<T>());

        /// <summary>
        /// Checks if the offset/count paramters for the given memory handle 
        /// point outside the block wrapped in the handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle">The handle to check bounds of</param>
        /// <param name="offset">The base offset to add</param>
        /// <param name="count">The number of bytes expected to be assigned or dereferrenced</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckBounds<T>(IMemoryHandle<T> handle, nuint offset, nuint count)
            => ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, handle.Length, nameof(count));

        /// <summary>
        /// Checks if the offset/count paramters for the given block
        /// point outside the block wrapped in the handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block">The handle to check bounds of</param>
        /// <param name="offset">The base offset to add</param>
        /// <param name="count">The number of bytes expected to be assigned or dereferrenced</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckBounds<T>(ReadOnlySpan<T> block, int offset, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, block.Length, nameof(count));
        }

        /// <summary>
        /// Checks if the offset/count paramters for the given block
        /// point outside the block wrapped in the handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block">The handle to check bounds of</param>
        /// <param name="offset">The base offset to add</param>
        /// <param name="count">The number of bytes expected to be assigned or dereferrenced</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckBounds<T>(Span<T> block, int offset, int count) 
            => CheckBounds((ReadOnlySpan<T>)block, offset, count);

        /// <summary>
        /// Checks if the offset/count paramters for the given block
        /// point outside the block wrapped in the handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block">The handle to check bounds of</param>
        /// <param name="offset">The base offset to add</param>
        /// <param name="count">The number of bytes expected to be assigned or dereferrenced</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckBounds<T>(ReadOnlyMemory<T> block, int offset, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, block.Length, nameof(count));
        }

        /// <summary>
        /// Checks if the offset/count paramters for the given block
        /// point outside the block wrapped in the handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block">The handle to check bounds of</param>
        /// <param name="offset">The base offset to add</param>
        /// <param name="count">The number of bytes expected to be assigned or dereferrenced</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckBounds<T>(Memory<T> block, int offset, int count) 
            => CheckBounds((ReadOnlyMemory<T>)block, offset, count);

        /// <summary>
        /// Checks if the offset/count paramters for the given block
        /// point outside the block bounds
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block">The handle to check bounds of</param>
        /// <param name="offset">The base offset to add</param>
        /// <param name="count">The number of bytes expected to be assigned or dereferrenced</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckBounds<T>(T[] block, nuint offset, nuint count)
            => ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, (ulong)block.LongLength, nameof(count));

        #endregion

        /// <summary>
        /// Pins the supplied array and gets the memory handle that controls 
        /// the pinning lifetime via GC handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array">The array to pin</param>
        /// <param name="elementOffset">The address offset</param>
        /// <returns>A <see cref="MemoryHandle"/> that manages the pinning of the supplied array</returns>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static MemoryHandle PinArrayAndGetHandle<T>(T[] array, nint elementOffset)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elementOffset);
            return PinArrayAndGetHandle(array, (nuint)elementOffset);
        }

        /// <summary>
        /// Pins the supplied array and gets the memory handle that controls 
        /// the pinning lifetime via GC handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array">The array to pin</param>
        /// <param name="elementOffset">The address offset</param>
        /// <returns>A <see cref="MemoryHandle"/> that manages the pinning of the supplied array</returns>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static MemoryHandle PinArrayAndGetHandle<T>(T[] array, nuint elementOffset)
        {
            ArgumentNullException.ThrowIfNull(array);

            //Quick verify index exists, may be the very last index
            CheckBounds(array, elementOffset, 1);

            //Pin the array
            GCHandle arrHandle = GCHandle.Alloc(array, GCHandleType.Pinned);

            //safe to get array base pointer
            ref T arrBase = ref MemoryMarshal.GetArrayDataReference(array);

            //Get element offset
            ref T indexOffet = ref Unsafe.Add(ref arrBase, elementOffset);

            return new(Unsafe.AsPointer(ref indexOffet), arrHandle);
        }

        /// <summary>
        /// Gets a runtime <see cref="MemoryHandle"/> wrapper for the given pointer
        /// </summary>
        /// <param name="value">The pointer to get the handle for</param>
        /// <param name="handle">The optional <see cref="GCHandle"/> to attach</param>
        /// <param name="pinnable">An optional <see cref="IPinnable"/> instace to wrap with the handle</param>
        /// <returns>The <see cref="MemoryHandle"/> wrapper</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle GetMemoryHandleFromPointer(IntPtr value, GCHandle handle = default, IPinnable? pinnable = null) 
            => new (value.ToPointer(), handle, pinnable);


        /// <summary>
        /// Slices the current array by the specified starting offset to the end 
        /// of the array
        /// </summary>
        /// <typeparam name="T">The array type</typeparam>
        /// <param name="arr"></param>
        /// <param name="start">The start offset of the new array slice</param>
        /// <returns>The sliced array</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static T[] SliceArray<T>(T[] arr, int start)
        {
            ArgumentNullException.ThrowIfNull(arr);
            return SliceArray(arr, start, arr.Length - start);
        }

        /// <summary>
        /// Slices the current array by the specified starting offset to including the 
        /// speciifed number of items
        /// </summary>
        /// <typeparam name="T">The array type</typeparam>
        /// <param name="arr"></param>
        /// <param name="start">The start offset of the new array slice</param>
        /// <param name="count">The size of the new array</param>
        /// <returns>The sliced array</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static T[] SliceArray<T>(T[] arr, int start, int count)
        {
            ArgumentNullException.ThrowIfNull(arr);
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start + count, arr.Length);

            if (count == 0)
            {
                return [];
            }

            //Calc the slice range
            Range sliceRange = new(start, start + count);
            return RuntimeHelpers.GetSubArray(arr, sliceRange);
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> from the supplied address
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address of the begining of the memory sequence</param>
        /// <param name="size">The size of the sequence</param>
        /// <returns>The span pointing to the memory at the supplied addres</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(IntPtr address, int size) 
            => new(address.ToPointer(), size);

        /// <summary>
        /// Gets a <see cref="Span{T}"/> over the block of memory pointed to by the supplied handle.
        /// reference
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="size">The size of the span (the size of the block)</param>
        /// <returns>A span over the block of memory pointed to by the handle of the specified size</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(ref readonly MemoryHandle handle, int size) 
            => new(handle.Pointer, size);
        
        /// <summary>
        /// Gets a <see cref="Span{T}"/> over the block of memory pointed to by the supplied handle.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="size">The size of the span (the size of the block)</param>
        /// <returns>A span over the block of memory pointed to by the handle of the specified size</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(MemoryHandle handle, int size) 
            => new(handle.Pointer, size);

        /// <summary>
        /// Recovers a reference to the supplied pointer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The base address to cast to a reference</param>
        /// <returns>The reference to the supplied address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetRef<T>(IntPtr address) 
            => ref Unsafe.AsRef<T>(address.ToPointer());

        /// <summary>
        /// Recovers a reference to the supplied pointer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The base address to cast to a reference</param>
        /// <param name="offset">The offset to add to the base address</param>
        /// <returns>The reference to the supplied address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetRef<T>(IntPtr address, nuint offset)
        {
            ref T baseRef = ref GetRef<T>(address);
            return ref Unsafe.Add(ref baseRef, offset);
        }

        /// <summary>
        /// Gets a managed pointer from the supplied handle
        /// </summary>
        /// <param name="handle">A reference to the handle to get the intpr for</param>
        /// <returns>A managed pointer from the handle</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetIntptr(ref readonly MemoryHandle handle) 
            => new(handle.Pointer);

        /// <summary>
        /// Rounds the requested byte size up to the nearest page
        /// number of bytes
        /// </summary>
        /// <param name="byteSize">The number of bytes to get the rounded page size of</param>
        /// <returns>The number of bytes equivalent to the requested byte size rounded to the next system memory page</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint NearestPage(nuint byteSize)
        {
            //Get page count by dividing count by number of pages
            nuint pages = (uint)Math.Ceiling(byteSize / (double)SystemPageSize);

            //Multiply back to page sizes
            return pages * (nuint)SystemPageSize;
        }

        /// <summary>
        /// Rounds the requested byte size up to the nearest page
        /// number of bytes
        /// </summary>
        /// <param name="byteSize">The number of bytes to get the rounded page size of</param>
        /// <returns>The number of bytes equivalent to the requested byte size rounded to the next system memory page</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint NearestPage(nint byteSize)
        {
            //Get page count by dividing count by number of pages
            nint pages = (int)Math.Ceiling(byteSize / (double)SystemPageSize);

            //Multiply back to page sizes
            return pages * SystemPageSize;
        }

        /// <summary>
        /// Rounds the requested number of elements up to the nearest page
        /// </summary>
        /// <typeparam name="T">The unmanaged type</typeparam>
        /// <param name="elements">The number of elements of size T to round</param>
        /// <returns>The number of elements rounded to the nearest page in elements</returns>
        public static nuint NearestPage<T>(nuint elements) where T : unmanaged
        {
            nuint elSize = (nuint)sizeof(T);
            //Round to nearest page (in bytes)
            return NearestPage(elements * elSize) / elSize;
        }

        /// <summary>
        /// Rounds the requested number of elements up to the nearest page
        /// </summary>
        /// <typeparam name="T">The unmanaged type</typeparam>
        /// <param name="elements">The number of elements of size T to round</param>
        /// <returns>The number of elements rounded to the nearest page in elements</returns>
        public static nint NearestPage<T>(nint elements) where T : unmanaged
        {
            nint elSize = sizeof(T);
            //Round to nearest page (in bytes)
            return NearestPage(elements * elSize) / elSize;
        }

        private static class Refs
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref byte AsByte<T>(void* ptr, nuint elementOffset) where T : unmanaged
            {
                //Compute the pointer offset and return the reference
                ref T asType = ref Unsafe.AsRef<T>(ptr);              
                ref T offset = ref Unsafe.Add(ref asType, elementOffset);
                return ref Unsafe.AsRef<byte>(ptr);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref byte AsByte<T>(ref T ptr, nuint elementOffset)
            {
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref byte AsByteR<T>(scoped ref readonly T ptr, nuint elementOffset)
            {
                ref T offset = ref Unsafe.Add(ref Unsafe.AsRef(in ptr), elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref byte AsByte<T>(T[] arr, nuint elementOffset)
            {
                ref T ptr = ref MemoryMarshal.GetArrayDataReference(arr);
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref byte AsByte<T>(Span<T> span, nuint elementOffset)
            {
                ref T ptr = ref MemoryMarshal.GetReference(span);
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref byte AsByte<T>(ReadOnlySpan<T> span, nuint elementOffset)
            {
                ref T ptr = ref MemoryMarshal.GetReference(span);
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref byte AsByte<T>(IMemoryHandle<T> handle, nuint elementOffset)
            {
                ref T ptr = ref handle.GetReference();
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }
        }

        private interface I64BitBlock
        {
            ref byte GetOffsetRef();

            MemoryHandle Pin();

            nuint Offset { get; }

            void Validate(nuint count);
        }

        private readonly struct ArrayCopyHandle<T>(T[] array, nuint offset) : I64BitBlock
        {
            ///<inheritdoc/>
            public readonly nuint Offset => offset;

            ///<inheritdoc/>
            public readonly MemoryHandle Pin() => PinArrayAndGetHandle(array, 0);

            ///<inheritdoc/>
            public readonly ref byte GetOffsetRef() => ref Refs.AsByte(array, offset);           

            ///<inheritdoc/>
            public readonly void Validate(nuint count) => CheckBounds(array, offset, count);
        }

        private readonly struct RMemCopyHandle<T>(ReadOnlyMemory<T> block, nuint offset) : I64BitBlock
        {
            ///<inheritdoc/>
            public readonly nuint Offset => offset;

            ///<inheritdoc/>
            public readonly MemoryHandle Pin() => block.Pin();

            ///<inheritdoc/>
            public readonly ref byte GetOffsetRef() => ref Refs.AsByte(block.Span, offset);

            ///<inheritdoc/>
            public readonly void Validate(nuint count) => CheckBounds(block, (int)offset, checked((int)count));
        }

        private readonly struct WMemCopyHandle<T>(Memory<T> block, nuint offset) : I64BitBlock
        {
            ///<inheritdoc/>
            public readonly nuint Offset => offset;

            ///<inheritdoc/>
            public readonly MemoryHandle Pin() => block.Pin();

            ///<inheritdoc/>
            public readonly ref byte GetOffsetRef() => ref Refs.AsByte(block.Span, offset);
           
            ///<inheritdoc/>
            public readonly void Validate(nuint count) => CheckBounds(block, (int)offset, checked((int)count));
        }

        private readonly struct MemhandleCopyHandle<T>(IMemoryHandle<T> handle, nuint offset) : I64BitBlock
        {
            ///<inheritdoc/>
            public readonly nuint Offset => offset;

            ///<inheritdoc/>
            public readonly MemoryHandle Pin() => handle.Pin(0);

            ///<inheritdoc/>
            public readonly ref byte GetOffsetRef() => ref Refs.AsByte(handle, offset);

            ///<inheritdoc/>
            public readonly void Validate(nuint count) => CheckBounds(handle, offset, count);
        }
    }
}
