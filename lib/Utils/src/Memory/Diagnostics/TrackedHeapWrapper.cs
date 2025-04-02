/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: TrackedHeapWrapper.cs 
*
* TrackedHeapWrapper.cs is part of VNLib.Utils which is part of the larger 
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
using System.Collections.Concurrent;

namespace VNLib.Utils.Memory.Diagnostics
{
    /// <summary>
    /// A wrapper for <see cref="IUnmangedHeap"/> that tracks 
    /// statistics for memory allocations.
    /// </summary>
    /// <remarks>
    /// Creates a new diagnostics wrapper for the heap
    /// </remarks>
    /// <param name="heap">The heap to gather statistics on</param>
    /// <param name="ownsHeap">If true, the wrapper will dispose the heap when disposed</param>
    public class TrackedHeapWrapper(IUnmangedHeap heap, bool ownsHeap) : VnDisposeable, IUnmangedHeap
    {
        private readonly object _statsLock = new();
        private readonly ConcurrentDictionary<IntPtr, ulong> _table = new();

        ///<inheritdoc/>
        public HeapCreation CreationFlags => heap.CreationFlags;

        /// <summary>
        /// Gets the underlying heap
        /// </summary>
        protected IUnmangedHeap Heap => heap;

        /// <summary>
        /// Gets the internal lock held when updating statistics
        /// during allocations or frees
        /// </summary>
        protected object StatsLock => _statsLock;

        /* Stats block */
        private ulong _allocatedBytes;
        private ulong _maxBlockSize;
        private ulong _maxHeapSize;
        private ulong _minBlockSize = ulong.MaxValue;

        /// <summary>
        /// Captures the current state of the heap.
        /// </summary>
        /// <returns>A new <see cref="HeapStatistics"/> captured from the current heap</returns>
        public HeapStatistics GetCurrentStats()
        {
            //Aquire stats lock
            lock (_statsLock)
            {
                return new()
                {
                    AllocatedBytes = _allocatedBytes,
                    MaxBlockSize = _maxBlockSize,
                    MaxHeapSize = _maxHeapSize,
                    MinBlockSize = _minBlockSize,
                    //The number of elements in the table is the number of tracked blocks
                    AllocatedBlocks = (ulong)_table.Count
                };
            }
        }

        ///<inheritdoc/>
        public IntPtr Alloc(nuint elements, nuint size, bool zero)
        {
            //Calc the number of bytes allocated
            ulong bytes = checked((ulong)elements * (ulong)size);

            //Alloc the block
            IntPtr block = Heap.Alloc(elements, size, zero);

            //Store number of bytes allocated
            _table[block] = bytes;

            lock (_statsLock)
            {
                UpdateStats(bytes);
            }

            return block;
        }

        private void UpdateStats(ulong bytes)
        {
            //Update stats
            _allocatedBytes += bytes;
            _maxBlockSize = Math.Max(_maxBlockSize, bytes);
            _maxHeapSize = Math.Max(_maxHeapSize, _allocatedBytes);
            _minBlockSize = Math.Min(_minBlockSize, bytes);
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            if (ownsHeap)
            {
                heap.Dispose();
            }
        }

        ///<inheritdoc/>
        public bool Free(ref IntPtr block)
        {
            //Remove the block from the table, if it has already been freed, raise exception
            if (!_table.TryRemove(block, out ulong bytes))
            {
                throw new IllegalHeapOperationException($"Double free detected. The block {block:x} has already been freed.");
            }

            //Free the block
            bool result = Heap.Free(ref block);

            //Update stats
            lock (_statsLock)
            {
                _allocatedBytes -= bytes;
            }

            return result;
        }

        ///<inheritdoc/>
        public void Resize(ref IntPtr block, nuint elements, nuint size, bool zero)
        {
            //Store old block pointer
            IntPtr oldBlock = block;

            //Calc new size 
            ulong newSize = checked((ulong)size * (ulong)elements);

            //resize the block
            Heap.Resize(ref block, elements, size, zero);

            //Remove old size
            _ = _table.TryRemove(oldBlock, out ulong oldSize);

            //Update new size
            _table[block] = newSize;

            lock (_statsLock)
            {
                //Remove old ref
                _allocatedBytes -= oldSize;

                //Update stats
                UpdateStats(newSize);
            }
        }
    }
}
