/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: RuntimeLoaderLifecycleTests.cs 
*
* RuntimeLoaderLifecycleTests.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
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
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Plugins.Runtime.Batteries;
using VNLib.Plugins.Runtime.Services;
using VNLib.Plugins.Runtime.Tests.Helpers;


namespace VNLib.Plugins.Runtime.Tests
{
    [TestClass]
    public class RuntimeLoaderLifecycleTests
    {
        #region Happy Path

        /// <summary>
        /// Tests the complete happy-path plugin lifecycle: initialization, loading, service export, and
        /// unloading with all listeners and hooks in place. This test intentionally covers multiple
        /// lifecycle stages in sequence.
        /// </summary>
        [TestMethod]
        public void PluginLifecycle_HappyPath_IsCorrect()
        {
            using RuntimePluginLoader loader = new(config: new TestLoadConfig(), log: null);

            // Ensure empty before initialization
            Assert.HasCount(0, loader.Controller.Plugins);

            // Register the config initializer to inject config data and init logger
            RegisterConfigInitializer(loader.Controller);

            loader.InitializeController();

            /*
             * Broad assertion: exact count not asserted because multiple IPlugin types
             * may be discovered from the test assembly. Verifying at least one was found
             * is sufficient to confirm discovery works.
             */

            Assert.IsGreaterThan(0, loader.Controller.Plugins.Count, "Controller did not discover any plugins during initialization");

            // Ensure the local test plugin type was discovered 
            Assert.IsTrue(
                loader.Controller.ExposesType(typeof(LifecycleTestPlugin)), // Test ExposesType extension method
                message: "Controller did not discover the expected plugin type during initialization"
            );

            LifecycleTestPlugin? testPlugin = loader.Controller.GetPlugin<LifecycleTestPlugin>();
            Assert.IsNotNull(testPlugin);

            // Ensure nothing was loaded before LoadPlugins was called
            Assert.AreEqual(0, testPlugin.ConfigCalledCount);
            Assert.AreEqual(0, testPlugin.LogCalledCount);
            Assert.AreEqual(0, testPlugin.LoadCallCount);
            Assert.AreEqual(0, testPlugin.PublishServicesCallCount);

            loader.LoadPlugins();
          
            Assert.AreEqual(1, testPlugin.ConfigCalledCount);
            Assert.AreEqual(1, testPlugin.LogCalledCount);
            Assert.AreEqual(1, testPlugin.LoadCallCount);
            Assert.AreEqual(1, testPlugin.PublishServicesCallCount);

            // Test that the service was published
            PluginServiceExport[] exports = loader.Controller.GetExportedServices();

            /*
             * Broad assertion: multiple plugins may export services, so only
             * verifying that at least one service was published to catch total
             * export failures without coupling to a specific count.
             */
            Assert.IsGreaterThan(
                lowerBound: 0, 
                exports.Length, 
                message: "Expected more than one service in the pool, failed to export services"
            );

            // Ensure that our lifecycle service was published by the plugin
            Assert.Contains(export => export.Service is ILifecycleTestService, exports);

            // Unload the plugin and ensure the unload hook was called and the plugin is no longer active
            loader.UnloadAll(false);

            Assert.AreEqual(1, testPlugin.UnloadCallCount);

            // Ensure controller is empty 
            Assert.HasCount(0, loader.Controller.Plugins);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Registers a <see cref="PluginConfigInitializer"/> backed by a <see cref="LifecycleTestConfigReader"/>
        /// on the given controller to satisfy plugin configuration requirements during the lifecycle test.
        /// </summary>
        /// <param name="controller">The plugin controller to register the initializer with.</param>
        private static void RegisterConfigInitializer(PluginController controller)
        {
            controller.Register(
                listener: new PluginConfigInitializer(new LifecycleTestConfigReader())
            );
        }

        private sealed class LifecycleTestConfigReader : IPluginConfigReader
        {
            /// <inheritdoc/>
            public void ReadPluginConfigData(IPluginAssemblyLoadConfig config, Stream outputStream)
            {
                // Write some dummy data to the stream
                outputStream.WriteByte(0x00);
                outputStream.WriteByte(0x00);
            }
        }

        public interface ILifecycleTestService { }

        private sealed class TestService : ILifecycleTestService { }

        public sealed class LifecycleTestPlugin : TestPluginBase
        {
            /// <inheritdoc/>
            public override void PublishServices(IPluginServicePool pool)
            {
                pool.ExportService(
                    serviceType: typeof(ILifecycleTestService), 
                    service: new TestService(),
                    flags: ExportFlags.None
                );

                base.PublishServices(pool);
            }
        }

        #endregion
    }
}
