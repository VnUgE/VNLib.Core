/*
* Copyright (c) 2023 Vaughn Nugent
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
        /// <summary>
        /// An internal HTTP character binary pool for HTTP specific internal buffers
        /// </summary>
        public static ArrayPool<byte> HttpBinBufferPool { get; } = ArrayPool<byte>.Create();

        /// <summary>
        /// An <see cref="IUnmangedHeap"/> used for internal HTTP buffers
        /// </summary>
        public static IUnmangedHeap HttpPrivateHeap => _lazyHeap.Value;

        private static readonly Lazy<IUnmangedHeap> _lazyHeap = new(MemoryUtil.InitializeNewHeapForProcess, LazyThreadSafetyMode.PublicationOnly);

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
            size = (int)MemoryUtil.NearestPage(size);

            /*
             * Heap synchronziation may be enabled for our private heap, so we may want
             * to avoid it in favor of performance over private heap segmentation.
             * 
             * If synchronization is enabled, use the system heap
             */
            
            if ((HttpPrivateHeap.CreationFlags & HeapCreation.UseSynchronization) > 0)
            {
                return MemoryUtil.UnsafeAlloc(size, zero);
            }
            else
            {
                return HttpPrivateHeap.UnsafeAlloc<byte>(size, zero);
            }
        }

        public static IMemoryOwner<byte> GetMemory(int size, bool zero)
        {
            //Calc buffer size to the nearest page size
            size = (int)MemoryUtil.NearestPage(size);

            /*
             * Heap synchronziation may be enabled for our private heap, so we may want
             * to avoid it in favor of performance over private heap segmentation.
             * 
             * If synchronization is enabled, use the system heap
             */

            if ((HttpPrivateHeap.CreationFlags & HeapCreation.UseSynchronization) > 0)
            {
                return MemoryUtil.Shared.DirectAlloc<byte>(size, zero);
            }
            //If the block is larger than an safe array size, avoid LOH pressure
            else if(size > MemoryUtil.MAX_UNSAFE_POOL_SIZE)
            {
                return HttpPrivateHeap.DirectAlloc<byte>(size, zero);
            }
            //Use the array pool to get a memory handle
            else
            {
                return new VnTempBuffer<byte>(HttpBinBufferPool, size, zero);
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
        public static InitDataBuffer? GetReminaingData<T>(this ref T reader, long maxContentLength) where T: struct, IVnTextReader
        {
            //clamp max available to max content length
            int available = Math.Clamp(reader.Available, 0, (int)maxContentLength);
            if (available <= 0)
            {
                return null;
            }

            //Creates the new initial data buffer
            InitDataBuffer buf = InitDataBuffer.AllocBuffer(HttpBinBufferPool, available);

            //Read remaining data into the buffer's data segment
            _ = reader.ReadRemaining(buf.DataSegment);
           
            return buf;
        }
        
    }
}
