/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: RuntimePluginLoaderTests.cs 
*
* RuntimePluginLoaderTests.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime.Tests. If not, see http://www.gnu.org/licenses/.
*/

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Plugins.Runtime.Tests.Helpers;
using VNLib.Plugins.Runtime.Watcher;


namespace VNLib.Plugins.Runtime.Tests
{
    [TestClass]
    public class RuntimePluginLoaderTests
    {
        /*
         * Covers constructor-time guarantees for the runtime entry point.
         * These cases verify the loader rejects invalid input and wires the
         * config, controller, and assembly-loader dependencies without doing early work.
         */
        #region Construction

        /// <summary>
        /// Validates that the runtime entry point rejects a null config immediately.
        /// </summary>
        [TestMethod]
        public void Ctor_NullConfig_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => _ = new RuntimePluginLoader(null!, null)
            );
        }

        /// <summary>
        /// Validates that construction wires config and controller dependencies without performing
        /// early initialization. Ensures the controller is created but remains empty until explicit
        /// initialization is requested.
        /// </summary>
        [TestMethod]
        public void Ctor_ConfigProvidesLoader_ControllerInitialized()
        {
            TestLoadConfig config = new();

            using RuntimePluginLoader loader = CreateLoader(config);

            // Ensures loader and controller config are wired correctly
            Assert.AreSame(config, loader.Config);
            Assert.IsNotNull(loader.Controller);
            Assert.AreSame(config, loader.Controller.LoaderConfig);

            // Plugins should not be discovered until InitializeController
            Assert.IsEmpty(loader.Controller.Plugins);
        }

        #endregion

        /*
         * Covers the cold-start discovery path after construction.
         * These cases verify explicit initialization prepares the assembly loader,
         * fetches the assembly, and discovers plugin types without loading them yet.
         */
        #region Initialization

        /// <summary>
        /// Validates that explicit initialization discovers plugin types and makes them queryable
        /// through the controller without loading them or publishing their services.
        /// </summary>
        [TestMethod]
        public void InitializeController_InvokesLoaderLoadAndInitializesController()
        {
            TestLoadConfig config = new();

            using RuntimePluginLoader loader = CreateLoader(config);

            loader.InitializeController();

            // Verify plugin was discovered and is queryable
            RplTestPlugin? plugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(plugin);

            // Verify controller has plugins after initialization
            Assert.IsGreaterThan(0, loader.Controller.Plugins.Count);
        }

        #endregion

        /*
         * Covers the normal load path once discovery has already completed.
         * These cases verify RuntimePluginLoader delegates into the controller
         * and drives plugin load plus service publication as one operation.
         */
        #region Load delegation

        /// <summary>
        /// Validates that LoadPlugins delegates to the controller and drives discovered plugins
        /// through their load hook and service publication.
        /// </summary>
        [TestMethod]
        public void LoadPlugins_AfterInitialize_LoadsDiscoveredPlugins()
        {
            using RuntimePluginLoader loader = CreateInitializedLoader();

            RplTestPlugin? plugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(plugin);

            loader.LoadPlugins();

            // Verify plugin is accessible after load
            RplTestPlugin? loadedPlugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(loadedPlugin);
        }

        #endregion

        /*
         * Covers full unload behavior across unloadable and non-unloadable contexts.
         * These cases verify plugin teardown always happens while assembly-loader teardown
         * only occurs when the configuration says the load context supports it.
         */
        #region Unload behavior

        /// <summary>
        /// Validates that UnloadAll invokes plugin unload hooks and clears the controller
        /// plugin collection.
        /// </summary>
        [TestMethod]
        public void UnloadAll_WhenUnloadable_CallsLoaderUnload()
        {
            TestLoadConfig config = new() { Unloadable = true };

            using RuntimePluginLoader loader = CreateInitializedLoader(config);

            RplTestPlugin? plugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(plugin);

            loader.LoadPlugins();
            loader.UnloadAll(false);

            // Should clear plugins collection after unload
            Assert.IsEmpty(loader.Controller.Plugins);

            // Verify Unload was called since the plugin is marked as unloadable
            Assert.AreEqual(1, config.Loader.UnloadCallCount, "Loader.Unload should be called once when the plugin is unloadable");
        }

        /// <summary>
        /// Validates that UnloadAll still unloads active plugins and clears the controller
        /// collection even when the assembly context is not unloadable.
        /// </summary>
        [TestMethod]
        public void UnloadAll_WhenNotUnloadable_DoesNotCallLoaderUnload()
        {
            TestLoadConfig config = new() { Unloadable = false };

            using RuntimePluginLoader loader = CreateInitializedLoader(config);

            RplTestPlugin? plugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(plugin);

            loader.LoadPlugins();
            loader.UnloadAll(false);

            // Should clear plugins collection after unload
            Assert.IsEmpty(loader.Controller.Plugins);

            // Verify Unload was NOT called since the plugin is not marked as unloadable
            Assert.AreEqual(0, config.Loader.UnloadCallCount, "Loader.Unload should not be called when the plugin is not unloadable");
        }

        /// <summary>
        /// Validates that UnloadAll can be called consecutively without throwing,
        /// ensuring the unload operation is idempotent.
        /// </summary>
        [TestMethod]
        public void UnloadAll_Consecutive_IsIdempotent()
        {
            TestLoadConfig config = new() { Unloadable = true };

            using RuntimePluginLoader loader = CreateInitializedLoader(config);

            loader.LoadPlugins();
            
            // First unload should succeed
            loader.UnloadAll(false);
            Assert.IsEmpty(loader.Controller.Plugins);

            // Second unload should not throw even though plugins already unloaded
            loader.UnloadAll(false);
            Assert.IsEmpty(loader.Controller.Plugins);
        }

        #endregion

        /*
         * Covers manual reload policy and sequencing.
         * These cases verify unsupported reloads fail fast and the supported branch
         * will later prove the full unload, reinitialize, and reload orchestration path.
         */
        #region Reload behavior

        /// <summary>
        /// Validates that ReloadPlugins fails fast when the backing assembly context cannot be
        /// unloaded, throwing without attempting teardown or initialization.
        /// </summary>
        [TestMethod]
        public void ReloadPlugins_WhenNotUnloadable_ThrowsNotSupportedException()
        {
            TestLoadConfig config = new() { Unloadable = false };

            using RuntimePluginLoader loader = CreateLoader(config);

            Assert.ThrowsExactly<NotSupportedException>(
                () => loader.ReloadPlugins(false)
            );
        }

        /// <summary>
        /// Validates that ReloadPlugins successfully completes the full reload cycle and leaves
        /// the controller populated with newly discovered plugin instances.
        /// </summary>
        [TestMethod]
        public void ReloadPlugins_WhenUnloadable_PerformsUnloadInitializeAndLoadSequence()
        {
            TestLoadConfig config = new() { Unloadable = true };

            using RuntimePluginLoader loader = CreateInitializedLoader(config);           

            loader.LoadPlugins();

            RplTestPlugin? originalPlugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(originalPlugin);

            loader.ReloadPlugins(false);

            RplTestPlugin? reloadedPlugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(reloadedPlugin);

            // Should not be the same instance after reload.
            // Every time InitializeController is called, it should create new plugin instances
            Assert.AreNotSame(originalPlugin, reloadedPlugin);
            
            // Verify controller still has plugins after reload
            Assert.IsGreaterThan(0, loader.Controller.Plugins.Count);
        }

        #endregion

        /*
         * Covers the file-watch callback path used for hot reload.
         * These cases focus on the runtime contract that reload failures are
         * converted into log events instead of escaping from the watcher handler.
         */
        #region Watch reload and logging

        /// <summary>
        /// Validates the watcher callback path triggers plugin reload, replacing the existing
        /// plugin instance with a new one after unloading and reinitialization.
        /// </summary>
        [TestMethod]
        public void OnAssemblyFileChanged_TriggersPluginReload()
        {
            TestLoadConfig config = new();
            using RuntimePluginLoader loader = CreateInitializedLoader(config);

            loader.LoadPlugins();

            RplTestPlugin? originalPlugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(originalPlugin);

            // Trigger an internal reload via watcher callback
            ((IPluginReloadEventHandler)loader).OnAssemblyFileChanged();

            // Re-query the plugin after reload should yield a new instance
            RplTestPlugin? reloadedPlugin = loader.Controller.GetPlugin<RplTestPlugin>();
            Assert.IsNotNull(reloadedPlugin);

            // Should be a different instance after reload (InitializeController creates new instances)
            Assert.AreNotSame(originalPlugin, reloadedPlugin);
        }

        #endregion

        /*
         * Covers final teardown of the loader itself.
         * These cases verify disposal releases owned resources and clears
         * controller state so the runtime does not retain stale plugin references.
         */
        #region Disposal

        /// <summary>
        /// Validates that disposal releases the backing assembly loader exactly once and clears
        /// controller plugin state to avoid retaining stale plugin wrappers.
        /// </summary>
        [TestMethod]
        public void Dispose_FreesControllerAndLoader()
        {
            TestLoadConfig config = new();
            RuntimePluginLoader loader = CreateInitializedLoader(config);

            loader.LoadPlugins();
            loader.Dispose();

            // Ensure dispose was called and cleared plugins from controller
            Assert.AreEqual(expected: 1, config.Loader.DisposeCallCount);
            Assert.HasCount(expected: 0, loader.Controller.Plugins);
        }

        /// <summary>
        /// Validates that all public API methods guarded by <see cref="VnDisposeable.Check"/> 
        /// throw <see cref="ObjectDisposedException"/> after the loader is disposed.
        /// Expects InitializeController, LoadPlugins, UnloadPlugins, and ReloadPlugins
        /// to all reject post-disposal invocation.
        /// </summary>
        [TestMethod]
        public void DisposedLoader_ApiMethods_ThrowObjectDisposedException()
        {
            TestLoadConfig config = new() { Unloadable = true };
            using RuntimePluginLoader loader = CreateInitializedLoader(config);
            loader.Dispose(); 

            // Ensure public api functions all throw ObjectDisposedException after disposal
            Assert.ThrowsExactly<ObjectDisposedException>(loader.InitializeController);
            Assert.ThrowsExactly<ObjectDisposedException>(loader.LoadPlugins);
            Assert.ThrowsExactly<ObjectDisposedException>(loader.UnloadPlugins);
            Assert.ThrowsExactly<ObjectDisposedException>(() => loader.ReloadPlugins(false));
        }

        #endregion

        #region Helpers

        private static RuntimePluginLoader CreateLoader(TestLoadConfig? config = null)
        {
            config ??= new TestLoadConfig();

            return new RuntimePluginLoader(config, null);
        }

        private static RuntimePluginLoader CreateInitializedLoader(TestLoadConfig? config = null)
        {
            RuntimePluginLoader loader = CreateLoader(config);

            loader.InitializeController();

            return loader;
        }    


        private sealed class RplTestPlugin : TestPluginBase
        {

        }

        #endregion
    }
}

