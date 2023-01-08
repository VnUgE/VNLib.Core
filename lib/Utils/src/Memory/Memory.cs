/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: Memory.cs 
*
* Memory.cs is part of VNLib.Utils which is part of the larger 
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
using System.IO;
using System.Buffers;
using System.Security;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides optimized cross-platform maanged/umanaged safe/unsafe memory operations
    /// </summary>
    [SecurityCritical]
    [ComVisible(false)]
    public unsafe static class Memory
    {
        public const string SHARED_HEAP_TYPE_ENV= "VNLIB_SHARED_HEAP_TYPE";
        public const string SHARED_HEAP_INTIAL_SIZE_ENV = "VNLIB_SHARED_HEAP_SIZE";

        /// <summary>
        /// Initial shared heap size (bytes)
        /// </summary>
        public const ulong SHARED_HEAP_INIT_SIZE = 20971520;

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

        private static readonly Lazy<IUnmangedHeap> _sharedHeap;

        static Memory()
        {
            _sharedHeap = new Lazy<IUnmangedHeap>(() => InitHeapInternal(true), LazyThreadSafetyMode.PublicationOnly);
            //Cleanup the heap on process exit
            AppDomain.CurrentDomain.DomainUnload += DomainUnloaded;
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
        /// Initializes a new <see cref="IUnmangedHeap"/> determined by compilation/runtime flags
        /// and operating system type for the current proccess.
        /// </summary>
        /// <returns>An <see cref="IUnmangedHeap"/> for the current process</returns>
        /// <exception cref="SystemException"></exception>
        /// <exception cref="DllNotFoundException"></exception>
        public static IUnmangedHeap InitializeNewHeapForProcess() => InitHeapInternal(false);

        private static IUnmangedHeap InitHeapInternal(bool isShared)
        {
            bool IsWindows = OperatingSystem.IsWindows();
            //Get environment varable
            string heapType = Environment.GetEnvironmentVariable(SHARED_HEAP_TYPE_ENV);
            //Get inital size
            string sharedSize = Environment.GetEnvironmentVariable(SHARED_HEAP_INTIAL_SIZE_ENV);
            //Try to parse the shared size from the env
            if (!ulong.TryParse(sharedSize, out ulong defaultSize))
            {
                defaultSize = SHARED_HEAP_INIT_SIZE;
            }
            //Gen the private heap from its type or default
            switch (heapType)
            {
                case "win32":
                    if (!IsWindows)
                    {
                        throw new PlatformNotSupportedException("Win32 private heaps are not supported on non-windows platforms");
                    }
                    return PrivateHeap.Create(defaultSize);
                case "rpmalloc":
                    //If the shared heap is being allocated, then return a lock free global heap
                    return isShared ? RpMallocPrivateHeap.GlobalHeap : new RpMallocPrivateHeap(false);
                default:
                    return IsWindows ? PrivateHeap.Create(defaultSize) : new ProcessHeap();
            }
        }

        /// <summary>
        /// Gets a value that indicates if the Rpmalloc native library is loaded
        /// </summary>
        public static bool IsRpMallocLoaded { get; } = Environment.GetEnvironmentVariable(SHARED_HEAP_TYPE_ENV) == "rpmalloc";

        #region Zero
        /// <summary>
        /// Zeros a block of memory of umanged type.  If Windows is detected at runtime, calls RtlSecureZeroMemory Win32 function
        /// </summary>
        /// <typeparam name="T">Unmanged datatype</typeparam>
        /// <param name="block">Block of memory to be cleared</param>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void UnsafeZeroMemory<T>(ReadOnlySpan<T> block) where T : unmanaged
        {
            if (!block.IsEmpty)
            {
                checked
                {
                    fixed (void* ptr = &MemoryMarshal.GetReference(block))
                    {
                        //Calls memset
                        Unsafe.InitBlock(ptr, 0, (uint)(block.Length * sizeof(T)));
                    }
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
            if (!block.IsEmpty)
            {
                checked
                {
                    //Pin memory and get pointer
                    using MemoryHandle handle = block.Pin();
                    //Calls memset
                    Unsafe.InitBlock(handle.Pointer, 0, (uint)(block.Length * sizeof(T)));
                }
            }
        }

        /// <summary>
        /// Initializes a block of memory with zeros 
        /// </summary>
        /// <typeparam name="T">The unmanaged</typeparam>
        /// <param name="block">The block of memory to initialize</param>
        public static void InitializeBlock<T>(Span<T> block) where T : unmanaged => UnsafeZeroMemory<T>(block);
        /// <summary>
        /// Initializes a block of memory with zeros 
        /// </summary>
        /// <typeparam name="T">The unmanaged</typeparam>
        /// <param name="block">The block of memory to initialize</param>
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
        public static void Copy<T>(ReadOnlySpan<T> source, MemoryHandle<T> dest, Int64 destOffset) where T : unmanaged
        {
            if (source.IsEmpty)
            {
                return;
            }
            if (dest.Length < (ulong)(destOffset + source.Length))
            {
                throw new ArgumentException("Source data is larger than the dest data block", nameof(source));
            }
            //Get long offset from the destination handle
            T* offset = dest.GetOffset(destOffset);
            fixed(void* src = &MemoryMarshal.GetReference(source))
            {
                int byteCount = checked(source.Length * sizeof(T));
                Unsafe.CopyBlock(offset, src, (uint)byteCount);
            }
        }
        /// <summary>
        /// Copies data from source memory to destination memory of an umanged data type
        /// </summary>
        /// <typeparam name="T">Unmanged type</typeparam>
        /// <param name="source">Source data <see cref="ReadOnlyMemory{T}"/></param>
        /// <param name="dest">Destination <see cref="MemoryHandle{T}"/></param>
        /// <param name="destOffset">Dest offset</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(ReadOnlyMemory<T> source, MemoryHandle<T> dest, Int64 destOffset) where T : unmanaged
        {
            if (source.IsEmpty)
            {
                return;
            }
            if (dest.Length < (ulong)(destOffset + source.Length))
            {
                throw new ArgumentException("Dest constraints are larger than the dest data block", nameof(source));
            }
            //Get long offset from the destination handle
            T* offset = dest.GetOffset(destOffset);
            //Pin the source memory
            using MemoryHandle srcHandle = source.Pin();
            int byteCount = checked(source.Length * sizeof(T));
            //Copy block using unsafe class
            Unsafe.CopyBlock(offset, srcHandle.Pointer, (uint)byteCount);
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
        public static void Copy<T>(MemoryHandle<T> source, Int64 sourceOffset, Span<T> dest, int destOffset, int count) where T : unmanaged
        {
            if (count <= 0)
            {
                return;
            }
            if (source.Length < (ulong)(sourceOffset + count))
            {
                throw new ArgumentException("Source constraints are larger than the source data block", nameof(count));
            }
            if (dest.Length < destOffset + count)
            {
                throw new ArgumentOutOfRangeException(nameof(destOffset), "Destination offset range cannot exceed the size of the destination buffer");
            }
            //Get offset to allow large blocks of memory
            T* src = source.GetOffset(sourceOffset);
            fixed(T* dst = &MemoryMarshal.GetReference(dest))
            {
                //Cacl offset
                T* dstoffset = dst + destOffset;
                int byteCount = checked(count * sizeof(T));
                //Aligned copy
                Unsafe.CopyBlock(dstoffset, src, (uint)byteCount);
            }
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
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Copy<T>(MemoryHandle<T> source, Int64 sourceOffset, Memory<T> dest, int destOffset, int count) where T : unmanaged
        {
            if (count == 0)
            {
                return;
            }
            if (source.Length < (ulong)(sourceOffset + count))
            {
                throw new ArgumentException("Source constraints are larger than the source data block", nameof(count));
            }
            if(dest.Length < destOffset + count)
            {
                throw new ArgumentOutOfRangeException(nameof(destOffset), "Destination offset range cannot exceed the size of the destination buffer");
            }
            //Get offset to allow large blocks of memory
            T* src = source.GetOffset(sourceOffset);
            //Pin the memory handle
            using MemoryHandle handle = dest.Pin();
            //Byte count
            int byteCount = checked(count * sizeof(T));
            //Dest offset
            T* dst = ((T*)handle.Pointer) + destOffset;
            //Aligned copy
            Unsafe.CopyBlock(dst, src, (uint)byteCount);
        }
        #endregion

        #region Streams
        /// <summary>
        /// Copies data from one stream to another in specified blocks
        /// </summary>
        /// <param name="source">Source memory</param>
        /// <param name="srcOffset">Source offset</param>
        /// <param name="dest">Destination memory</param>
        /// <param name="destOffst">Destination offset</param>
        /// <param name="count">Number of elements to copy</param>
        public static void Copy(Stream source, Int64 srcOffset, Stream dest, Int64 destOffst, Int64 count)
        {
            if (count == 0)
            {
                return;
            }
            if (count < 0)
            {
                throw new ArgumentException("Count must be a positive integer", nameof(count));
            }
            //Seek streams
            _ = source.Seek(srcOffset, SeekOrigin.Begin);
            _ = dest.Seek(destOffst, SeekOrigin.Begin);
            //Create new buffer
            using IMemoryHandle<byte> buffer = Shared.Alloc<byte>(count);
            Span<byte> buf = buffer.Span;
            int total = 0;
            do
            {
                //read from source
                int read = source.Read(buf);
                //guard
                if (read == 0)
                {
                    break;
                }
                //write read slice to dest
                dest.Write(buf[..read]);
                //update total read
                total += read;
            } while (total < count);
        }
        #endregion

        #region alloc

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

        #endregion
    }
}