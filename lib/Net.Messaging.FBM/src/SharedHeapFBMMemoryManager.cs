/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: SharedHeapFBMMemoryManager.cs 
*
* SharedHeapFBMMemoryManager.cs is part of VNLib.Net.Messaging.FBM which 
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// A default/fallback implementation of a <see cref="IFBMMemoryManager"/> that 
    /// uses an <see cref="IUnmangedHeap"/> to allocate buffers from
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of <see cref="SharedHeapFBMMemoryManager"/> allocating
    /// memory from the specified <see cref="IUnmangedHeap"/>
    /// </remarks>
    /// <param name="heap">The heap to allocate memory from</param>
    /// <exception cref="ArgumentNullException"></exception>
    public sealed class SharedHeapFBMMemoryManager(IUnmangedHeap heap) : IFBMMemoryManager
    {
        private readonly IUnmangedHeap _heap = heap ?? throw new ArgumentNullException(nameof(heap));

        ///<inheritdoc/>
        public void AllocBuffer(IFBMSpanOnlyMemoryHandle state, int size)
        {
            ArgumentNullException.ThrowIfNull(state);
            (state as IFBMBufferHolder)!.AllocBuffer(size);
        }

        ///<inheritdoc/>
        public void FreeBuffer(IFBMSpanOnlyMemoryHandle state)
        {
            ArgumentNullException.ThrowIfNull(state);
            (state as IFBMBufferHolder)!.FreeBuffer();
        }

        ///<inheritdoc/>
        public IFBMMemoryHandle InitHandle() => new FBMMemHandle(_heap);

        ///<inheritdoc/>
        public IFBMSpanOnlyMemoryHandle InitSpanOnly() => new FBMSpanOnlyMemHandle(_heap);

        ///<inheritdoc/>
        public bool TryGetHeap([NotNullWhen(true)] out IUnmangedHeap? heap)
        {
            heap = _heap;
            return true;
        }

        interface IFBMBufferHolder
        {
            void AllocBuffer(int size);

            void FreeBuffer();
        }

        private sealed record class FBMMemHandle(IUnmangedHeap Heap) : IFBMMemoryHandle, IFBMBufferHolder
        {
            private MemoryHandle<byte>? _handle;
            private MemoryManager<byte>? _memHandle;

            ///<inheritdoc/>
            public Memory<byte> GetMemory()
            {
                _ = _memHandle ?? throw new InvalidOperationException("Buffer has not allocated");
                return _memHandle.Memory;
            }

            ///<inheritdoc/>
            public Span<byte> GetSpan()
            {
                _ = _handle ?? throw new InvalidOperationException("Buffer has not allocated");
                return _handle.Span;
            }

            ///<inheritdoc/>
            public void AllocBuffer(int size)
            {
                //Alloc buffer and memory manager to wrap it
                _handle = Heap.Alloc<byte>(size, false);
                _memHandle = _handle.ToMemoryManager(false);
            }

            ///<inheritdoc/>
            public void FreeBuffer()
            {
                _handle?.Dispose();

                _handle = null;
                _memHandle = null;
            }
        }

        private sealed record class FBMSpanOnlyMemHandle(IUnmangedHeap Heap) : IFBMSpanOnlyMemoryHandle, IFBMBufferHolder
        {
            private MemoryHandle<byte>? _handle;

            ///<inheritdoc/>
            public void AllocBuffer(int size) => _handle = Heap.Alloc<byte>(size, false);

            ///<inheritdoc/>
            public void FreeBuffer() => _handle?.Dispose();

            ///<inheritdoc/>
            public Span<byte> GetSpan()
            {
                _ = _handle ?? throw new InvalidOperationException("Buffer has not allocated");
                return _handle.Span;
            }
        }
    }
}
