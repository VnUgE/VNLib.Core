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
    public unsafe static class MemoryUtil
    {
        /// <summary>
        /// The environment variable name used to specify the shared heap type
        /// to create
        /// </summary>
        public const string SHARED_HEAP_TYPE_ENV= "VNLIB_SHARED_HEAP_TYPE";
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
        public const int MAX_UNSAFE_POOL_SIZE = 500 * 1024;
      
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
           
            Trace.WriteIf(diagEnable, "Shared heap diagnostics enabled");
            
            Lazy<IUnmangedHeap> heap = new (() => InitHeapInternal(true, diagEnable), LazyThreadSafetyMode.PublicationOnly);
            
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
        /// <returns>An <see cref="IUnmangedHeap"/> for the current process</returns>
        /// <exception cref="SystemException"></exception>
        /// <exception cref="DllNotFoundException"></exception>
        public static IUnmangedHeap InitializeNewHeapForProcess() => InitHeapInternal(false, false);

        private static IUnmangedHeap InitHeapInternal(bool isShared, bool enableStats)
        {
            bool IsWindows = OperatingSystem.IsWindows();
            
            //Get environment varable
            string? heapType = Environment.GetEnvironmentVariable(SHARED_HEAP_TYPE_ENV);
            
            //Get inital size
            string? sharedSize = Environment.GetEnvironmentVariable(SHARED_HEAP_INTIAL_SIZE_ENV);
            
            //Try to parse the shared size from the env
            if (!nuint.TryParse(sharedSize, out nuint defaultSize))
            {
                defaultSize = SHARED_HEAP_INIT_SIZE;
            }

            //convert to upper
            heapType = heapType?.ToUpperInvariant();
            
            //Create the heap
            IUnmangedHeap heap = heapType switch
            {
                "WIN32" => IsWindows ? Win32PrivateHeap.Create(defaultSize) : throw new PlatformNotSupportedException("Win32 private heaps are not supported on non-windows platforms"),
                //If the shared heap is being allocated, then return a lock free global heap
                "RPMALLOC" => isShared ? RpMallocPrivateHeap.GlobalHeap : new RpMallocPrivateHeap(false),
                //Get the process heap if the heap is shared, otherwise create a new win32 private heap
                _ => IsWindows && !isShared ? Win32PrivateHeap.Create(defaultSize) : new ProcessHeap(),
            };

            //If diagnosticts is enabled, wrap the heap in a stats heap
            return enableStats ? new TrackedHeapWrapper(heap) : heap;
        }

        /// <summary>
        /// Gets a value that indicates if the Rpmalloc native library is loaded
        /// </summary>
        public static bool IsRpMallocLoaded { get; } = Environment.GetEnvironmentVariable(SHARED_HEAP_TYPE_ENV)?.ToUpperInvariant() == "RPMALLOC";

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
            
            checked
            {
                fixed (void* ptr = &MemoryMarshal.GetReference(block))
                {
                    //Calls memset
                    Unsafe.InitBlock(ptr, 0, byteSize);
                }
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
            
            checked
            {
                //Pin memory and get pointer
                using MemoryHandle handle = block.Pin();
                //Calls memset
                Unsafe.InitBlock(handle.Pointer, 0, byteSize);
            }
        }

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
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="block">The pointer to the allocated structure</param>
        public static void ZeroStruct<T>(IntPtr block)
        {
            //get thes size of the structure
            int size = Unsafe.SizeOf<T>();
            //Zero block
            Unsafe.InitBlock(block.ToPointer(), 0, (uint)size);
        }
        
        /// <summary>
        /// Zeroes a block of memory pointing to the structure
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="structPtr">The pointer to the allocated structure</param>
        public static void ZeroStruct<T>(void* structPtr) where T: unmanaged
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
        public static void ZeroStruct<T>(T* structPtr) where T : unmanaged
        {
            //get thes size of the structure
            int size = Unsafe.SizeOf<T>();
            //Zero block
            Unsafe.InitBlock(structPtr, 0, (uint)size);
        }

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


#if TARGET_64BIT
            //Get the number of bytes to copy
            nuint byteCount = ByteCount<T>(count);

            //Get memory handle from source
            using MemoryHandle srcHandle = source.Pin(0);

            //get source offset
            T* src = (T*)srcHandle.Pointer + offset;

            //pin array
            fixed (T* dst = dest)
            {
                //Offset dest ptr
                T* dstOffset = dst + destOffset;

                //Copy src to set
                Buffer.MemoryCopy(src, dstOffset, byteCount, byteCount);
            }
#else
            //If 32bit its safe to use spans

            Span<T> src = source.Span.Slice((int)offset, (int)count);
            Span<T> dst = dest.AsSpan((int)destOffset, (int)count);
            //Copy
            src.CopyTo(dst);
#endif
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
                throw new ArgumentException("The offset or count is outside of the range of the block of memory");
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
            if (((nuint)block.LongLength - offset) <= count)
            {
                throw new ArgumentException("The offset or count is outside of the range of the block of memory");
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
            //Quick verify index exists
            _ = array[elementOffset];

            //Pin the array
            GCHandle arrHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
            //Get array base address
            void* basePtr = (void*)arrHandle.AddrOfPinnedObject();
            //Get element offset
            void* indexOffet = Unsafe.Add<T>(basePtr, elementOffset);

            return new(indexOffet, arrHandle);
        }

        #region alloc

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
        /// Rounds the requested byte size up to the nearest page
        /// number of bytes
        /// </summary>
        /// <param name="byteSize">The number of bytes to get the rounded page size of</param>
        /// <returns>The number of bytes equivalent to the requested byte size rounded to the next system memory page</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint NearestPage(nuint byteSize)
        {
            //Get page count by dividing count by number of pages
            nuint pages = (uint)Math.Ceiling(byteSize / (double)Environment.SystemPageSize);

            //Multiply back to page sizes
            return pages * (nuint)Environment.SystemPageSize;
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
            nint pages = (int)Math.Ceiling(byteSize / (double)Environment.SystemPageSize);

            //Multiply back to page sizes
            return pages * Environment.SystemPageSize;
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static UnsafeMemoryHandle<T> UnsafeAlloc<T>(int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            if(elements > MAX_UNSAFE_POOL_SIZE || IsRpMallocLoaded)
            {
                // Alloc from heap
                IntPtr block = Shared.Alloc((uint)elements, (uint)sizeof(T), zero);
                //Init new handle
                return new(Shared, block, elements);
            }
            else
            {
                return new(ArrayPool<T>.Shared, elements, zero);
            }
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnsafeMemoryHandle<T> UnsafeAllocNearestPage<T>(int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }
            //Round to nearest page (in bytes)
            nint np = NearestPage(elements * sizeof(T));

            //Resize to element size
            np /= sizeof(T);

            return UnsafeAlloc<T>((int)np, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static IMemoryHandle<T> SafeAlloc<T>(int elements, bool zero = false) where T: unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            //If the element count is larger than max pool size, alloc from shared heap
            if (elements > MAX_UNSAFE_POOL_SIZE)
            {
                //Alloc from shared heap
                return Shared.Alloc<T>(elements, zero);
            }
            else
            {
                //Get temp buffer from shared buffer pool
                return new VnTempBuffer<T>(elements, zero);
            }
        }

        /// <summary>
        /// Allocates a block of unmanaged, or pooled manaaged memory depending on
        /// compilation flags and runtime unamanged allocators, rounded up to the 
        /// neareset memory page.
        /// </summary>
        /// <typeparam name="T">The unamanged type to allocate</typeparam>
        /// <param name="elements">The number of elements of the type within the block</param>
        /// <param name="zero">Flag to zero elements during allocation before the method returns</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static IMemoryHandle<T> SafeAllocNearestPage<T>(int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentException("Number of elements must be a positive integer", nameof(elements));
            }

            //Round to nearest page (in bytes)
            nint np = NearestPage(elements * sizeof(T));

            //Resize to element size
            np /= sizeof(T);

            return SafeAlloc<T>((int)np, zero);
        }

        #endregion
    }
}