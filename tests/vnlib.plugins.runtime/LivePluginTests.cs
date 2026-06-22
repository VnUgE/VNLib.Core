/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: LivePluginTests.cs 
*
* LivePluginTests.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
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
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Plugins.Runtime.Tests.Helpers;

namespace VNLib.Plugins.Runtime.Tests
{
    [TestClass]
    public class LivePluginTests
    {
        private static readonly Assembly TestAsm = typeof(LivePluginTests).Assembly;
        /*
         * Validates constructor-time behavior for the LivePlugin wrapper.
         * These cases verify the constructor rejects null input and properly assigns
         * properties from the provided IPlugin and Assembly.
         */
        #region Construction

        /// <summary>
        /// Validates that the constructor rejects null arguments for both plugin and assembly.
        /// </summary>
        [TestMethod]
        public void Ctor_NullArguments_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new LivePlugin(null!, TestAsm)
            );

            Assert.ThrowsExactly<ArgumentNullException>(
                () => new LivePlugin(new LpTestPlugin(), null!)
            );
        }

        /// <summary>
        /// Validates that the constructor properly assigns all properties from the provided
        /// IPlugin and Assembly: PluginName, Plugin, OriginAsm, and PluginType.
        /// </summary>
        [TestMethod]
        public void Ctor_ValidPlugin_SetsPropertiesCorrectly()
        {
            LpTestPlugin plugin = new();
            LivePlugin livePlugin = new(plugin, TestAsm);

            Assert.AreEqual(expected: "LpTestPlugin", livePlugin.PluginName);
            Assert.IsNotNull(livePlugin.Plugin);
            Assert.AreSame(plugin, livePlugin.Plugin);
            Assert.AreSame(TestAsm, livePlugin.OriginAsm);
            Assert.AreEqual(typeof(LpTestPlugin), livePlugin.PluginType);
        }

        #endregion

        /*
         * Covers the plugin load/unload lifecycle managed by LivePlugin.
         * These cases verify LoadPlugin sets the internal loaded flag and invokes
         * IPlugin.Load, UnloadPlugin only calls IPlugin.Unload when the plugin was
         * previously loaded, and unload clears the plugin reference.
         */
        #region Plugin Lifecycle

        /// <summary>
        /// Validates that LoadPlugin sets the internal loaded flag, invokes IPlugin.Load,
        /// and allows GetServices to succeed after loading.
        /// </summary>
        [TestMethod]
        public void LoadPlugin_SetsLoadedFlagAndCallsLoad()
        {
            LpTestPlugin plugin = new();
            TestServicePool pool = new();
            LivePlugin livePlugin = new(plugin, TestAsm);

            livePlugin.LoadPlugin();

            // Ensure plugin load was called
            Assert.AreEqual(expected: 1, plugin.LoadCallCount);           

            livePlugin.GetServices(pool);

            // Ensure GetServices calls PublishServices on the plugin
            Assert.AreEqual(expected: 1,  plugin.PublishServicesCallCount);
        }

        /// <summary>
        /// Validates that UnloadPlugin invokes IPlugin.Unload when the plugin
        /// was previously loaded.
        /// </summary>
        [TestMethod]
        public void UnloadPlugin_WhenLoaded_CallsUnload()
        {
            LpTestPlugin plugin = new();
            LivePlugin livePlugin = new(plugin, TestAsm);

            livePlugin.LoadPlugin();
            livePlugin.UnloadPlugin();

            Assert.AreEqual(expected: 1, plugin.UnloadCallCount);
        }

        /// <summary>
        /// Validates that UnloadPlugin skips the IPlugin.Unload call when the plugin
        /// was never loaded.
        /// </summary>
        [TestMethod]
        public void UnloadPlugin_WhenNotLoaded_SkipsUnloadCall()
        {
            LpTestPlugin plugin = new();
            LivePlugin livePlugin = new(plugin, TestAsm);

            livePlugin.UnloadPlugin();

            // Ensure load was never called
            Assert.AreEqual(expected: 0, plugin.LoadCallCount);

            // IPlugin.Unload should not be invoked when LoadPlugin was never called
            Assert.AreEqual(expected: 0, plugin.UnloadCallCount);
        }

        /// <summary>
        /// Validates that UnloadPlugin clears the Plugin property to null
        /// and guards access to subsequent property reads.
        /// </summary>
        [TestMethod]
        public void UnloadPlugin_GuardsPluginProperties()
        {
            LpTestPlugin plugin = new();
            LivePlugin livePlugin = new(plugin, TestAsm);

            livePlugin.LoadPlugin();
            livePlugin.UnloadPlugin();

            Assert.IsNull(livePlugin.Plugin);

            Assert.ThrowsExactly<InvalidOperationException>(() => _ = livePlugin.PluginName);

            Assert.ThrowsExactly<InvalidOperationException>(() => _ = livePlugin.GetHashCode());
        }


        #endregion

        /*
         * Validates service collection behavior via GetServices.
         * These cases verify GetServices calls PublishServices when the plugin is
         * loaded, and throws InvalidOperationException when called on an unloaded plugin.
         */
        #region Service Collection

        /// <summary>
        /// Validates that GetServices invokes IPlugin.PublishServices when the plugin is loaded.
        /// </summary>
        [TestMethod]
        public void GetServices_WhenLoaded_CallsPublishServices()
        {
            LpTestPlugin plugin = new();

            LivePlugin livePlugin = new(plugin, TestAsm);
            TestServicePool pool = new();

            livePlugin.LoadPlugin();

            livePlugin.GetServices(pool);

            Assert.AreEqual(
                expected: 1,
                plugin.PublishServicesCallCount, 
                message: "PublishServices was not called when plugin is loaded"
            );
        }

        /// <summary>
        /// Validates that GetServices throws InvalidOperationException when called
        /// before LoadPlugin has been invoked.
        /// </summary>
        [TestMethod]
        public void GetServices_WhenNotLoaded_ThrowsInvalidOperationException()
        {
            LpTestPlugin plugin = new();
            LivePlugin livePlugin = new(plugin, TestAsm);

            TestServicePool pool = new();

            Assert.ThrowsExactly<InvalidOperationException>(() => livePlugin.GetServices(pool));
        }

        #endregion
      
        /*
         * Validates basic equality functionality for LivePlugin.
         * These tests verify the equality surface works without asserting specific
         * implementation details, since the equality contract may need future correction.
         */
        #region Equality

        /// <summary>
        /// Validates that Equals correctly identifies when two LivePlugins wrap the same plugin type.
        /// </summary>
        [TestMethod]
        public void Equals_SameType_ReturnsExpectedResult()
        {
            LpTestPlugin plugin1 = new(), plugin2 = new();

            LivePlugin livePlugin1 = new(plugin1, TestAsm);
            LivePlugin livePlugin2 = new(plugin2, TestAsm);       

            Assert.IsTrue(
                livePlugin1.Equals(livePlugin2), 
                "Equals should identify plugins of the same type"
            );
        }

        /// <summary>
        /// Validates that Equals correctly distinguishes LivePlugins wrapping different plugin types.
        /// </summary>
        [TestMethod]
        public void Equals_DifferentTypes_ReturnsFalse()
        {
            LpTestPlugin plugin1 = new();
            AlternateTypePlugin plugin2 = new();          

            LivePlugin livePlugin1 = new(plugin1, TestAsm);
            LivePlugin livePlugin2 = new(plugin2, TestAsm);           

            Assert.IsFalse(
                livePlugin1.Equals(livePlugin2), 
                message: "Equals should distinguish between different plugin types"
            );
        }

        /// <summary>
        /// Validates that Equals handles null comparisons without throwing.
        /// </summary>
        [TestMethod]
        public void Equals_WithNull_ReturnsFalse()
        {
            LpTestPlugin plugin = new();            
            LivePlugin livePlugin = new(plugin, TestAsm);

            Assert.IsFalse(
                livePlugin.Equals((object?)null), 
                message: "Equals should handle null without throwing"
            );
        }

        /// <summary>
        /// Validates that GetHashCode returns a valid hash code when the plugin is loaded.
        /// </summary>
        [TestMethod]
        public void GetHashCode_WhenPluginNotNull_ReturnsHashCode()
        {
            LpTestPlugin plugin = new();
            LivePlugin livePlugin = new(plugin, TestAsm);        

            Assert.AreNotEqual(
                notExpected: 0, 
                livePlugin.GetHashCode(), 
                message: "GetHashCode should return a valid hash code"
            );
        }

        #endregion

        #region Test Helpers

        /*
         * Simple plugin type used to verify equality comparison between different plugin types.
         */
        private sealed class AlternateTypePlugin : IPlugin
        {
            public string PluginName => "AlternateTypePlugin";

            public void Load() { }

            public void Unload() { }

            public void PublishServices(IPluginServicePool pool) { }
        }

        private sealed class LpTestPlugin : TestPluginBase
        {
            public LpTestPlugin() 
                => PluginName = "LpTestPlugin";
        }

        #endregion
    }
}
