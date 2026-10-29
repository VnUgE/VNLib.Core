/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginSharedMemoryRegistryTests.cs
*
* PluginSharedMemoryRegistryTests.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Memory.Diagnostics;
using VNLib.Plugins.Ipc.SharedMemory;
using VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc;

namespace VNLib.Plugins.Essentials.ServiceStack.Tests.Ipc
{
    [TestClass]
    public sealed class PluginSharedMemoryRegistryTests
    {
        private const int MinSize       = 64;
        private const int MaxSize       = 4096;
        private const int DefaultSize   = 256;
        private const string RegionName = "test-region";

        #region MapRegion

        /// <summary>
        /// Owner maps a region then releases it. With no accessors the heap must
        /// return to baseline immediately, confirming the ref-count free path works.
        /// </summary>
        [TestMethod]
        public void MapRegion_OwnerOnly_HeapReturnsToBaselineOnRelease()
        {
            using RegistryContext ctx = new();

            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);

            // One live block of the exact requested size
            HeapStatistics afterMap = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(1ul, afterMap.AllocatedBlocks);
            Assert.AreEqual((ulong)DefaultSize, afterMap.AllocatedBytes);

            ctx.Registry.ReleaseHandle(owner);

            // No accessors held a ref — memory must be freed on owner release
            HeapStatistics afterRelease = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(0ul, afterRelease.AllocatedBlocks);
            Assert.AreEqual(0ul, afterRelease.AllocatedBytes);
        }

        /// <summary>
        /// Verifies that mapping the same region name twice returns the same underlying
        /// region and shares the backing allocation. The second caller acts as an
        /// additional owner; memory must survive both individual releases and be freed
        /// only when the last handle is released.
        /// </summary>
        [TestMethod]
        public void MapRegion_SameName_ReturnsExistingRegionAndSharesMemory()
        {
            using RegistryContext ctx = new();

            IIpcRegionOwner first  = ctx.Registry.MapRegion(RegionName, DefaultSize);
            IIpcRegionOwner second = ctx.Registry.MapRegion(RegionName, DefaultSize);

            /* Both owners must reference the same underlying region object and
             * the allocator must still report exactly one live block. */
            Assert.AreSame(first.Region, second.Region);
            Assert.AreEqual(1ul, ctx.Heap.GetCurrentStats().AllocatedBlocks);

            ctx.Registry.ReleaseHandle(first);

            // Second owner still holds a ref — block must survive the first release
            Assert.AreEqual(1ul, ctx.Heap.GetCurrentStats().AllocatedBlocks);

            ctx.Registry.ReleaseHandle(second);

            // All refs dropped — memory must now be freed
            Assert.AreEqual(0ul, ctx.Heap.GetCurrentStats().AllocatedBlocks);
        }

        /*
         * NOTE: This test asserts the current throw-on-mismatch behavior, which is
         * an unresolved TODO (see MapRegion). It is not a canonical feature
         * assertion. If the decision is made to allow mismatched sizes or
         * otherwise change the behavior, this test must be updated accordingly.
         */
        /// <summary>
        /// Verifies that mapping the same region name with a different size throws
        /// <see cref="InvalidOperationException"/>, rejecting the second producer's
        /// mismatched allocation rather than silently returning the existing region.
        /// </summary>
        [TestMethod]
        public void MapRegion_SameNameDifferentSize_ThrowsInvalidOperationException()
        {
            const int firstSize = DefaultSize;     // 256
            const int secondSize = DefaultSize * 2; // 512

            using RegistryContext ctx = new();

            IIpcRegionOwner first = ctx.Registry.MapRegion(RegionName, firstSize);

            // Second mapping with a different size must throw
            Assert.ThrowsExactly<InvalidOperationException>(() => ctx.Registry.MapRegion(RegionName, secondSize));

            // The first region remains valid with its original size
            Assert.AreEqual(firstSize, first.Region.Length);

            // Only one allocation should exist
            Assert.AreEqual(1ul, ctx.Heap.GetCurrentStats().AllocatedBlocks);

            ctx.Registry.ReleaseHandle(first);

            Assert.AreEqual(0ul, ctx.Heap.GetCurrentStats().AllocatedBlocks);
        }

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.MapRegion"/> rejects a 
        /// size above <see cref="PluginSharedMemoryConfig.MaxRegionSize"/> and below
        /// <see cref="PluginSharedMemoryConfig.MinRegionSize"/>
        /// </summary>
        [TestMethod]
        public void MapRegion_SizeOutOfBounds_ThrowsArgumentOutOfRange()
        {
            using RegistryContext ctx = new();

            // Below min size and above max size
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ctx.Registry.MapRegion(RegionName, MinSize - 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ctx.Registry.MapRegion(RegionName, MaxSize + 1));
        }

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.MapRegion"/> accepts a
        /// size equal to <see cref="PluginSharedMemoryConfig.MinRegionSize"/> 
        /// and one equal to <see cref="PluginSharedMemoryConfig.MaxRegionSize"/> exercising 
        /// boundaries.
        /// </summary>
        [TestMethod]
        public void MapRegion_SizeAtBoundaries_Succeeds()
        {
            using RegistryContext ctx = new();

            // min size
            {
                IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, MinSize);

                Assert.AreEqual(MinSize, owner.Region.Length);

                ctx.Registry.ReleaseHandle(owner);               
            }

            // max size
            {
                IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, MaxSize);

                Assert.AreEqual(MaxSize, owner.Region.Length);                

                ctx.Registry.ReleaseHandle(owner);
            }

            Assert.AreEqual(0ul, ctx.Heap.GetCurrentStats().AllocatedBytes);
        }

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.MapRegion"/> rejects 
        /// calls after the registry has been disposed.
        /// </summary>
        [TestMethod]
        public void MapRegion_WhenDisposed_ThrowsObjectDisposedException()
        {
            RegistryContext ctx = new();
            ctx.Dispose();

            // Registry must reject calls after disposal
            Assert.ThrowsExactly<ObjectDisposedException>(() => ctx.Registry.MapRegion(RegionName, DefaultSize));
        }

        #endregion

        #region AddReader — active accessor

        /// <summary>
        /// Accessor calls <c>AddReader</c> after the producer has already mapped.
        /// The accessor must immediately report valid, and <c>WaitAsync</c> must
        /// return an already-completed task with the correct region.
        /// </summary>
        [TestMethod]
        public void AddReader_AfterMap_AccessorIsImmediatelyValid()
        {
            using RegistryContext ctx = new();

            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);
            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            Assert.IsTrue(accessor.IsValid());                              // active accessor must see region as valid immediately
            Assert.AreSame(owner.Region, accessor.GetRegion());             // must reference the exact same region object as the owner
            Assert.IsTrue(accessor.WaitAsync().IsCompleted);                // already-mapped region yields a completed task

            ctx.Registry.ReleaseHandle(accessor);
            ctx.Registry.ReleaseHandle(owner);
        }

        /// <summary>
        /// Verifies that region name lookups are case-insensitive. A producer maps
        /// a region with mixed casing and a reader adds with different casing; the
        /// reader must resolve to the already-mapped region (active accessor), not
        /// a pending accessor.
        /// </summary>
        [TestMethod]
        public void AddReader_MixedCaseName_ResolvesToExistingRegion()
        {
            using RegistryContext ctx = new();

            IIpcRegionOwner owner = ctx.Registry.MapRegion("TestRegion", DefaultSize);
            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader("testregion");

            // Must be an active accessor — region was already mapped under a different case
            Assert.IsTrue(accessor.IsValid());
            Assert.AreSame(owner.Region, accessor.GetRegion());
            Assert.IsTrue(accessor.WaitAsync().IsCompleted);

            ctx.Registry.ReleaseHandle(accessor);
            ctx.Registry.ReleaseHandle(owner);
        }

        /// <summary>
        /// Verifies that a pending accessor added with different casing than the
        /// producer's mapping is resolved when the region is subsequently mapped.
        /// </summary>
        [TestMethod]
        public async Task AddReader_MixedCaseName_PendingAccessorResolvesOnMap()
        {
            using RegistryContext ctx = new();

            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader("testregion");
            Assert.IsFalse(accessor.IsValid());  // pending — not yet mapped

            IIpcRegionOwner owner = ctx.Registry.MapRegion("TestRegion", DefaultSize);

            // Pending accessor must be resolved by the mapping
            Assert.IsTrue(accessor.IsValid());
            Assert.AreSame(owner.Region, accessor.GetRegion());

            await accessor.WaitAsync();

            ctx.Registry.ReleaseHandle(accessor);
            ctx.Registry.ReleaseHandle(owner);
        }

        /// <summary>
        /// Owner and one active accessor both release. The heap must only be freed 
        /// after the second (last) release — not after the first.
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_OwnerThenLateReader_MemoryFreedOnLastRelease()
        {
            using RegistryContext ctx = new();

            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);
            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            ctx.Registry.ReleaseHandle(owner);

            // Accessor still holds a ref — block must remain live
            HeapStatistics afterOwnerRelease = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(1ul, afterOwnerRelease.AllocatedBlocks);        // accessor's ref keeps memory alive

            ctx.Registry.ReleaseHandle(accessor);

            // All refs dropped — allocator must have reclaimed the block
            HeapStatistics afterReaderRelease = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(0ul, afterReaderRelease.AllocatedBlocks);       // last ref gone, memory freed
        }

        /// <summary>
        /// Maps a region then adds two accessors. Releasing the owner and then each
        /// accessor in turn verifies that memory is freed only when the last reference
        /// is released — confirming correct ref-count accounting across three holders.
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_MultipleReaders_MemoryFreedOnLastRelease()
        {
            using RegistryContext ctx = new();

            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);
            IPluginMemoryRegionAccessor accessor1 = ctx.Registry.AddReader(RegionName);
            IPluginMemoryRegionAccessor accessor2 = ctx.Registry.AddReader(RegionName);

            ctx.Registry.ReleaseHandle(owner);

            // accessor1 and accessor2 still hold refs — block must remain live
            HeapStatistics afterOwnerRelease = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(1ul, afterOwnerRelease.AllocatedBlocks);        // two accessors still hold refs, memory must survive

            ctx.Registry.ReleaseHandle(accessor1);

            // accessor2 still holds a ref — block must still be live
            HeapStatistics afterAccessor1Release = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(1ul, afterAccessor1Release.AllocatedBlocks);      // accessor2 still holds a ref, memory must survive

            ctx.Registry.ReleaseHandle(accessor2);

            // All refs dropped — allocator must have reclaimed the block
            HeapStatistics afterAccessor2Release = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(0ul, afterAccessor2Release.AllocatedBlocks);      // last ref gone, memory freed
        }

        #endregion

        #region AddReader — pending accessor

        /// <summary>
        /// Accessor registers before the producer maps.After the producer calls 
        /// <c>MapRegion</c>, the accessor's <c>WaitAsync</c> task must complete and 
        /// the accessor must become valid.
        /// </summary>
        [TestMethod]
        public async Task AddReader_BeforeMap_AccessorBecomesValidAfterMap()
        {
            using RegistryContext ctx = new();

            // Pending accessor registers before the producer maps
            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            // Region not yet mapped — accessor must report invalid
            Assert.IsFalse(accessor.IsValid(), "Accessor must be invalid before MapRegion is called");

            // Producer maps the region, which notifies all pending accessors
            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);

            // Cold task completes asynchronously on the thread pool after OnRegionMapped
            await accessor.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

            // Accessor must now report valid after the notification completes
            Assert.IsTrue(accessor.IsValid(), "Accessor must be valid after MapRegion notifies it");

            // Region returned by the accessor must be the exact same object as the owner's region
            Assert.AreSame(owner.Region, accessor.GetRegion(), "Accessor region must be the same reference as the owner region");

            ctx.Registry.ReleaseHandle(accessor);
            ctx.Registry.ReleaseHandle(owner);
        }

        /// <summary>
        /// Pending accessor registers but exits (calls <c>ReleaseHandle</c>) before 
        /// the producer ever maps. The release must be a no-op — it must not 
        /// decrement any ref count or affect the producer's ability to map later.
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_EarlyReaderBeforeMap_IsNoOp()
        {
            using RegistryContext ctx = new();

            // Pending accessor registers before the producer maps
            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            // Accessor exits before producer maps — must be a no-op (no ref count to decrement yet)
            ctx.Registry.ReleaseHandle(accessor);

            // Producer maps normally — the stale pending accessor release must not interfere
            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);

            // Exactly one live block; pending accessor release must not have skewed the ref count
            HeapStatistics afterMap = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(1ul, afterMap.AllocatedBlocks, "Exactly one block must be live after MapRegion");

            ctx.Registry.ReleaseHandle(owner);

            // Owner was the sole ref holder — memory must be freed after its release
            HeapStatistics afterRelease = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(0ul, afterRelease.AllocatedBlocks, "Heap must be empty after owner releases");
        }

        /// <summary>
        /// Owner maps a region, notifying pending accessors. Then both 
        /// owner and accessor release. The heap must be freed only after the last 
        /// release, verifying that pending accessors correctly contribute to the 
        /// ref count.
        /// </summary>
        [TestMethod]
        public async Task ReleaseHandle_OwnerThenEarlyReader_MemoryFreedOnLastRelease()
        {
            using RegistryContext ctx = new();

            // Pending accessor registers before producer maps
            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            // Producer maps — increments pending accessor's ref count and starts its task
            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);

            // Wait for the notification task to complete before asserting anything
            await accessor.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

            ctx.Registry.ReleaseHandle(owner);

            // Pending accessor's ref keeps the block alive after the owner exits
            HeapStatistics afterOwner = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(1ul, afterOwner.AllocatedBlocks, "Memory must survive owner release while pending accessor holds a ref");

            ctx.Registry.ReleaseHandle(accessor);

            // Pending accessor was the last holder — memory must now be freed
            HeapStatistics afterReader = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(0ul, afterReader.AllocatedBlocks, "Heap must return to baseline after last holder (pending accessor) releases");
        }

        /// <summary>
        /// A pending accessor is released after <see cref="MapRegion"/> notifies it but
        /// before its <see cref="IPluginMemoryRegionAccessor.WaitAsync"/> task completes
        /// on the thread pool. This exercises the race between the notification task
        /// startup, the ref-count increment inside <see cref="MapRegion"/>, and the
        /// ref-count decrement in <see cref="ReleaseHandle"/>. The release must
        /// correctly decrement the ref count (since MapRegion already incremented it
        /// and removed the accessor from the pending list), and the memory must remain
        /// alive until the owner also releases.
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_PendingAccessorAfterMapBeforeWait_RefsCountedCorrectly()
        {
            using RegistryContext ctx = new();

            // Pending accessor registers before producer maps
            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            // Producer maps — increments ref count for both owner and accessor,
            // and starts the accessor's notification task on the thread pool
            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);

            // Release the accessor immediately, before its WaitAsync task completes.
            // MapRegion already incremented the ref count and removed it from the
            // pending list, so ReleaseHandle must decrement the mapped region's ref count
            // (not treat it as a pending-accessor no-op).
            ctx.Registry.ReleaseHandle(accessor);

            // Only the owner holds a ref now — memory must still be alive
            HeapStatistics afterAccessorRelease = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(
                expected: 1ul, 
                afterAccessorRelease.AllocatedBlocks,
                message: "Memory must survive accessor release while owner still holds a ref"
            );

            // Owner releases — last holder, memory must be freed
            ctx.Registry.ReleaseHandle(owner);

            HeapStatistics afterOwnerRelease = ctx.Heap.GetCurrentStats();
            Assert.AreEqual(
                expected: 0ul, 
                afterOwnerRelease.AllocatedBlocks,
                message: "Heap must return to baseline after last holder (owner) releases"
            );
        }

        #endregion

        #region ReleaseHandle — error / edge cases

        /// <summary>
        /// Releasing the same owner handle a second time must throw
        /// <see cref="ArgumentException"/> (double-free guard).
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_DoubleOwnerRelease_ThrowsArgumentException()
        {
            using RegistryContext ctx = new();

            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);
            ctx.Registry.ReleaseHandle(owner);                              // first release succeeds; no readers so memory is freed

            // Second release must be rejected by the double-free guard
            Assert.ThrowsExactly<ArgumentException>(() => ctx.Registry.ReleaseHandle(owner));
        }

        /// <summary>
        /// Releasing the same accessor handle a second time must throw
        /// <see cref="ArgumentException"/> (double-free guard).
        /// </summary>
        [TestMethod]
        public async Task ReleaseHandle_DoubleAccessorRelease_ThrowsArgumentException()
        {
            using RegistryContext ctx = new();

            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);
            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            await accessor.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

            ctx.Registry.ReleaseHandle(accessor);
            ctx.Registry.ReleaseHandle(owner);

            // Second accessor release must be rejected by the double-free guard
            Assert.ThrowsExactly<ArgumentException>(() => ctx.Registry.ReleaseHandle(accessor));
        }

        /// <summary>
        /// Releasing a pending accessor (never mapped) a second time must throw
        /// <see cref="ArgumentException"/> (double-free guard).
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_DoublePendingAccessorRelease_ThrowsArgumentException()
        {
            using RegistryContext ctx = new();

            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            // Release before the region is ever mapped — must be a no-op
            ctx.Registry.ReleaseHandle(accessor);

            // Second release must be rejected by the double-free guard
            Assert.ThrowsExactly<ArgumentException>(() => ctx.Registry.ReleaseHandle(accessor));
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Disposing the registry while a region is still mapped must forcibly free
        /// the underlying allocation — verifying the <c>Free()</c> sweep path.
        /// </summary>
        [TestMethod]
        public void Dispose_WithActiveRegion_ForceFreesAllMemory()
        {
            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryAllocator allocator = new(heap, zeroAllocations: false);
            PluginSharedMemoryRegistry registry = new(new PluginSharedMemoryConfig
            {
                Allocator       = allocator,
                MinRegionSize   = MinSize,
                MaxRegionSize   = MaxSize,
            });

            // Map two regions but intentionally skip ReleaseHandle to simulate leaked handles
            registry.MapRegion("region-a", DefaultSize);
            registry.MapRegion("region-b", DefaultSize);

            // Both blocks must be live before the registry is disposed
            HeapStatistics beforeDispose = heap.GetCurrentStats();
            Assert.AreEqual(2ul, beforeDispose.AllocatedBlocks);            // two mapped regions, two live blocks

            registry.Dispose();

            // Force-free sweep must have returned all blocks to the allocator
            HeapStatistics afterDispose = heap.GetCurrentStats();
            Assert.AreEqual(0ul, afterDispose.AllocatedBlocks);             // dispose force-freed every remaining region
        }

        /// <summary>
        /// Disposing the registry while a pending accessor is still waiting for
        /// its region to be mapped must cancel the accessor's <see cref="IPluginMemoryRegionAccessor.WaitAsync"/>
        /// task via the registry's cancellation token, rather than leaving it
        /// pending forever. No memory was ever allocated for the unmapped region,
        /// so the heap must remain at baseline.
        /// </summary>
        [TestMethod]
        public async Task Dispose_WithPendingAccessor_CancelsWaitAsync()
        {
            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryAllocator allocator = new(heap, zeroAllocations: false);            
            PluginSharedMemoryRegistry registry = new(new PluginSharedMemoryConfig
            {
                Allocator       = allocator,
                MinRegionSize   = MinSize,
                MaxRegionSize   = MaxSize,
            });

            // Register a pending accessor whose region is never mapped
            IPluginMemoryRegionAccessor accessor = registry.AddReader("never-mapped");

            Assert.IsFalse(accessor.IsValid());

            // Dispose the registry — Free() must cancel the pending accessor's task
            registry.Dispose();

            // WaitAsync must observe the cancellation and throw TaskCanceledException
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(accessor.WaitAsync);

            // No region was ever allocated, so the heap must be empty
            Assert.AreEqual(0ul, heap.GetCurrentStats().AllocatedBlocks);

            heap.Dispose();
        }

        #endregion

        #region Input validation

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.MapRegion"/> rejects
        /// null and whitespace-only region names.
        /// </summary>
        [TestMethod]
        public void MapRegion_InvalidName_ThrowsArgumentException()
        {
            using RegistryContext ctx = new();

            Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Registry.MapRegion(null!, DefaultSize));
            Assert.ThrowsExactly<ArgumentException>(() => ctx.Registry.MapRegion("   ", DefaultSize));
        }

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.AddReader"/> rejects
        /// null and whitespace-only region names.
        /// </summary>
        [TestMethod]
        public void AddReader_InvalidName_ThrowsArgumentException()
        {
            using RegistryContext ctx = new();

            Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Registry.AddReader(null!));
            Assert.ThrowsExactly<ArgumentException>(() => ctx.Registry.AddReader("   "));
        }

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.AddReader"/> rejects
        /// calls after the registry has been disposed.
        /// </summary>
        [TestMethod]
        public void AddReader_WhenDisposed_ThrowsObjectDisposedException()
        {
            RegistryContext ctx = new();
            ctx.Dispose();

            // Registry must reject calls after disposal
            Assert.ThrowsExactly<ObjectDisposedException>(() => ctx.Registry.AddReader(RegionName)); // disposed registry must throw
        }

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.ReleaseHandle"/> rejects
        /// a null handle with <see cref="ArgumentNullException"/>.
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_NullHandle_ThrowsArgumentNullException()
        {
            using RegistryContext ctx = new();

            Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Registry.ReleaseHandle(null!)); // null handle must be rejected
        }

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.ReleaseHandle"/> rejects
        /// an unrecognized handle type with <see cref="ArgumentException"/>.
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_InvalidHandleType_ThrowsArgumentException()
        {
            using RegistryContext ctx = new();

            Assert.ThrowsExactly<ArgumentException>(() => ctx.Registry.ReleaseHandle(new object())); // unrecognized handle type must be rejected
        }

        /// <summary>
        /// Verifies that <see cref="PluginSharedMemoryRegistry.ReleaseHandle"/> rejects
        /// calls after the registry has been disposed.
        /// </summary>
        [TestMethod]
        public void ReleaseHandle_WhenDisposed_ThrowsObjectDisposedException()
        {
            RegistryContext ctx = new();
            IIpcRegionOwner owner = ctx.Registry.MapRegion(RegionName, DefaultSize);

            ctx.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(() => ctx.Registry.ReleaseHandle(owner));
        }

        /// <summary>
        /// Verifies that calling <see cref="IPluginMemoryRegionAccessor.GetRegion"/> on a pending
        /// accessor before its region has been mapped throws
        /// <see cref="InvalidOperationException"/>. Callers must guard with
        /// <see cref="IPluginMemoryRegionAccessor.IsValid"/> or await
        /// <see cref="IPluginMemoryRegionAccessor.WaitAsync"/> before calling
        /// <c>GetRegion</c> to avoid this.
        /// </summary>
        [TestMethod]
        public void GetRegion_BeforeMap_ThrowsInvalidOperationException()
        {
            using RegistryContext ctx = new();

            IPluginMemoryRegionAccessor accessor = ctx.Registry.AddReader(RegionName);

            // Region has not been mapped yet — GetRegion must throw rather than return null
            Assert.ThrowsExactly<InvalidOperationException>(accessor.GetRegion);

            ctx.Registry.ReleaseHandle(accessor);
        }

        #endregion

        /*
         * Bundles the registry with its backing heap so tests can assert memory
         * accounting alongside registry behavior. Disposing releases the registry
         * first (triggering any force-free sweep), then the heap wrapper.
         */
        private sealed class RegistryContext : VnDisposeable
        {
            public readonly PluginSharedMemoryRegistry Registry;
            public readonly TrackedHeapWrapper Heap;

            public RegistryContext()
            {
                Heap = new TrackedHeapWrapper(MemoryUtil.Shared, false);
                PluginSharedMemoryAllocator allocator = new(Heap, zeroAllocations: false);

                Registry = new PluginSharedMemoryRegistry(new PluginSharedMemoryConfig
                {
                    Allocator       = allocator,
                    MinRegionSize   = MinSize,
                    MaxRegionSize   = MaxSize,
                });
            }

            protected override void Free()
            {
                Registry.Dispose();
                Heap.Dispose();
            }
        }
    }
}
