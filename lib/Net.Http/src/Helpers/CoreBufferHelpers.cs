/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: CoreBufferHelpers.cs 
*
* CoreBufferHelpers.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

/*
 * This class is meant to provide memory helper methods
 * as a centralized HTTP local memory api. 
 * 
 * Pools and heaps are privatized to help avoid 
 * leaking sensitive HTTP data across other application
 * allocations and help provide memory optimization.
 */



using System;
using System.IO;
using System.Buffers;
using System.Security;
using System.Threading;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http.Core
{

    /// <summary>
    /// Provides memory pools and an internal heap for allocations.
    /// </summary>
    internal static class CoreBufferHelpers
    {
        private sealed class InitDataBuffer : ISlindingWindowBuffer<byte>
        {
            private readonly ArrayPool<byte> pool;
            private readonly int size;
            
            private byte[]? buffer;

            public InitDataBuffer(ArrayPool<byte> pool, int size)
            {
                this.buffer = pool.Rent(size, true);
                this.pool = pool;
                this.size = size;
                WindowStartPos = 0;
                WindowEndPos = 0;
            }
            
            public int WindowStartPos { get; set; }
            public int WindowEndPos { get; set; }
            Memory<byte> ISlindingWindowBuffer<byte>.Buffer => buffer.AsMemory(0, size);

            public void Advance(int count)
            {
                WindowEndPos += count;
            }

            public void AdvanceStart(int count)
            {
                WindowStartPos += count;
            }

            public void Reset()
            {
                WindowStartPos = 0;
                WindowEndPos = 0;
            }

            //Release the buffer back to the pool
            void ISlindingWindowBuffer<byte>.Close()
            {
                pool.Return(buffer!);
                buffer = null;
            }
        }
       
        /// <summary>
        /// An internal HTTP character binary pool for HTTP specific internal buffers
        /// </summary>
        public static ArrayPool<byte> HttpBinBufferPool { get; } = ArrayPool<byte>.Create();
        /// <summary>
        /// An <see cref="IUnmangedHeap"/> used for internal HTTP buffers
        /// </summary>
        public static IUnmangedHeap HttpPrivateHeap => _lazyHeap.Value;

        private static readonly Lazy<IUnmangedHeap> _lazyHeap = new(Memory.InitializeNewHeapForProcess, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Alloctes an unsafe block of memory from the internal heap, or buffer pool
        /// </summary>
        /// <param name="size">The number of elemnts to allocate</param>
        /// <param name="zero">A value indicating of the block should be zeroed before returning</param>
        /// <returns>A handle to the block of memory</returns>
        /// <exception cref="SecurityException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static UnsafeMemoryHandle<byte> GetBinBuffer(int size, bool zero)
        {
            //Calc buffer size to the nearest page size
            size = (size / 4096 + 1) * 4096;

            //If rpmalloc lib is loaded, use it
            if (Memory.IsRpMallocLoaded)
            {
                return Memory.Shared.UnsafeAlloc<byte>(size, zero);
            }
            else if (size > Memory.MAX_UNSAFE_POOL_SIZE)
            {
                return HttpPrivateHeap.UnsafeAlloc<byte>(size, zero);
            }
            else
            {
                return new(HttpBinBufferPool, size, zero);
            }
        }

        public static IMemoryOwner<byte> GetMemory(int size, bool zero)
        {
            //Calc buffer size to the nearest page size
            size = (size / 4096 + 1) * 4096;

            //If rpmalloc lib is loaded, use it
            if (Memory.IsRpMallocLoaded)
            {
                return Memory.Shared.DirectAlloc<byte>(size, zero);
            }
            //Avoid locking in heap unless the buffer is too large to alloc array
            else if (size > Memory.MAX_UNSAFE_POOL_SIZE)
            {
                return HttpPrivateHeap.DirectAlloc<byte>(size, zero);
            }
            else
            {
                //Convert temp buffer to memory owner

#pragma warning disable CA2000 // Dispose objects before losing scope
                return new VnTempBuffer<byte>(HttpBinBufferPool, size, zero).ToMemoryManager();
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }

        /// <summary>
        /// Gets the remaining data in the reader buffer and prepares a 
        /// sliding window buffer to read data from
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="maxContentLength">Maximum content size to clamp the remaining buffer window to</param>
        /// <returns></returns>
        public static ISlindingWindowBuffer<byte>? GetReminaingData<T>(this ref T reader, long maxContentLength) where T: struct, IVnTextReader
        {
            //clamp max available to max content length
            int available = Math.Clamp(reader.Available, 0, (int)maxContentLength);
            if (available <= 0)
            {
                return null;
            }
            //Alloc sliding window buffer
            ISlindingWindowBuffer<byte> buffer = new InitDataBuffer(HttpBinBufferPool, available);
            //Read remaining data 
            reader.ReadRemaining(buffer.RemainingBuffer.Span);
            //Advance the buffer to the end of available data
            buffer.Advance(available);
            return buffer;
        }
        
    }
}
