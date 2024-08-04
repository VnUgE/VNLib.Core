/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: MemoryPoolManager.cs 
*
* MemoryPoolManager.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Net.Http;

namespace VNLib.WebServer
{
    /// <summary>
    /// recovers a memory pool for the TCP server to alloc buffers from
    /// </summary>
    internal static class MemoryPoolManager
    {
        /// <summary>
        /// Gets an unmanaged memory pool provider for the TCP server to alloc buffers from
        /// </summary>
        /// <returns>The memory pool</returns>
        public static MemoryPool<byte> GetTcpPool(bool zeroOnAlloc) => new HttpMemoryPool(zeroOnAlloc);

        /// <summary>
        /// Gets a memory pool provider for the HTTP server to alloc buffers from
        /// </summary>
        /// <returns>The http server memory pool</returns>
        public static IHttpMemoryPool GetHttpPool(bool zeroOnAlloc) => new HttpMemoryPool(zeroOnAlloc);

        /*
         * Fun little umnanaged memory pool that allows for allocating blocks
         * with fast pointer access and zero cost pinning
         * 
         * All blocks are allocated to the nearest page size
         */

        internal sealed class HttpMemoryPool(bool zeroOnAlloc) : MemoryPool<byte>, IHttpMemoryPool
        {
            //Avoid the shared getter on every alloc call
            private readonly IUnmangedHeap _heap = MemoryUtil.Shared;

            ///<inheritdoc/>
            public override int MaxBufferSize { get; } = int.MaxValue;

            ///<inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IMemoryOwner<byte> AllocateBufferForContext(int bufferSize) => Rent(bufferSize);

            ///<inheritdoc/>
            public IResizeableMemoryHandle<T> AllocFormDataBuffer<T>(int initialSize) where T : unmanaged
            {
                return MemoryUtil.SafeAllocNearestPage<T>(_heap, initialSize, zeroOnAlloc);
            }

            ///<inheritdoc/>
            public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
            {
                nint initSize = MemoryUtil.NearestPage(minBufferSize);
                return new UnsafeMemoryManager(_heap, (nuint)initSize, zeroOnAlloc);
            }

            ///<inheritdoc/>
            protected override void Dispose(bool disposing)
            { }

            sealed class UnsafeMemoryManager(IUnmangedHeap heap, nuint bufferSize, bool zero) : MemoryManager<byte>
            {

                private nint _pointer = heap.Alloc(bufferSize, sizeof(byte), zero);
                private int _size = (int)bufferSize;

                ///<inheritdoc/>
                public override Span<byte> GetSpan()
                {
                    Debug.Assert(_pointer != nint.Zero, "Pointer to memory block is null, was not allocated properly or was released");

                    return MemoryUtil.GetSpan<byte>(_pointer, _size);
                }

                ///<inheritdoc/>
                public override MemoryHandle Pin(int elementIndex = 0)
                {
                    //Guard
                    ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
                    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elementIndex, _size);

                    Debug.Assert(_pointer != nint.Zero, "Pointer to memory block is null, was not allocated properly or was released");

                    //Get pointer offset from index
                    nint offset = nint.Add(_pointer, elementIndex);

                    //Return handle at offser
                    return MemoryUtil.GetMemoryHandleFromPointer(offset, pinnable: this);
                }

                //No-op
                public override void Unpin()
                { }

                protected override void Dispose(bool disposing)
                {
                    Debug.Assert(_pointer != nint.Zero, "Pointer to memory block is null, was not allocated properly");

                    bool freed = heap.Free(ref _pointer);

                    //Free the memory, should also zero the pointer
                    Debug.Assert(freed, "Failed to free an allocated block");

                    //Set size to 0
                    _size = 0;
                }
            }
        }
    }
}
