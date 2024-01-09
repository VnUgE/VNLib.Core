/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using VNLib.Utils.Resources;
using VNLib.Utils.Memory.Diagnostics;

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

        public const int MAX_BUF_SIZE = 2097152;
        public const int MIN_BUF_SIZE = 16000;
        
        /// <summary>
        /// The maximum buffer size requested by <see cref="UnsafeAlloc{T}(int, bool)"/>
        /// that will use the array pool before falling back to the <see cref="Shared"/>.
        /// heap.
        /// </summary>
        public const int MAX_UNSAFE_POOL_SIZE = 80 * 1024;

        //Cache the system page size
        private static readonly int SystemPageSize = Environment.SystemPageSize;

        //Cache avx2 support
        private static readonly bool IsAvx2Supported = Avx2.IsSupported;

        /// <summary>
        /// Provides a shared heap instance for the process to allocate memory from.
        /// </summary>
        /// <remarks>
        /// The backing heap
        /// is determined by the OS type and process environment varibles.
        /// </remarks>
        public static IUnmangedHeap Shared => _sharedHeap.Value;

        
        private static readonly Lazy<IUnmangedHeap> _sharedHeap = InitHeapInternal();

        //Avoiding static initializer
        private static Lazy<IUnmangedHeap> InitHeapInternal()
        {
            //Get env for heap diag
            _ = ERRNO.TryParse(Environment.GetEnvironmentVariable(SHARED_HEAP_ENABLE_DIAGNOISTICS_ENV), out ERRNO diagEnable);
            _ = ERRNO.TryParse(Environment.GetEnvironmentVariable(SHARED_HEAP_GLOBAL_ZERO), out ERRNO globalZero);
           
            Trace.WriteLineIf(diagEnable, "Shared heap diagnostics enabled");
            Trace.WriteLineIf(globalZero, "Shared heap global zero enabled");
            
            Lazy<IUnmangedHeap> heap = new (() => InitHeapInternal(true, diagEnable, globalZero), LazyThreadSafetyMode.PublicationOnly);

            //Cleanup the heap on process exit
            AppDomain.CurrentDomain.DomainUnload += DomainUnloaded;
            
            return heap;
        }

        private static void DomainUnloaded(object? sender, EventArgs e)
        {
            //Dispose the heap if allocated
            if (_sharedHeap.IsValueCreated)
            {
                _sharedHeap.Value.Dispose();
            }
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
            return _sharedHeap.IsValueCreated && _sharedHeap.Value is TrackedHeapWrapper h 
                ? h.GetCurrentStats() : new HeapStatistics();
        }

        /// <summary>
        /// Initializes a new <see cref="IUnmangedHeap"/> determined by compilation/runtime flags
        /// and operating system type for the current proccess.
        /// </summary>
        /// <param name="globalZero">If true, sets the <see cref="HeapCreation.GlobalZero"/> flag</param>
        /// <returns>An <see cref="IUnmangedHeap"/> for the current process</returns>
        /// <exception cref="SystemException"></exception>
        /// <exception cref="DllNotFoundException"></exception>
        public static IUnmangedHeap InitializeNewHeapForProcess(bool globalZero = false) => InitHeapInternal(false, false, globalZero);

        private static IUnmangedHeap InitHeapInternal(bool isShared, bool enableStats, bool globalZero)
        {
            bool IsWindows = OperatingSystem.IsWindows();
            
            //Get environment varable
            string? heapDllPath = Environment.GetEnvironmentVariable(SHARED_HEAP_FILE_PATH);
            string? rawFlagsEnv = Environment.GetEnvironmentVariable(SHARED_HEAP_RAW_FLAGS);

            //Default flags
            HeapCreation cFlags = HeapCreation.UseSynchronization | HeapCreation.SupportsRealloc;

            /*
            * We need to set the shared flag and the synchronziation flag.
            * 
            * The heap impl may reset the synchronziation flag if it does not 
            * need serialziation
            */
            cFlags |= isShared ? HeapCreation.Shared : HeapCreation.None;

            //Set global zero flag if requested
            cFlags |= globalZero ? HeapCreation.GlobalZero : HeapCreation.None;

            IUnmangedHeap heap;

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
                heap = NativeHeap.LoadHeap(heapDllPath, DllImportSearchPath.SafeDirectories, cFlags, userFlags);
            }
            //No user heap was specified, use fallback
            else if (IsWindows)
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
                heap = Win32PrivateHeap.Create(defaultSize, cFlags, flags:userFlags); 
            }
            else
            {
                //Finally fallback to .NET native mem impl 
                heap = new ProcessHeap();
            }

            //Enable heap statistics
            return enableStats ? new TrackedHeapWrapper(heap, true) : heap;
        }

        /// <summary>
        /// Gets a value that indicates if the use defined a custom heap
        /// implementation
        /// </summary>
        public static bool IsUserDefinedHeap { get; } = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SHARED_HEAP_FILE_PATH));

        #region Zero

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
           
            ref T r0 = ref MemoryMarshal.GetReference(block);

            //Calls memset
            ZeroByRef(ref r0, (uint)block.Length);
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
            
            uint byteSize = ByteCount<T>((uint)block.Length);

            //Pin memory and get pointer
            using MemoryHandle handle = block.Pin();
            //Calls memset
            Unsafe.InitBlock(handle.Pointer, 0, byteSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void ZeroByRef<T>(ref T src, uint elements)
        {
            Debug.Assert(Unsafe.IsNullRef(ref src) == false, "Null reference passed to ZeroByRef");

            //Call init block on bytes
            Unsafe.InitBlock(
                ref Refs.AsByte(ref src, 0), 
                0, 
                ByteCount<T>(elements)
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
        public static void InitializeBlock<T>(Span<T> block) where T : struct => UnsafeZeroMemory<T>(block);

        /// <summary>
        /// Initializes a block of memory with zeros 
        /// </summary>
        /// <typeparam name="T">The unmanaged</typeparam>
        /// <param name="block">The block of memory to initialize</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(Memory<T> block) where T : struct => UnsafeZeroMemory<T>(block);

        /// <summary>
        /// Initializes the entire array with zeros 
        /// </summary>
        /// <typeparam name="T">A structure type to initialize</typeparam>
        /// <param name="array">The array to zero</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(T[] array) where T : struct => InitializeBlock(array, (uint)array.Length);

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
            ArgumentNullException.ThrowIfNull(array, nameof(array));

            //Check bounds
            CheckBounds(array, 0, count);

            //Get array data reference
            ref T arrRef = ref MemoryMarshal.GetArrayDataReference(array);
            ZeroByRef(ref arrRef, count);
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
            if (Unsafe.IsNullRef(ref block))
            {
                throw new ArgumentNullException(nameof(block));
            }

            if (itemCount <= 0)
            {
                return;
            }

            ZeroByRef(ref block, (uint)itemCount);
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
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            InitializeBlock(ref *block, itemCount);
        }

        /// <summary>
        /// Zeroes a block of memory of the given unmanaged type
        /// </summary>
        /// <typeparam name="T">The unmanaged type to zero</typeparam>
        /// <param name="block">A pointer to the block of memory to zero</param>
        /// <param name="itemCount">The number of elements in the block to zero</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(IntPtr block, int itemCount) where T : unmanaged => InitializeBlock((T*)block, itemCount);

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="structRef">The reference to the allocated structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroStruct<T>(ref T structRef) where T : unmanaged
        {
            if (Unsafe.IsNullRef(ref structRef))
            {
                throw new ArgumentNullException(nameof(structRef));
            }

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
            if (structPtr == null)
            {
                throw new ArgumentNullException(nameof(structPtr));
            }

            ZeroStruct(ref *structPtr);
        }

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="structPtr">The pointer to the allocated structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroStruct<T>(void* structPtr) where T: unmanaged => ZeroStruct((T*)structPtr);

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="block">The pointer to the allocated structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroStruct<T>(IntPtr block) where T : unmanaged => ZeroStruct<T>(block.ToPointer());


        #endregion

        #region Copy

        /*
         * Dirty little trick to access internal Buffer.Memmove method for 
         * large references. May not always be supported, so optional safe
         * guards are in place.
         */
        private delegate void BigMemmove(ref byte dest, ref byte src, nuint len);
        private static readonly BigMemmove? _clrMemmove = ManagedLibrary.TryGetStaticMethod<BigMemmove>(typeof(Buffer), "Memmove", System.Reflection.BindingFlags.NonPublic);
        
        /// <summary>
        /// Copies structure data from a source byte reference that points to a sequence of 
        /// of data to the target structure reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">An initialized target structure to copy data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void CopyStruct<T>(ref byte source, ref T target) where T : unmanaged
        {
            if (Unsafe.IsNullRef(ref target))
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (Unsafe.IsNullRef(ref source))
            {
                throw new ArgumentNullException(nameof(source));
            }

            //Recover byte reference of target struct
            ref byte dst = ref Unsafe.As<T, byte>(ref target);

            Unsafe.CopyBlockUnaligned(ref dst, ref source, (uint)sizeof(T));
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
        public static void CopyStruct<T>(ref T source, ref byte target) where T : unmanaged
        {
            if (Unsafe.IsNullRef(ref source))
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (Unsafe.IsNullRef(ref target))
            {
                throw new ArgumentNullException(nameof(target));
            }

            //Recover byte reference to struct
            ref byte src = ref Unsafe.As<T, byte>(ref source);

            //Memmove
            Unsafe.CopyBlockUnaligned(ref target, ref src, (uint)sizeof(T));
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
        public static void CopyStruct<T>(ref byte source, T* target) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(target);
            CopyStruct(ref source, ref *target);
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
        public static void CopyStruct<T>(ref byte source, void* target) where T : unmanaged => CopyStruct(ref source, (T*)target);

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
        public static void CopyStruct<T>(ReadOnlySpan<byte> sourceData, T* target) where T : unmanaged => CopyStruct(sourceData, ref *target);

        /// <summary>
        /// Copies structure data from a source sequence of data to the target structure reference.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceData">A referrence to the first byte of source data to copy from</param>
        /// <param name="target">A pointer to initialized target structure to copy data to</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyStruct<T>(ReadOnlySpan<byte> sourceData, void* target) where T: unmanaged => CopyStruct(sourceData, (T*)target);


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
        public static void CopyStruct<T>(void* source, ref byte target) where T : unmanaged => CopyStruct((T*)source, ref target);

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
        public static void CopyStruct<T>(ref T source, Span<byte> target) where T : unmanaged
        {
            //check that the span is large enough to hold the structure
            ArgumentOutOfRangeException.ThrowIfLessThan(target.Length, sizeof(T), nameof(target));

            CopyStruct(ref source, ref MemoryMarshal.GetReference(target));
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

            CopyStruct(ref *source, target);
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
        public static void CopyStruct<T>(void* source, Span<byte> target) where T : unmanaged => CopyStruct((T*)source, target);

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
        public static void CopyStruct<T>(IntPtr source, ref byte target) where T : unmanaged => CopyStruct(ref GetRef<T>(source), ref target);
        

        /// <summary>
        /// Copies the memory of the structure pointed to by the source reference to the target
        /// structure reference
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">A reference to the source structure to copy from</param>
        /// <param name="target">A reference to the target structure to copy to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CloneStruct<T>(ref T source, ref T target) where T : unmanaged
        {
            if (Unsafe.IsNullRef(ref source))
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (Unsafe.IsNullRef(ref target))
            {
                throw new ArgumentNullException(nameof(target));
            }

            //Byte refs
            ref byte src = ref Unsafe.As<T, byte>(ref source);
            ref byte dst = ref Unsafe.As<T, byte>(ref target);

            Unsafe.CopyBlockUnaligned(ref dst, ref src, (uint)sizeof(T));
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

            Unsafe.CopyBlockUnaligned(target, source, (uint)sizeof(T));
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
        public static void Copy<T>(ReadOnlySpan<T> source, int sourceOffset, IMemoryHandle<T> dest, nuint destOffset, int count) where T: struct
        {
            ArgumentNullException.ThrowIfNull(dest, nameof(dest));

            if (count == 0)
            {
                return;
            }

            //Check bounds
            CheckBounds(source, sourceOffset, count);
            CheckBounds(dest, destOffset, (uint)count);
           
            //Use memmove by ref
            bool success = CopyUtilCore.MemmoveByRef(
                ref Refs.AsByte(source, (nuint)sourceOffset),
                ref Refs.AsByte(dest, destOffset),
                ByteCount<T>((uint)count),
                false
            );

            Debug.Assert(success, "Memmove by ref call failed during a 32bit copy");
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(ReadOnlyMemory<T> source, int sourceOffset, IMemoryHandle<T> dest, nuint destOffset, int count) where T : struct 
            => Copy(source.Span, sourceOffset, dest, destOffset, count);

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
            ArgumentNullException.ThrowIfNull(source, nameof(source));

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
            bool success = CopyUtilCore.MemmoveByRef(
                ref Refs.AsByte(source, (nuint)sourceOffset), 
                ref Refs.AsByte(dest, (nuint)destOffset),
                ByteCount<T>((uint)count),
                false
            );

            Debug.Assert(success, "Memmove by ref call failed during a 32bit copy");
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(IMemoryHandle<T> source, nint sourceOffset, Memory<T> dest, int destOffset, int count) where T : struct 
            => Copy(source, sourceOffset, dest.Span, destOffset, count);

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
            ArgumentNullException.ThrowIfNull(source, nameof(source));
            ArgumentNullException.ThrowIfNull(dest, nameof(dest));

            CheckBounds(source, sourceOffset, count);
            CheckBounds(dest, destOffset, count);

            //Get byte ref and byte count
            nuint byteCount = ByteCount<T>(count);

            if (!CopyUtilCore.MemmoveByRef(
               ref Refs.AsByte(source, sourceOffset),
               ref Refs.AsByte(dest, destOffset),
               byteCount,
               false
               )
            )
            {
                //Copying block larger than 32bit must be done with pointers
                using MemoryHandle srcH = source.Pin(0);
                using MemoryHandle dstH = dest.Pin(0);

                //Get pointers and add offsets
                T* srcOffset = ((T*)srcH.Pointer) + sourceOffset;
                T* dstOffset = ((T*)dstH.Pointer) + destOffset;

                //Copy memory
                Buffer.MemoryCopy(srcOffset, dstOffset, byteCount, byteCount);
            }
        }

        private static void ValidateCopyArgs(nint sourceOffset, nint destOffset, nint count)
        {
            if(sourceOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source offset must be a postive integer");
            }

            if(destOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(destOffset), "Destination offset must be a positive integer");
            }

            if(count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count parameter must be a postitive integer");
            }
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
        /// <param name="destOffset"></param>
        /// <param name="count">The number of elements to copy</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void CopyArray<T>(IMemoryHandle<T> source, nuint sourceOffset, T[] dest, nuint destOffset, nuint count) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));
            ArgumentNullException.ThrowIfNull(dest, nameof(dest));

            if (count == 0)
            {
                return;
            }

            //Check bounds
            CheckBounds(source, sourceOffset, count);
            CheckBounds(dest, destOffset, count);

            //Get byte count
            nuint byteCount = ByteCount<T>(count);

            //Try to memove by ref first, otherwise fallback to pinning
            if (!CopyUtilCore.MemmoveByRef(
                ref Refs.AsByte(source, sourceOffset),
                ref Refs.AsByte(dest, destOffset),
                byteCount,
                false
                )
            )
            {
                //Copying block larger than 32bit must be done with pointers
                using MemoryHandle srcH = source.Pin(0);
                using MemoryHandle dstH = PinArrayAndGetHandle(dest, 0);

                //Get pointers and add offsets
                T* srcOffset = ((T*)srcH.Pointer) + sourceOffset;
                T* dstOffset = ((T*)dstH.Pointer) + destOffset;

                //Copy memory
                Buffer.MemoryCopy(srcOffset, dstOffset, byteCount, byteCount);
            }
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
        /// <param name="destOffset"></param>
        /// <param name="count">The number of elements to copy</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void CopyArray<T>(T[] source, nuint sourceOffset, IMemoryHandle<T> dest, nuint destOffset, nuint count) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));
            ArgumentNullException.ThrowIfNull(dest, nameof(dest));

            if (count == 0)
            {
                return;
            }

            //Check bounds
            CheckBounds(source, sourceOffset, count);
            CheckBounds(dest, destOffset, count);

            //Get byte count
            nuint byteCount = ByteCount<T>(count);

            //Try to memove by ref first, otherwise fallback to pinning
            if (!CopyUtilCore.MemmoveByRef( 
                ref Refs.AsByte(source, sourceOffset), 
                ref Refs.AsByte(dest, destOffset), 
                byteCount, 
                false
                )
            )
            {
                //Copying block larger than 32bit must be done with pointers
                using MemoryHandle srcH = PinArrayAndGetHandle(source, 0);
                using MemoryHandle dstH = dest.Pin(0);

                //Get pointers and add offsets
                T* srcOffset = ((T*)srcH.Pointer) + sourceOffset;
                T* dstOffset = ((T*)dstH.Pointer) + destOffset;

                //Copy memory
                Buffer.MemoryCopy(srcOffset, dstOffset, byteCount, byteCount);
            }
        }

        /// <summary>
        /// Low level api for copying data from source memory to destination memory of an
        /// umanged data type.
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
        public static void Memmove<T>(ref T src, nuint srcOffset, ref T dst, nuint dstOffset, nuint elementCount) where T : struct 
            => Memmove(ref src, srcOffset, ref dst, dstOffset, elementCount, false);

        /// <summary>
        /// Low level api for copying data from source memory to destination memory of an
        /// umanged data type. This call attempts to force hadrware acceleration if supported.
        /// <para>
        /// Understand that using this function attempts to force hardware acceleration, which 
        /// may hurt performance if the data is not large enough to justify the overhead.
        /// </para>
        /// <para>
        /// If the <see cref="Avx.IsSupported"/> flag is false, this function will fallback to
        /// the default method used by <see cref="Memmove{T}(ref T, nuint, ref T, nuint, nuint)"/>
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
        public static void AcceleratedMemmove<T>(ref T src, nuint srcOffset, ref T dst, nuint dstOffset, nuint elementCount) where T : struct 
            => Memmove(ref src, srcOffset, ref dst, dstOffset, elementCount, IsAvx2Supported);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Memmove<T>(ref T src, nuint srcOffset, ref T dst, nuint dstOffset, nuint elementCount, bool useAcceleration) where T : struct
        {
            if (Unsafe.IsNullRef(ref src))
            {
                throw new ArgumentNullException(nameof(src));
            }

            if (Unsafe.IsNullRef(ref dst))
            {
                throw new ArgumentNullException(nameof(dst));
            }

            if (elementCount == 0)
            {
                return;
            }

            if (!CopyUtilCore.MemmoveByRef(
                ref Refs.AsByte(ref src, srcOffset), 
                ref Refs.AsByte(ref dst, dstOffset), 
                ByteCount<T>(elementCount), 
                useAcceleration)
            )
            {
                throw new ArgumentException("The number of bytes to copy was larger than Uint32.MaxValue and was unsupported on this platform", nameof(elementCount));
            }
        }

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
            return checked(handle.Length * (nuint)Unsafe.SizeOf<T>());
        }

        /// <summary>
        /// Gets the size in bytes of the handle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle">The handle to get the byte size of</param>
        /// <returns>The number of bytes pointed to by the handle</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint ByteSize<T>(in UnsafeMemoryHandle<T> handle) where T : unmanaged => checked(handle.Length * (nuint)sizeof(T));

        /// <summary>
        /// Gets the byte multiple of the length parameter
        /// </summary>
        /// <typeparam name="T">The type to get the byte offset of</typeparam>
        /// <param name="elementCount">The number of elements to get the byte count of</param>
        /// <returns>The byte multiple of the number of elments</returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint ByteCount<T>(nuint elementCount) => checked(elementCount * (nuint)Unsafe.SizeOf<T>());

        /// <summary>
        /// Gets the byte multiple of the length parameter
        /// </summary>
        /// <typeparam name="T">The type to get the byte offset of</typeparam>
        /// <param name="elementCount">The number of elements to get the byte count of</param>
        /// <returns>The byte multiple of the number of elments</returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ByteCount<T>(uint elementCount) => checked(elementCount * (uint)Unsafe.SizeOf<T>());

        /// <summary>
        /// Gets the byte multiple of the length parameter. NOTE: Does not verify negative values
        /// </summary>
        /// <typeparam name="T">The type to get the byte offset of</typeparam>
        /// <param name="elementCount">The number of elements to get the byte count of</param>
        /// <returns>The byte multiple of the number of elments</returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint ByteCount<T>(nint elementCount) => checked(elementCount * Unsafe.SizeOf<T>());

        /// <summary>
        /// Gets the byte multiple of the length parameter. NOTE: Does not verify negative values
        /// </summary>
        /// <typeparam name="T">The type to get the byte offset of</typeparam>
        /// <param name="elementCount">The number of elements to get the byte count of</param>
        /// <returns>The byte multiple of the number of elments</returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ByteCount<T>(int elementCount) => checked(elementCount * Unsafe.SizeOf<T>());

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
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, block.Length, nameof(count));
        }

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
            ArgumentNullException.ThrowIfNull(array, nameof(array));

            //Quick verify index exists, may be the very last index
            CheckBounds(array, elementOffset, 1);

            //Pin the array
            GCHandle arrHandle = GCHandle.Alloc(array, GCHandleType.Pinned);

            //safe to get array basee pointer
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
        public static MemoryHandle GetMemoryHandleFromPointer(IntPtr value, GCHandle handle = default, IPinnable? pinnable = null) => new (value.ToPointer(), handle, pinnable);

        /// <summary>
        /// Gets a <see cref="Span{T}"/> from the supplied address
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address of the begining of the memory sequence</param>
        /// <param name="size">The size of the sequence</param>
        /// <returns>The span pointing to the memory at the supplied addres</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(IntPtr address, int size) => new(address.ToPointer(), size);

        /// <summary>
        /// Gets a <see cref="Span{T}"/> over the block of memory pointed to by the supplied handle.
        /// reference
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="size">The size of the span (the size of the block)</param>
        /// <returns>A span over the block of memory pointed to by the handle of the specified size</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(ref MemoryHandle handle, int size) => new(handle.Pointer, size);
        
        /// <summary>
        /// Gets a <see cref="Span{T}"/> over the block of memory pointed to by the supplied handle.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="size">The size of the span (the size of the block)</param>
        /// <returns>A span over the block of memory pointed to by the handle of the specified size</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(MemoryHandle handle, int size) => new(handle.Pointer, size);

        /// <summary>
        /// Recovers a reference to the supplied pointer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The base address to cast to a reference</param>
        /// <returns>The reference to the supplied address</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetRef<T>(IntPtr address) => ref Unsafe.AsRef<T>(address.ToPointer());

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
            return ref Unsafe.Add(ref baseRef, (nint)offset);
        }

        /// <summary>
        /// Gets a managed pointer from the supplied handle
        /// </summary>
        /// <param name="handle">A reference to the handle to get the intpr for</param>
        /// <returns>A managed pointer from the handle</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetIntptr(ref MemoryHandle handle) => new(handle.Pointer);

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


        private static class CopyUtilCore
        {

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public static bool MemmoveByRef(ref byte srcByte, ref byte dstByte, nuint byteCount, bool forceAcceleration)
            {
                Debug.Assert(!Unsafe.IsNullRef(ref srcByte), "Null source reference passed to MemmoveByRef");
                Debug.Assert(!Unsafe.IsNullRef(ref dstByte), "Null destination reference passed to MemmoveByRef");

                if (IsAvx2Supported)
                {
                    //If the data is aligned, always use the 32 byte copy
                    if (Is32ByteAligned(byteCount))
                    {
                        _avx32ByteCopy(ref srcByte, ref dstByte, byteCount);
                        return true;
                    }

                    //See if forcing acceleration is desired
                    if (forceAcceleration)
                    {
                        //not aligned, so we need to only copy the aligned portion
                        nuint remainder = byteCount % 0x20u;
                        nuint alignedCount = byteCount - remainder;

                        //Copy aligned portion
                        _avx32ByteCopy(ref srcByte, ref dstByte, alignedCount);

                        //Upshift references to the remainder addresses
                        ref byte srcRemainder = ref Unsafe.Add(ref srcByte, alignedCount);
                        ref byte dstRemainder = ref Unsafe.Add(ref dstByte, alignedCount);

                        //finish copying remaining data
                        bool success = _memmove(ref srcRemainder, ref dstRemainder, remainder);
                        Debug.Assert(success, "Memmove by ref call failed during a 32bit copy");

                        return true;
                    }
                }

                //fallback to memmove
                return _memmove(ref srcByte, ref dstByte, byteCount);
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            private static bool _memmove(ref byte src, ref byte dst, nuint byteCount)
            {
                if (_clrMemmove != null)
                {
                    //Call sysinternal memmove
                    _clrMemmove(ref dst, ref src, byteCount);
                    return true;
                }
                else if (byteCount < uint.MaxValue)
                {
                    //Use safe 32bit block copy
                    Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            const nuint _avx32ByteAlignment = 0x20u;

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            private static void _avx32ByteCopy(
                ref byte src,
                ref byte dst,
                nuint count
            )
            {
                Debug.Assert(Is32ByteAligned(count), "Byte count must be 32 byte aligned");
                Debug.Assert(Avx2.IsSupported, "AVX2 is not supported on this platform");

                //determine the number of loops
                nuint loopCount = count / _avx32ByteAlignment;

                fixed (byte* srcPtr = &src, dstPtr = &dst)
                {
                    //local mutable copies
                    byte* srcOffset = srcPtr;
                    byte* dstOffset = dstPtr;

                    for (nuint i = 0; i < loopCount; i++)
                    {
                        //avx vector load
                        Vector256<byte> srcVector = Avx.LoadVector256(srcOffset);
                        Avx.Store(dstOffset, srcVector);

                        //Upshift pointers
                        srcOffset += Vector256<byte>.Count;
                        dstOffset += Vector256<byte>.Count;
                    }
                }
            }


            /// <summary>
            /// Determines if the given size 32-byte aligned
            /// </summary>
            /// <param name="size">The block size to test</param>
            /// <returns>A value that indicates if the block size is 32byte aligned</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Is32ByteAligned(nuint size) => unchecked(size % _avx32ByteAlignment) == 0;
        }

        private static class Refs
        {
            public static ref byte AsByte<T>(ref T ptr, nuint elementOffset)
            {
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            public static ref byte AsByte<T>(T[] arr, nuint elementOffset)
            {
                ref T ptr = ref MemoryMarshal.GetArrayDataReference(arr);
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            public static ref byte AsByte<T>(Span<T> span, nuint elementOffset)
            {
                ref T ptr = ref MemoryMarshal.GetReference(span);
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            public static ref byte AsByte<T>(ReadOnlySpan<T> span, nuint elementOffset)
            {
                ref T ptr = ref MemoryMarshal.GetReference(span);
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }

            public static ref byte AsByte<T>(IMemoryHandle<T> handle, nuint elementOffset)
            {
                ref T ptr = ref handle.GetReference();
                ref T offset = ref Unsafe.Add(ref ptr, elementOffset);
                return ref Unsafe.As<T, byte>(ref offset);
            }
        }
    }
}