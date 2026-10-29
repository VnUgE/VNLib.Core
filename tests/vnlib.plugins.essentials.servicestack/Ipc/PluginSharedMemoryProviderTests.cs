/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginSharedMemoryProviderTests.cs
*
* PluginSharedMemoryProviderTests.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Memory.Diagnostics;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Runtime.Events;
using VNLib.Plugins.Ipc.SharedMemory;
using VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc;
using System.Linq;

namespace VNLib.Plugins.Essentials.ServiceStack.Tests.Ipc
{
    [TestClass]
    public sealed class PluginSharedMemoryProviderTests
    {
        #region Helpers

        private static ProviderContext GetTestProvider()
        {
            TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 64,
                MaxRegionSize   = 65536
            };
            return new ProviderContext(new PluginSharedMemoryProvider(config), heap);
        }

        /*
         * Returns an unloaded context with the IPC listener registered and test plugins
         * discovered from the current assembly. Callers must invoke LoadPlugins() to
         * trigger OnBeforeLoading/Load/OnPluginLoaded.
         */
        private static LoadedContext GetContext()
        {
            ProviderContext ctx = GetTestProvider();
            RuntimePluginLoader loader = new(
                new TestPluginLoadConfig(Assembly.GetExecutingAssembly()),
                null
            );

            loader.Controller.Register(ctx.Provider.GetListener());          

            return new LoadedContext(ctx, loader);
        }      

        #endregion

        #region Constructor validation

        /// <summary>
        /// Verifies that a null config throws <see cref="ArgumentNullException"/>.
        /// </summary>
        [TestMethod]
        public void Constructor_NullConfig_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new PluginSharedMemoryProvider(null!));
        }

        /// <summary>
        /// Verifies that a null allocator in the config throws <see cref="ArgumentNullException"/>.
        /// </summary>
        [TestMethod]
        public void Constructor_NullAllocator_ThrowsArgumentNullException()
        {
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = null!,
                MinRegionSize   = 64,
                MaxRegionSize   = 65536
            };

            Assert.ThrowsExactly<ArgumentNullException>(() => new PluginSharedMemoryProvider(config));
        }

        /// <summary>
        /// Verifies that a MinRegionSize less than 1 throws <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        [TestMethod]
        public void Constructor_MinRegionSizeLessThanOne_ThrowsArgumentOutOfRangeException()
        {
            TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 0,
                MaxRegionSize   = 65536
            };

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PluginSharedMemoryProvider(config));
        }

        /// <summary>
        /// Verifies that a MaxRegionSize less than MinRegionSize throws <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        [TestMethod]
        public void Constructor_MaxRegionSizeLessThanMin_ThrowsArgumentOutOfRangeException()
        {
            TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 4096,
                MaxRegionSize   = 64
            };

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PluginSharedMemoryProvider(config));
        }

        #endregion

        #region Provider surface

        /// <summary>
        /// Verifies that GetListener returns a non-null listener instance.
        /// </summary>
        [TestMethod]
        public void GetListener_ReturnsNonNullListener()
        {
            using ProviderContext ctx = GetTestProvider();

            IPluginEventListener listener = ctx.Provider.GetListener();

            Assert.IsNotNull(listener);
        }

        /// <summary>
        /// Verifies the dispose guard is enforced on <see cref="PluginSharedMemoryProvider.GetListener"/>.
        /// </summary>
        [TestMethod]
        public void GetListener_WhenDisposed_ThrowsObjectDisposedException()
        {
            ProviderContext ctx = GetTestProvider();
            ctx.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(ctx.Provider.GetListener);
        }

        #endregion

        #region OnBeforeLoading — alloc path

        /// <summary>
        /// Verifies that a plugin decorated with <see cref="SharedRegionAllocAttribute"/>
        /// has its <see cref="IPluginMemoryRegion"/> property injected before
        /// <see cref="IPlugin.Load"/> is called.
        /// </summary>
        [TestMethod]
        public void OnBeforeLoading_AllocAttribute_SetsRegionPropertyOnPlugin()
        {
            using LoadedContext ctx = GetContext();
            
            ctx.LoadPlugins();

            AllocPlugin plugin = ctx.GetPlugin<AllocPlugin>();

            Assert.IsNotNull(plugin.Region);
        }

        /// <summary>
        /// Verifies exactly one heap block is allocated for the single alloc-attributed
        /// plugin. <c>OpenPlugin</c> (accessor) and <c>NoAttrPlugin</c> do not allocate.
        /// </summary>
        [TestMethod]
        public void OnBeforeLoading_AllocAttribute_AllocatesOneBlock()
        {
            using LoadedContext ctx = GetContext();
            ctx.LoadPlugins();

            HeapStatistics stats = ctx.ProviderCtx.Heap.GetCurrentStats();
            Assert.AreEqual(1ul, stats.AllocatedBlocks);
        }

        #endregion

        #region OnBeforeLoading — open path

        /// <summary>
        /// Verifies that a plugin decorated with <see cref="SharedRegionOpenAttribute"/>
        /// has its <see cref="IPluginMemoryRegionAccessor"/> property injected.
        /// Processing order of <c>asm.GetTypes()</c> is non-deterministic — the open
        /// plugin may be a pending or active accessor — but the accessor is always assigned.
        /// </summary>
        [TestMethod]
        public void OnBeforeLoading_OpenAttribute_SetsAccessorPropertyOnPlugin()
        {
            using LoadedContext ctx = GetContext();
            ctx.LoadPlugins();

            OpenPlugin plugin = ctx.GetPlugin<OpenPlugin>();

            Assert.IsNotNull(plugin.Accessor);
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Verifies that disposing the provider directly — without calling
        /// <see cref="LoadedContext.UnloadPlugins"/> first — still frees every
        /// allocated region via <see cref="PluginSharedMemoryRegistry"/>'s
        /// force-free sweep. This exercises the <c>Free()</c> path rather than
        /// the normal <c>OnAfterUnloaded</c> cleanup path.
        /// </summary>
        [TestMethod]
        public void Dispose_WithActiveRegionsAndNoUnload_ForceFreesAllMemory()
        {
            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator     = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize = 64,
                MaxRegionSize = 65536,
            };

            PluginSharedMemoryProvider provider = new(config); // intentionally not using — testing explicit dispose path

            using RuntimePluginLoader loader = new(
                new TestPluginLoadConfig(Assembly.GetExecutingAssembly()),
                null
            );

            loader.Controller.Register(provider.GetListener());
            loader.InitializeController();
            loader.LoadPlugins();

            // AllocPlugin allocated exactly one block
            Assert.AreEqual(1ul, heap.GetCurrentStats().AllocatedBlocks);

            // Dispose the provider directly — registry.Free() must sweep remaining regions
            provider.Dispose();

            Assert.AreEqual(0ul, heap.GetCurrentStats().AllocatedBlocks);
        }

        #endregion

        #region OnAfterUnloaded

        /// <summary>
        /// Verifies that <c>OnAfterUnloaded</c> releases all allocated region handles
        /// so the heap returns to zero.
        /// </summary>
        [TestMethod]
        public void OnAfterUnloaded_WithAllocatedRegion_HeapReturnsToBaseline()
        {
            using LoadedContext ctx = GetContext();
            ctx.LoadPlugins();
            ctx.UnloadPlugins();

            HeapStatistics stats = ctx.ProviderCtx.Heap.GetCurrentStats();
            Assert.AreEqual(0ul, stats.AllocatedBlocks);
        }

        /// <summary>
        /// Verifies that plugins carrying no IPC attributes are silently skipped by
        /// <c>OnAfterUnloaded</c> without error, and that all allocated regions from
        /// the other plugins are still correctly freed.
        /// </summary>
        [TestMethod]
        public void OnAfterUnloaded_PluginsWithNoAttributes_SilentlySkipped()
        {
            using LoadedContext ctx = GetContext();

            ctx.LoadPlugins();
            ctx.UnloadPlugins();

            HeapStatistics stats = ctx.ProviderCtx.Heap.GetCurrentStats();
            Assert.AreEqual(0ul, stats.AllocatedBlocks);
        }

        /// <summary>
        /// Verifies that calling <c>UnloadPlugins</c> without a prior <c>LoadPlugins</c>
        /// is a safe no-op. No plugins are registered, so <c>_plugins</c> is empty and
        /// the second call processes nothing.
        /// </summary>
        [TestMethod]
        public void OnAfterUnloaded_WithoutPriorLoad_IsNoOp()
        {
            using LoadedContext ctx = GetContext();

            ctx.UnloadPlugins();
            ctx.UnloadPlugins();

            HeapStatistics stats = ctx.ProviderCtx.Heap.GetCurrentStats();
            Assert.AreEqual(0ul, stats.AllocatedBlocks);
        }

        #endregion

        #region IPC data sharing

        /// <summary>
        /// Verifies that the accessor is valid immediately after loading completes.
        /// Because alloc attributes are processed before open attributes within
        /// <see cref="PluginSharedMemoryProvider"/>, the region is always mapped
        /// before the accessor is registered, making the accessor an active accessor
        /// whose <see cref="IPluginMemoryRegionAccessor.IsValid"/> is always true
        /// upon return from <see cref="LoadedContext.LoadPlugins"/>.
        /// </summary>
        [TestMethod]
        public void SharedRegion_AfterLoad_AccessorIsValid()
        {
            using LoadedContext ctx = GetContext();
            ctx.LoadPlugins();

            OpenPlugin openPlugin = ctx.GetPlugin<OpenPlugin>();

            Assert.IsTrue(openPlugin.Accessor!.IsValid());
        }

        /// <summary>
        /// Verifies that <see cref="IPluginMemoryRegionAccessor.GetRegion"/> returns the exact
        /// same <see cref="IPluginMemoryRegion"/> instance held by the owner plugin, confirming
        /// the registry wires both plugins to the same backing allocation. Pointer identity is
        /// asserted through both <see cref="IPluginMemoryRegion.GetReference(int)"/> and
        /// <see cref="IPluginMemoryRegion.AsSpan()"/> to cover both access paths.
        /// </summary>
        [TestMethod]
        public void SharedRegion_GetRegion_ReturnsSameRegionAsOwner()
        {
            using LoadedContext ctx = GetContext();
            ctx.LoadPlugins();

            AllocPlugin allocPlugin = ctx.GetPlugin<AllocPlugin>();
            OpenPlugin openPlugin   = ctx.GetPlugin<OpenPlugin>();

            IPluginMemoryRegion ownerRegion  = allocPlugin.Region!;
            IPluginMemoryRegion accessorRegion = openPlugin.Accessor!.GetRegion();

            // Test references are actually the same object and not just identical copies
            // NOTE: This may change in the future if wrappers are used to enforce new rules.
            Assert.AreSame(ownerRegion, accessorRegion);

            Assert.IsTrue(Unsafe.AreSame(
                ref ownerRegion.GetReference(0),
                ref accessorRegion.GetReference(0))
            );

            Assert.IsTrue(Unsafe.AreSame(
                ref ownerRegion.AsSpan()[0],
                ref accessorRegion.AsSpan()[0])
            );
        }

        #endregion

        #region Reserved Regions

        /// <summary>
        /// Verifies that a plugin decorated with <see cref="SharedRegionOpenAttribute"/>
        /// targeting a host-reserved region receives an immediately-valid accessor with
        /// the correct region size after <c>LoadPlugins</c>.
        /// </summary>
        [TestMethod]
        public void ReservedRegion_PluginOpensReservedRegion_AccessorIsValid()
        {
            const int regionSize = 256;

            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 64,
                MaxRegionSize   = 4096,
                HostReservations = [
                    new PluginSharedMemoryHostReservation("host_reserved_region", regionSize)
                ]
            };

            using PluginSharedMemoryProvider provider = new(config);

            using RuntimePluginLoader loader = new(
                new TestPluginLoadConfig(Assembly.GetExecutingAssembly()),
                null
            );

            loader.Controller.Register(provider.GetListener());
            loader.InitializeController();
            loader.LoadPlugins();
        
            ReservedOpenPlugin? plugin = loader.Controller.GetPlugin<ReservedOpenPlugin>();
            Assert.IsNotNull(plugin);

            // Region should be valid immediately 
            Assert.IsNotNull(plugin.Accessor);
            Assert.IsTrue(plugin.Accessor.IsValid());
            Assert.IsNotNull(plugin.Accessor.GetRegion());

            IPluginMemoryRegion region = plugin.Accessor.GetRegion();
            Assert.AreEqual(regionSize, region.Length);

            loader.UnloadAll(false);
        }

        [TestMethod]
        public void ReservedRegionIsAllocated()
        {
            const int testRegionSize = 64;

            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 64,
                MaxRegionSize   = 4096,
                HostReservations = [ 
                    new PluginSharedMemoryHostReservation("test_region", testRegionSize) 
                ]
            };

            Assert.AreEqual(0ul, heap.GetCurrentStats().AllocatedBlocks);

            // Construction should validate and pre-allocate reserved regions
            using (PluginSharedMemoryProvider provider = new(config))
            {
                // ensure reserved regions get allocated
                HeapStatistics stats = heap.GetCurrentStats();
                uint reservations = (uint)config.HostReservations.Count();

                Assert.AreEqual(
                    reservations,
                    stats.AllocatedBlocks,
                    "Expected the number of allocated blocks to equal the number of reservations"
                );

                Assert.AreEqual(testRegionSize * reservations, stats.AllocatedBytes);
            }

            // Ensure all regions are unmapped again
            Assert.AreEqual(0ul, heap.GetCurrentStats().AllocatedBlocks);

        }

        /// <summary>
        /// Verifies that multiple host-reserved regions are all allocated with the
        /// correct total block count and byte total, and that all are released on dispose.
        /// </summary>
        [TestMethod]
        public void ReservedRegion_MultipleRegions_AllAllocatedAndReleased()
        {
            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 64,
                MaxRegionSize   = 4096,
                HostReservations = [
                    new PluginSharedMemoryHostReservation("region_a", 128),
                    new PluginSharedMemoryHostReservation("region_b", 256),
                    new PluginSharedMemoryHostReservation("region_c", 512)
                ]
            };

            using (PluginSharedMemoryProvider provider = new(config))
            {
                HeapStatistics stats = heap.GetCurrentStats();
                Assert.AreEqual(3ul, stats.AllocatedBlocks);
                Assert.AreEqual((uint)128 + 256 + 512, stats.AllocatedBytes);
            }

            Assert.AreEqual(0ul, heap.GetCurrentStats().AllocatedBlocks);
        }

        /// <summary>
        /// Verifies that providing duplicate region names in <c>HostReservations</c>
        /// throws <see cref="ArgumentException"/> from the dictionary construction.
        /// </summary>
        [TestMethod]
        public void ReservedRegion_DuplicateNames_ThrowsArgumentException()
        {
            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 64,
                MaxRegionSize   = 4096,
                HostReservations = [
                    new PluginSharedMemoryHostReservation("same_name", 128),
                    new PluginSharedMemoryHostReservation("same_name", 256)
                ]
            };

            Assert.ThrowsExactly<ArgumentException>(() => new PluginSharedMemoryProvider(config));
        }

        /// <summary>
        /// Verifies that host-reserved regions and plugin-allocated regions coexist
        /// without interference, and that the dispose ordering (ReleaseHandle on
        /// reserved handles, then registry sweep for plugin regions) correctly
        /// frees all memory without double-free.
        /// </summary>
        [TestMethod]
        public void ReservedRegion_CoexistsWithPluginAlloc_DisposeFreesAll()
        {
            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 64,
                MaxRegionSize   = 65536,
                HostReservations = [
                    new PluginSharedMemoryHostReservation("host_reserved_region", 256)
                ]
            };

            using (PluginSharedMemoryProvider provider = new(config))
            {
                using RuntimePluginLoader loader = new(
                    new TestPluginLoadConfig(Assembly.GetExecutingAssembly()),
                    null
                );

                loader.Controller.Register(provider.GetListener());
                loader.InitializeController();
                loader.LoadPlugins();

                // 1 reserved + 1 plugin-allocated (AllocPlugin) = 2 blocks
                Assert.AreEqual(2ul, heap.GetCurrentStats().AllocatedBlocks);              
            }

            Assert.AreEqual(0ul, heap.GetCurrentStats().AllocatedBlocks);          
        }

        /// <summary>
        /// Verifies that an empty reservation array results in zero allocations
        /// and that the provider remains fully functional.
        /// </summary>
        [TestMethod]
        public void ReservedRegion_EmptyArray_AllocatesNothing()
        {
            using TrackedHeapWrapper heap = new(MemoryUtil.Shared, false);
            PluginSharedMemoryConfig config = new()
            {
                Allocator       = new PluginSharedMemoryAllocator(heap, false),
                MinRegionSize   = 64,
                MaxRegionSize   = 4096,
                HostReservations = []
            };

            using PluginSharedMemoryProvider provider = new(config);

            Assert.AreEqual(0ul, heap.GetCurrentStats().AllocatedBlocks);
        }

        #endregion

        /*
         * Bundles the provider with its backing heap so tests can assert memory
         * accounting alongside provider behavior. Dispose releases the provider
         * first (triggering any internal registry sweep), then the heap wrapper.
         */
        private sealed class ProviderContext(PluginSharedMemoryProvider provider, TrackedHeapWrapper heap) 
            : VnDisposeable
        {
            public readonly PluginSharedMemoryProvider Provider = provider;
            public readonly TrackedHeapWrapper Heap = heap;

            protected override void Free()
            {
                Provider.Dispose();
                Heap.Dispose();
            }
        }

        /*
         * Bundles a ProviderContext with a RuntimePluginLoader so tests can inspect
         * both plugin state and heap accounting. Dispose releases the loader first
         * (clearing the controller), then the provider context.
         */
        private sealed class LoadedContext(ProviderContext providerCtx, RuntimePluginLoader pluginLoader) 
            : VnDisposeable
        {
            public readonly ProviderContext ProviderCtx = providerCtx;
            public readonly RuntimePluginLoader PluginLoader = pluginLoader;

            public void LoadPlugins()
            {
                PluginLoader.InitializeController();
                PluginLoader.LoadPlugins();
            }

            /// <summary>
            /// Explicitly unloads all plugins in the loader.
            /// </summary>
            public void UnloadPlugins() 
                => PluginLoader.UnloadPlugins();

            /// <summary>
            /// Gets the plugin instance of the desired type from the loader's controller.
            /// Asserts that a plugin of the specified type was loaded.
            /// </summary>
            /// <typeparam name="T">The type of the plugin to retrieve.</typeparam>
            /// <returns>The plugin instance.</returns>
            public T GetPlugin<T>() where T : class, IPlugin
            {
                T? plugin = PluginLoader
                    .Controller
                    .GetPlugin<T>();

                Assert.IsNotNull(plugin, $"No loaded plugin of type {typeof(T).Name} found");
                return plugin;
            }

            protected override void Free()  
            {
                PluginLoader.Dispose();
                ProviderCtx.Dispose();
            }
        }

        /*
         * A test IPluginAssemblyLoadConfig that wraps a fixed assembly so
         * RuntimePluginLoader can discover IPlugin types from it without touching
         * the file system. WatchForReload is false so no file watcher is created.
         */
        private sealed class TestPluginLoadConfig(Assembly assembly) : IPluginAssemblyLoadConfig
        {
            private readonly FixedAssemblyLoader _loader = new(assembly);

            public bool Unloadable => false;
            public string AssemblyFile => string.Empty;
            public bool WatchForReload => false;
            public TimeSpan ReloadDelay => TimeSpan.Zero;

            public IAssemblyLoader GetLoader() => _loader;

            private sealed class FixedAssemblyLoader(Assembly assembly) : IAssemblyLoader
            {
                public void Load() { }
                public Assembly GetAssembly() => assembly;
                public void Unload() { }
                public void Dispose() { }
            }
        }

        /*
         * Test IPlugin implementations — only valid ones.
         * All three are discovered by RuntimePluginLoader.InitializeController()
         * because Assembly.GetExecutingAssembly().GetTypes() finds every non-abstract
         * IPlugin implementation in the test assembly.
         */

        /// <summary>
        /// Test plugin that owns a shared memory region. Decorated with
        /// <see cref="SharedRegionAllocAttribute"/> to trigger region allocation
        /// during pre-load injection. Exactly one heap block is expected to be
        /// allocated for this plugin when the loader initializes.
        /// </summary>
        private sealed class AllocPlugin : IPlugin
        {
            public string PluginName => nameof(AllocPlugin);

            [SharedRegionAlloc("test-region", 1024)]
            public IPluginMemoryRegion? Region { get; set; }

            public void Load() { }
            public void Unload() { }
            public void PublishServices(IPluginServicePool pool) { }
        }

        /// <summary>
        /// Test plugin that consumes an existing shared memory region. Decorated
        /// with <see cref="SharedRegionOpenAttribute"/> to receive a deferred
        /// accessor during pre-load injection. Because <c>AllocPlugin</c> always
         /// maps the region first, this plugin is always an active accessor.
        /// </summary>
        private sealed class OpenPlugin : IPlugin
        {
            public string PluginName => nameof(OpenPlugin);

            [SharedRegionOpen("test-region")]
            public IPluginMemoryRegionAccessor? Accessor { get; set; }

            public void Load() { }
            public void Unload() { }
            public void PublishServices(IPluginServicePool pool) { }
        }

        /// <summary>
        /// Test plugin that opens a host-reserved shared memory region. Decorated
        /// with <see cref="SharedRegionOpenAttribute"/> targeting a region name
        /// that is pre-allocated via <c>HostReservations</c> in the provider config.
        /// </summary>
        private sealed class ReservedOpenPlugin : IPlugin
        {
            public string PluginName => nameof(ReservedOpenPlugin);

            [SharedRegionOpen("host_reserved_region")]
            public IPluginMemoryRegionAccessor? Accessor { get; set; }

            public void Load() { }

            public void Unload() { }
            public void PublishServices(IPluginServicePool pool) { }
        }

        /// <summary>
        /// Test plugin with no IPC attributes. Exists to verify that the provider
        /// silently skips plugins that carry no <see cref="SharedRegionAllocAttribute"/>
        /// or <see cref="SharedRegionOpenAttribute"/> decorations without error.
        /// </summary>
        private sealed class NoAttrPlugin : IPlugin
        {
            public string PluginName => nameof(NoAttrPlugin);

            public void Load() { }
            public void Unload() { }
            public void PublishServices(IPluginServicePool pool) { }
        }
    }
}
