/*
* Copyright (c) 2023 Vaughn Nugent
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

using VNLib.Utils.Extensions;
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
           
            Trace.WriteIf(diagEnable, "Shared heap diagnostics enabled");
            Trace.WriteIf(globalZero, "Shared heap global zero enabled");
            
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
            HeapCreation cFlags = HeapCreation.UseSynchronization;

            /*
            * We need to set the shared flag and the synchronziation flag.
            * 
            * The heap impl may reset the synchronziation flag if it does not 
            * need serialziation
            */
            cFlags |= isShared ? HeapCreation.IsSharedHeap : HeapCreation.None;

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
        public static void UnsafeZeroMemory<T>(ReadOnlySpan<T> block) where T : unmanaged
        {
            if (block.IsEmpty)
            {
                return;
            }

            uint byteSize = ByteCount<T>((uint)block.Length);

            fixed (void* ptr = &MemoryMarshal.GetReference(block))
            {
                //Calls memset
                Unsafe.InitBlock(ptr, 0, byteSize);
            }
        }

        /// <summary>
        /// Zeros a block of memory of umanged type.  If Windows is detected at runtime, calls RtlSecureZeroMemory Win32 function
        /// </summary>
        /// <typeparam name="T">Unmanged datatype</typeparam>
        /// <param name="block">Block of memory to be cleared</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void UnsafeZeroMemory<T>(ReadOnlyMemory<T> block) where T : unmanaged
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
        public static void InitializeBlock<T>(Span<T> block) where T : unmanaged => UnsafeZeroMemory<T>(block);

        /// <summary>
        /// Initializes a block of memory with zeros 
        /// </summary>
        /// <typeparam name="T">The unmanaged</typeparam>
        /// <param name="block">The block of memory to initialize</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeBlock<T>(Memory<T> block) where T : unmanaged => UnsafeZeroMemory<T>(block);

        /// <summary>
        /// Zeroes a block of memory of the given unmanaged type
        /// </summary>
        /// <typeparam name="T">The unmanaged type to zero</typeparam>
        /// <param name="block">A pointer to the block of memory to zero</param>
        /// <param name="itemCount">The number of elements in the block to zero</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void InitializeBlock<T>(T* block, int itemCount) where T : unmanaged
        {
            if (itemCount == 0)
            {
                return;
            }

            //Get the size of the structure
            int size = sizeof(T);

            //Zero block
            Unsafe.InitBlock(block, 0, (uint)(size * itemCount));
        }

        /// <summary>
        /// Zeroes a block of memory of the given unmanaged type
        /// </summary>
        /// <typeparam name="T">The unmanaged type to zero</typeparam>
        /// <param name="block">A pointer to the block of memory to zero</param>
        /// <param name="itemCount">The number of elements in the block to zero</param>
        public static void InitializeBlock<T>(IntPtr block, int itemCount) where T : unmanaged => InitializeBlock((T*)block, itemCount);

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="block">The pointer to the allocated structure</param>
        public static void ZeroStruct<T>(IntPtr block)
        {
            //get thes size of the structure does not have to be primitive type
            int size = Unsafe.SizeOf<T>();
            //Zero block
            Unsafe.InitBlock(block.ToPointer(), 0, (uint)size);
        }

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="structPtr">The pointer to the allocated structure</param>
        public static void ZeroStruct<T>(void* structPtr) 
        {
            //get thes size of the structure
            int size = Unsafe.SizeOf<T>();
            //Zero block
            Unsafe.InitBlock(structPtr, 0, (uint)size);
        }

        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="structPtr">The pointer to the allocated structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroStruct<T>(T* structPtr) where T : unmanaged => Unsafe.InitBlock(structPtr, 0, (uint)sizeof(T));

        #endregion

        #region Copy

        /// <summary>
        /// Copies data from source memory to destination memory of an umanged data type
        /// </summary>
        /// <typeparam name="T">Unmanged type</typeparam>
        /// <param name="source">Source data <see cref="ReadOnlySpan{T}"/></param>
        /// <param name="dest">Destination <see cref="MemoryHandle{T}"/></param>
        /// <param name="destOffset">Dest offset</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(ReadOnlySpan<T> source, MemoryHandle<T> dest, nuint destOffset) where T : unmanaged
        {
            if (dest is null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            if (source.IsEmpty)
            {
                return;
            }

            //Get long offset from the destination handle (also checks bounds)
            Span<T> dst = dest.GetOffsetSpan(destOffset, source.Length);

            //Copy data
            source.CopyTo(dst);
        }

        /// <summary>
        /// Copies data from source memory to destination memory of an umanged data type
        /// </summary>
        /// <typeparam name="T">Unmanged type</typeparam>
        /// <param name="source">Source data <see cref="ReadOnlyMemory{T}"/></param>
        /// <param name="dest">Destination <see cref="MemoryHandle{T}"/></param>
        /// <param name="destOffset">Dest offset</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(ReadOnlyMemory<T> source, MemoryHandle<T> dest, nuint destOffset) where T : unmanaged
        {
            if (dest is null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            if (source.IsEmpty)
            {
                return;
            }

            //Get long offset from the destination handle (also checks bounds)
            Span<T> dst = dest.GetOffsetSpan(destOffset, source.Length);

            //Copy data
            source.Span.CopyTo(dst);
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
        public static void Copy<T>(MemoryHandle<T> source, nint sourceOffset, Span<T> dest, int destOffset, int count) where T : unmanaged
        {
            //Validate source/dest/count
            ValidateArgs(sourceOffset, destOffset, count);

            //Check count last for debug reasons
            if (count == 0)
            {
                return;
            }

            //Get offset span, also checks bounts
            Span<T> src = source.GetOffsetSpan(sourceOffset, count);

            //slice the dest span
            Span<T> dst = dest.Slice(destOffset, count);

            //Copy data
            src.CopyTo(dst);
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
        public static void Copy<T>(MemoryHandle<T> source, nint sourceOffset, Memory<T> dest, int destOffset, int count) where T : unmanaged
        {
            //Call copy method with dest as span
            Copy(source, sourceOffset, dest.Span, destOffset, count);
        }

        private static void ValidateArgs(nint sourceOffset, nint destOffset, nint count)
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
        /// 32/64 bit large block copy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source memory handle to copy data from</param>
        /// <param name="offset">The element offset to begin reading from</param>
        /// <param name="dest">The destination array to write data to</param>
        /// <param name="destOffset"></param>
        /// <param name="count">The number of elements to copy</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(IMemoryHandle<T> source, nuint offset, T[] dest, nuint destOffset, nuint count) where T : unmanaged
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (dest is null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            if (count == 0)
            {
                return;
            }

            //Check source bounds
            CheckBounds(source, offset, count);

            //Check dest bounts
            CheckBounds(dest, destOffset, count);

            //Check if 64bit
            if(sizeof(void*) == 8)
            {
                //Get the number of bytes to copy
                nuint byteCount = ByteCount<T>(count);

                //Get memory handle from source
                using MemoryHandle srcHandle = source.Pin(0);

                //get source offset
                T* src = (T*)srcHandle.Pointer + offset;

                //pin array
                fixed (T* dst = &MemoryMarshal.GetArrayDataReference(dest))
                {
                    //Offset dest ptr
                    T* dstOffset = dst + destOffset;

                    //Copy src to set
                    Buffer.MemoryCopy(src, dstOffset, byteCount, byteCount);
                }
            }
            else
            {
                //If 32bit its safe to use spans

                Span<T> src = source.AsSpan((int)offset, (int)count);
                Span<T> dst = dest.AsSpan((int)destOffset, (int)count);
                //Copy
                src.CopyTo(dst);
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
            _ = handle ?? throw new ArgumentNullException(nameof(handle));
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
        {
            if (offset + count > handle.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset or count are beyond the range of the supplied memory handle");
            }
        }

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
            //Call slice and discard to raise exception
            _ = block.Slice(offset, count);
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
            //Call slice and discard to raise exception
            _ = block.Slice(offset, count);
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
        {
            if (offset + count > (ulong)block.LongLength)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "The offset or count is outside of the range of the block of memory");
            }
        }

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
        public static MemoryHandle PinArrayAndGetHandle<T>(T[] array, int elementOffset)
        {
            if(elementOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elementOffset));
            }

            //Quick verify index exists, may be the very last index
            CheckBounds(array, (nuint)elementOffset, 1);

            //Pin the array
            GCHandle arrHandle = GCHandle.Alloc(array, GCHandleType.Pinned);

            //Get array base address
            void* basePtr = (void*)arrHandle.AddrOfPinnedObject();

            Debug.Assert(basePtr != null);

            //Get element offset
            void* indexOffet = Unsafe.Add<T>(basePtr, elementOffset);

            return new(indexOffet, arrHandle);
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
    }
}