/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginSharedMemoryAllocatorTests.cs
*
* PluginSharedMemoryAllocatorTests.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Memory;
using VNLib.Utils.Memory.Diagnostics;
using VNLib.Plugins.Ipc.SharedMemory;
using VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc;

namespace VNLib.Plugins.Essentials.ServiceStack.Tests.Ipc
{
    [TestClass]
    public sealed class PluginSharedMemoryAllocatorTests
    {
        /*
         * Fresh isolated heap per test via TrackedHeapWrapper wrapping the global shared heap.
         * The wrapper tracks only its own allocations so tests are independent of process-wide state.
         */
        private static TrackedHeapWrapper GetTestHeap() => new(MemoryUtil.Shared, false);

        #region Alloc

        /// <summary>
        /// Verifies that a single alloc/free cycle is correctly reflected in 
        /// the underlying heap's tracked block and byte counters.
        /// </summary>
        [TestMethod]
        public void Alloc_SingleRegion_HeapStatsMatchExpected()
        {
            const int TestRegionSize = 1024;
            const string TestRegionName = "test";

            using TrackedHeapWrapper heap = GetTestHeap();

            // Wrapper must report a completely empty baseline before any use
            HeapStatistics pre = heap.GetCurrentStats();
            Assert.AreEqual(0ul, pre.AllocatedBlocks);
            Assert.AreEqual(0ul, pre.AllocatedBytes);

            PluginSharedMemoryAllocator allocator = new(heap, false);
            IPluginMemoryRegion region = allocator.Alloc(TestRegionName, TestRegionSize);

            // Exactly one block of the requested byte size must be live
            HeapStatistics postAlloc = heap.GetCurrentStats();
            Assert.AreEqual(1ul, postAlloc.AllocatedBlocks);
            Assert.AreEqual((ulong)TestRegionSize, postAlloc.AllocatedBytes);

            allocator.Free(region);

            // Heap must return to baseline — no leaks
            HeapStatistics postFree = heap.GetCurrentStats();
            Assert.AreEqual(0ul, postFree.AllocatedBlocks);
            Assert.AreEqual(0ul, postFree.AllocatedBytes);
        }

        /// <summary>
        /// Verifies that multiple independently-named allocations are all tracked by 
        /// the heap, and that freeing each one returns the heap cleanly to baseline.
        /// </summary>
        [TestMethod]
        public void Alloc_MultipleRegions_AllFreedHeapReturnsToBaseline()
        {
            const int RegionSize  = 128;
            const int RegionCount = 4;

            using TrackedHeapWrapper heap = GetTestHeap();
            PluginSharedMemoryAllocator allocator = new(heap, false);

            IPluginMemoryRegion[] regions = new IPluginMemoryRegion[RegionCount];
            for (int i = 0; i < RegionCount; i++)
            {
                regions[i] = allocator.Alloc($"region-{i}", RegionSize);
            }

            // All blocks must be accounted for before any free
            HeapStatistics afterAllocs = heap.GetCurrentStats();
            Assert.AreEqual((ulong)RegionCount, afterAllocs.AllocatedBlocks);
            Assert.AreEqual((ulong)(RegionSize * RegionCount), afterAllocs.AllocatedBytes);

            Array.ForEach(regions, allocator.Free);

            // Every byte must be returned — no leaks
            HeapStatistics afterFrees = heap.GetCurrentStats();
            Assert.AreEqual(0ul, afterFrees.AllocatedBlocks);
            Assert.AreEqual(0ul, afterFrees.AllocatedBytes);
        }

        /// <summary>
        /// Verifies that <see cref="IPluginMemoryRegion.Length"/> reports the exact byte 
        /// count that was requested during allocation.
        /// </summary>
        [TestMethod]
        public void Alloc_Region_LengthMatchesRequestedSize()
        {
            const int TestRegionSize = 512;

            using TrackedHeapWrapper heap = GetTestHeap();
            PluginSharedMemoryAllocator allocator = new(heap, false);

            IPluginMemoryRegion region = allocator.Alloc("test", TestRegionSize);

            try
            {
                Assert.AreEqual(TestRegionSize, region.Length);
                Assert.AreEqual(TestRegionSize, region.AsSpan().Length);
            }
            finally
            {
                allocator.Free(region);
            }
        }

        /// <summary>
        /// Verifies that <see cref="IPluginMemoryRegion.SyncRoot"/> is non-null and 
        /// therefore safe to pass to <c>lock</c>.
        /// </summary>
        [TestMethod]
        public void Alloc_Region_SyncRootIsNotNull()
        {
            using TrackedHeapWrapper heap = GetTestHeap();
            PluginSharedMemoryAllocator allocator = new(heap, false);
            IPluginMemoryRegion region = allocator.Alloc("test", 64);

            try
            {
                Assert.IsNotNull(region.SyncRoot);
            }
            finally
            {
                allocator.Free(region);
            }
        }

        /// <summary>
        /// Verifies that passing <c>zeroAllocations: true</c> produces a region whose 
        /// entire initial content is zeroed. Uses <see cref="MemoryExtensions.IndexOfAnyExcept"/>
        /// to scan without allocating a comparison buffer.
        /// </summary>
        [TestMethod]
        public void Alloc_WithZeroFlag_SpanIsAllZeros()
        {
            const int TestRegionSize = 256;

            using TrackedHeapWrapper heap = GetTestHeap();
            PluginSharedMemoryAllocator allocator = new(heap, zeroAllocations: true);
            IPluginMemoryRegion region = allocator.Alloc("test", TestRegionSize);

            try
            {
                int firstNonZero = region.AsSpan().IndexOfAnyExcept((byte)0);
                Assert.AreEqual(-1, firstNonZero, "Expected all bytes to be zero after a zeroed allocation");
            }
            finally
            {
                allocator.Free(region);
            }
        }

        #endregion

        #region GetReference

        /// <summary>
        /// Verifies that <see cref="IPluginMemoryRegion.GetReference"/> rejects a 
        /// negative offset with <see cref="ArgumentOutOfRangeException"/> before 
        /// returning a reference.
        /// </summary>
        [TestMethod]
        public void GetReference_NegativeOffset_ThrowsArgumentOutOfRange()
        {
            using TrackedHeapWrapper heap = GetTestHeap();
            PluginSharedMemoryAllocator allocator = new(heap, false);
            IPluginMemoryRegion region = allocator.Alloc("test", 64);

            try
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => region.GetReference(-1));
            }
            finally
            {
                allocator.Free(region);
            }
        }

        /// <summary>
        /// Verifies that <see cref="IPluginMemoryRegion.GetReference"/> returns a reference
        /// that aliases the same backing byte as <see cref="IPluginMemoryRegion.AsSpan"/><c>[offset]</c>.
        /// Uses <see cref="Unsafe.ByteOffset"/> to confirm the address is exactly
        /// <c>offset</c> bytes from the start of the span.
        /// </summary>
        [TestMethod]
        public void GetReference_ValidOffset_ReturnsReferenceToCorrectByte()
        {
            const int TestOffset = 10;
            const byte TestValue = 0xAB;

            using TrackedHeapWrapper heap = GetTestHeap();

            // Zero the allocation so the written value is unambiguous
            PluginSharedMemoryAllocator allocator = new(heap, zeroAllocations: true);
            IPluginMemoryRegion region = allocator.Alloc("test", 64);

            try
            {
                // Write a sentinel at the target offset via the span
                region.AsSpan()[TestOffset] = TestValue;

                ref readonly byte r = ref region.GetReference(TestOffset);

                // Value round-trip: ref must read back what the span wrote
                Assert.AreEqual(TestValue, r, "GetReference should alias the same byte as AsSpan()[offset]");

                // Address check: distance from span-start must equal the requested offset
                ref readonly byte spanBase = ref MemoryMarshal.GetReference(region.AsSpan());

                nint byteOffset = Unsafe.ByteOffset(in spanBase, in r);

                Assert.AreEqual(TestOffset, (int)byteOffset, "GetReference address must be exactly 'offset' bytes from the span start");
            }
            finally
            {
                allocator.Free(region);
            }
        }

        /// <summary>
        /// Verifies that <see cref="IPluginMemoryRegion.GetReference"/> accepts
        /// the last valid offset (<c>Length - 1</c>) and rejects
        /// <c>Length</c> with <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        [TestMethod]
        public void GetReference_BoundaryOffsets()
        {
            using TrackedHeapWrapper heap = GetTestHeap();
            PluginSharedMemoryAllocator allocator = new(heap, false);
            IPluginMemoryRegion region = allocator.Alloc("test", 64);

            try
            {
                ref byte r = ref region.GetReference(region.Length - 1);
                r = 0xAB;
                Assert.AreEqual((byte)0xAB, region.AsSpan()[region.Length - 1]);

                // Cannot access memory outside of the buffer region.
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => region.GetReference(region.Length));
            }
            finally
            {
                allocator.Free(region);
            }
        }

        #endregion

        #region Pin

        /// <summary>
        /// Verifies that <see cref="IPluginMemoryRegion.Pin"/> and 
        /// <see cref="IPluginMemoryRegion.Unpin"/> complete without throwing for both 
        /// the first element and a mid-span offset.
        /// </summary>
        [TestMethod]
        public void Pin_ValidOffsets_RoundTripSucceeds()
        {
            using TrackedHeapWrapper heap = GetTestHeap();
            PluginSharedMemoryAllocator allocator = new(heap, false);
            IPluginMemoryRegion region = allocator.Alloc("test", 64);

            try
            {
                using MemoryHandle mh = region.Pin(0);

                using MemoryHandle mh2 = region.Pin(region.Length / 2);
            }
            finally
            {
                allocator.Free(region);
            }
        }

        /// <summary>
        /// Verifies that <see cref="IPluginMemoryRegion.Pin"/> accepts the last
        /// valid offset (<c>Length - 1</c>) and rejects <c>Length</c> with
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        [TestMethod]
        public void Pin_BoundaryOffsets()
        {
            using TrackedHeapWrapper heap = GetTestHeap();
            PluginSharedMemoryAllocator allocator = new(heap, false);
            IPluginMemoryRegion region = allocator.Alloc("test", 64);

            try
            {
                using MemoryHandle mh = region.Pin(region.Length - 1);

                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => region.Pin(region.Length));
            }
            finally
            {
                allocator.Free(region);
            }
        }

        #endregion
    }
}        
