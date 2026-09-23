/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginSharedMemoryAllocator.cs
*
* PluginSharedMemoryAllocator.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.ServiceStack is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;

using VNLib.Utils.Memory;
using VNLib.Plugins.Ipc.SharedMemory;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc
{
    /// <summary>
    /// Creates a simple <see cref="IPluginSharedMemoryManager"/> around an <see cref="IUnmanagedHeap"/> for use with
    /// <see cref="PluginSharedMemoryProvider"/> for IPC shared memory.
    /// </summary>
    /// <param name="heap">The heap instance to allocate unmanaged blocks from</param>
    /// <param name="zeroAllocations">A value that indicates if all allocations should be zeroed before returned to callers</param>
    public class PluginSharedMemoryAllocator(IUnmanagedHeap heap, bool zeroAllocations) 
        : IPluginSharedMemoryManager
    {
        private readonly IUnmanagedHeap heap = heap ?? throw new ArgumentNullException(nameof(heap));

        /// <inheritdoc/>
        public IPluginMemoryRegion Alloc(string name, int size)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            MemoryHandle<byte> handle = MemoryUtil.SafeAlloc<byte>(heap, size, zeroAllocations);
            return new PluginMemoryRegion(handle);
        }

        /// <inheritdoc/>
        public void Free(IPluginMemoryRegion region)
        {
            ArgumentNullException.ThrowIfNull(region);

            if (region is not PluginMemoryRegion r)
            {
                throw new ArgumentException("Region was not created by this allocator", nameof(region));
            }

            r.Handle.Dispose();
        }

        private sealed class PluginMemoryRegion(MemoryHandle<byte> handle) : IPluginMemoryRegion
        {
            internal readonly MemoryHandle<byte> Handle = handle;

            /// <inheritdoc/>
            public object SyncRoot { get; } = new();

            /// <inheritdoc/>
            public int Length => (int)Handle.Length; // Regions are always positive 32bit int sizes, never larger, safe to typecast

            /// <inheritdoc/>
            public Span<byte> AsSpan() => Handle.Span;

            /// <inheritdoc/>
            public ref byte GetReference(int offset)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(offset);
                return ref Handle.GetOffsetRef(checked((uint)offset));
            }

            /// <inheritdoc/>
            public MemoryHandle Pin(int elementIndex) => Handle.Pin(elementIndex);

            /// <inheritdoc/>
            public void Unpin() => Handle.Unpin();
        }
    }
}
