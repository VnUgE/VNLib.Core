/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: PrivateBuffersMemoryPool.cs 
*
* PrivateBuffersMemoryPool.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides a <see cref="MemoryPool{T}"/> wrapper for using an <see cref="IUnmangedHeap"/>s
    /// </summary>
    /// <typeparam name="T">Unamanged memory type to provide data memory instances from</typeparam>
    public sealed class PrivateBuffersMemoryPool<T> : MemoryPool<T> where T : unmanaged
    {
        private readonly IUnmangedHeap Heap;

        internal PrivateBuffersMemoryPool(IUnmangedHeap heap, int maxSize)
        {
            Heap = heap;
            MaxBufferSize = maxSize;
        }

        ///<inheritdoc/>
        public override int MaxBufferSize { get; }

        ///<inheritdoc/>
        ///<exception cref="OutOfMemoryException"></exception>
        ///<exception cref="ObjectDisposedException"></exception>
        ///<exception cref="ArgumentOutOfRangeException"></exception>
        public override IMemoryOwner<T> Rent(int minBufferSize = 0) 
            => Heap.AllocMemory<T>(minBufferSize, zero: false);

        /// <summary>
        /// Allocates a new <see cref="MemoryManager{T}"/> of a different data type from the pool
        /// </summary>
        /// <typeparam name="TDifType">The unmanaged data type to allocate for</typeparam>
        /// <param name="minBufferSize">Minumum size of the buffer</param>
        /// <returns>The memory owner of a different data type</returns>
        public IMemoryOwner<TDifType> Rent<TDifType>(int minBufferSize = 0) where TDifType : unmanaged 
            => Heap.AllocMemory<TDifType>(minBufferSize, zero: false);

        ///<inheritdoc/>
        protected override void Dispose(bool disposing) => Heap.Dispose();
    }
}
