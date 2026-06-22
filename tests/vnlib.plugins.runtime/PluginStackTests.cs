/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: PluginStackTests.cs 
*
* PluginStackTests.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
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
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Plugins.Runtime.Events;
using VNLib.Plugins.Runtime.Construction;
using VNLib.Plugins.Runtime.Tests.Helpers;

namespace VNLib.Plugins.Runtime.Tests
{
    [TestClass]
    public class PluginStackTests
    {
        /*
         * Validates constructor-time behavior for the PluginStack.
         * These cases verify the constructor rejects null resolver input, properly
         * initializes with required and optional dependencies (resolver, listeners, logger),
         * and defers actual plugin discovery until BuildStack is called.
         */
        #region Construction

        /// <summary>
        /// Verifies that the constructor throws <see cref="ArgumentNullException"/> when required
        /// arguments are null
        /// </summary>
        [TestMethod]
        public void Ctor_NullArgs_ThrowsArgumentNullException()
        {
            TestLogProvider debugLog = new();

            Assert.ThrowsExactly<ArgumentNullException>(() => _ = new PluginStack(null!, debugLog));
            Assert.ThrowsExactly<ArgumentNullException>(() => _ = new PluginStack(null!, [], debugLog));


            TestAssemblyResolver resolver = new();
            Assert.ThrowsExactly<ArgumentNullException>(() => _ = new PluginStack(resolver, null!, debugLog));
        }

        /// <summary>
        /// Verifies that the constructor initializes successfully with only the required resolver argument.
        /// The stack should be created with an empty plugin collection before BuildStack is called.
        /// </summary>
        [TestMethod]
        public void Ctor_ResolverOnly_Initializes()
        {
            PluginStack stack = new(CreateEmptyTestResolver(), null);
            Assert.IsEmpty(stack.Plugins);

            // Test with explicit empty listeners array as well
            stack = new(CreateEmptyTestResolver(), [], null);
            Assert.IsEmpty(stack.Plugins);
        }

        /// <summary>
        /// Verifies that event listeners provided to the constructor are stored and registered
        /// to all created loaders when BuildStack is called. Expects each loader's controller
        /// to have the listeners registered.
        /// </summary>
        [TestMethod]
        public void Ctor_ResolverAndListeners_Initializes()
        {
            TestEventListener[] listeners = [new(), new()];

            PluginStack stack = new(CreateDummyTestResolver(1), listeners, null);
            Assert.IsEmpty(stack.Plugins);
        }

        #endregion

        /*
         * Covers the BuildStack operation that discovers assemblies and creates loaders.
         * These cases verify BuildStack calls DiscoverAssemblies on the resolver, creates
         * a RuntimePluginLoader for each assembly config, and registers any pending event
         * listeners to all created loaders.
         */
        #region Stack Building

        /// <summary>
        /// Verifies that BuildStack creates an empty plugin collection when the resolver returns no configs.
        /// Expects Plugins to be empty after BuildStack completes.
        /// </summary>
        [TestMethod]
        public void BuildStack_EmptyResolver_CreatesEmptyPluginArray()
        {
            TestAssemblyResolver resolver = CreateEmptyTestResolver();
            PluginStack stack = new(resolver, null);

            stack.BuildStack();

            Assert.IsEmpty(stack.Plugins);
        }

        /// <summary>
        /// Verifies that BuildStack calls DiscoverAssemblies on the resolver exactly once.
        /// Expects the resolver's call counter to equal 1 after BuildStack.
        /// </summary>
        [TestMethod]
        public void BuildStack_CallsDiscoverAssembliesOnResolver()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(1);

            PluginStack stack = new(resolver, null);
            stack.BuildStack();

            Assert.AreEqual(1, resolver.DiscoverAssembliesCallCount);
        }

        /// <summary>
        /// Tests the happy-path scenario where BuildStack creates a RuntimePluginLoader for 
        /// each config returned by the resolver during BuildStack().
        /// </summary>
        [TestMethod]
        public void BuildStack_WithConfigs_CreatesLoaders()
        {
            const int dummyConfigsCount = 2;

            TestAssemblyResolver resolver = CreateDummyTestResolver(dummyConfigsCount);

            PluginStack stack = new(resolver, null);

            // Zero before building
            Assert.HasCount(0, stack.Plugins);

            stack.BuildStack();

            // Matching number of plugins vs configs returned
            Assert.HasCount(dummyConfigsCount, stack.Plugins);
        }

        /// <summary>
        /// Verifies that BuildStack registers constructor-provided event listeners to all created loaders.
        /// Expects each loader's controller to have called OnBeforeLoading for every listener.
        /// </summary>
        [TestMethod]
        public void BuildStack_WithListeners_RegistersListenersToAllLoaders()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(2);

            TestEventListener[] listeners = [new(), new()];

            PluginStack stack = new(resolver, listeners, null);
            stack.BuildStack();

            // Verify listeners are registered by unregistering them and checking success
            foreach (RuntimePluginLoader plugin in stack.Plugins)
            {
                foreach (IPluginEventListener listener in listeners)
                {
                    Assert.IsTrue(
                        plugin.Controller.Unregister(listener),
                        $"Expected listener {listener.GetType().Name} to be registered."
                    );
                }
            }
        }

        /// <summary>
        /// Verifies that BuildStack throws <see cref="InvalidOperationException"/> when called a second time.
        /// Expects an exception after the first successful BuildStack call.
        /// </summary>
        [TestMethod]
        public void BuildStack_CalledTwice_ThrowsInvalidOperationException()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(1);

            PluginStack stack = new(resolver, null);
            stack.BuildStack();

            Assert.ThrowsExactly<InvalidOperationException>(stack.BuildStack);
        }

        /// <summary>
        /// Verifies that BuildStack throws <see cref="ObjectDisposedException"/> when the PluginStack has been disposed.
        /// Expects disposal to invalidate all subsequent BuildStack calls.
        /// </summary>
        [TestMethod]
        public void BuildStack_WhenDisposed_ThrowsObjectDisposedException()
        {
            PluginStack stack = new(CreateEmptyTestResolver(), null);

            stack.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(stack.BuildStack);
        }

        /// <summary>
        /// Verifies that BuildStack throws <see cref="ObjectDisposedException"/> if Dispose was called after BuildStack.
        /// Expects disposal to invalidate all subsequent BuildStack calls.
        /// </summary>
        [TestMethod]
        public void BuildStack_AfterDispose_ThrowsObjectDisposedException()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(1);

            PluginStack stack = new(resolver, null);
            stack.BuildStack();
            stack.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(stack.BuildStack);
        }

        /// <summary>
        /// Verifies that BuildStack propagates exceptions thrown by the resolver's DiscoverAssemblies.
        /// Expects the thrown exception to surface without wrapping.
        /// </summary>
        [TestMethod]
        public void BuildStack_ResolverThrows_PropagatesException()
        {
            Exception expected = new("Resolver failure");

            TestAssemblyResolver resolver = new() { ThrowOnDiscover = expected };

            PluginStack stack = new(resolver, null);

            Exception? actual = null;
            try
            {
                stack.BuildStack();
            }
            catch (Exception ex)
            {
                actual = ex;
            }

            Assert.AreEqual(expected, actual);
        }

        #endregion

        /*
         * Validates the Plugins property behavior before and after stack building.
         * These cases verify Plugins returns an empty collection before BuildStack
         * is called, and returns the created loader collection after BuildStack.
         */
        #region Plugin Collection

        /// <summary>
        /// Verifies that the Plugins property returns an empty collection before BuildStack is called.
        /// Expects an immediately initialized PluginStack to have no plugins.
        /// </summary>
        [TestMethod]
        public void Plugins_BeforeBuildStack_ReturnsEmpty()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(1);

            PluginStack stack = new(resolver, null);

            Assert.IsEmpty(stack.Plugins);
        }

        /// <summary>
        /// Verifies that the Plugins property returns an empty collection after Dispose is called.
        /// Expects disposal to clear the internal plugin array.
        /// </summary>
        [TestMethod]
        public void Plugins_AfterDispose_ReturnsEmpty()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(1);

            PluginStack stack = new(resolver, null);
            stack.BuildStack();
            stack.Dispose();

            Assert.IsEmpty(stack.Plugins);
        }

        /// <summary>
        /// Verifies that the Plugins property exposes a read-only collection.
        /// Expects the collection type to implement IReadOnlyCollection&lt;RuntimePluginLoader&gt;.
        /// </summary>
        [TestMethod]
        public void Plugins_ReturnsReadOnlyCollection()
        {
            PluginStack stack = new(CreateEmptyTestResolver(), null);

            Assert.IsInstanceOfType<IReadOnlyCollection<RuntimePluginLoader>>(stack.Plugins);
        }

        #endregion

        /*
         * Validates disposal behavior for the PluginStack.
         * These cases verify Dispose calls dispose on all RuntimePluginLoader instances,
         * clears the plugin array, and is safe to call multiple times (idempotent).
         */
        #region Disposal

        /// <summary>
        /// Verifies that Dispose calls <see cref="RuntimePluginLoader.Dispose"/> on all loaders in the stack.
        /// Expects each loader to be disposed after PluginStack.Dispose is called.
        /// </summary>
        [TestMethod]
        public void Dispose_CallsDisposeOnAllLoaders()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(2);

            PluginStack stack = new(resolver, null);
            stack.BuildStack();

            Assert.HasCount(2, stack.Plugins, "Number of plugins should match number of configs returned by resolver");

            // Store copy of loaders before disposing
            RuntimePluginLoader[] loaders = stack.Plugins.ToArray();

            // Dispose should propagate to all loaders
            stack.Dispose();

            // Ensure all loaders were disposed by checking that accessing them throws ObjectDisposedException
            Array.ForEach(
                loaders,
                pl => Assert.ThrowsExactly<ObjectDisposedException>(pl.InitializeController)
            );
        }

        /// <summary>
        /// Verifies that Dispose clears the internal plugin array.
        /// Expects the Plugins property to return an empty collection after disposal.
        /// </summary>
        [TestMethod]
        public void Dispose_ClearsPluginArray()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(1);

            PluginStack stack = new(resolver, null);
            stack.BuildStack();

            // Should equal the number of configs returned by the resolver
            Assert.HasCount(1, stack.Plugins);

            stack.Dispose();

            Assert.IsEmpty(stack.Plugins);
        }

        /// <summary>
        /// Verifies that Dispose is idempotent and can be called multiple times without throwing.
        /// Expects no exception when Dispose is called twice on the same PluginStack instance.
        /// </summary>
        [TestMethod]
        public void Dispose_CalledTwice_IsIdempotent()
        {
            TestAssemblyResolver resolver = CreateDummyTestResolver(1);

            PluginStack stack = new(resolver, null);
            stack.BuildStack();

            // Dispose twice - should not throw
            stack.Dispose();
            stack.Dispose();
        }

        /// <summary>
        /// Verifies that Dispose is safe to call when BuildStack has not been called.
        /// Expects no exception and Plugins to remain empty.
        /// </summary>
        [TestMethod]
        public void Dispose_WhenNotBuilt_IsNoOp()
        {
            PluginStack stack = new(CreateEmptyTestResolver(), null);

            // Dispose before BuildStack - should not throw
            stack.Dispose();

            Assert.IsEmpty(stack.Plugins);
        }

        #endregion

        /*
         * Validates error handling and edge cases for PluginStack operations.
         * These cases verify graceful handling of null configurations and resolver
         * failures. BuildStack propagates exceptions from underlying components
         * to alert callers of invalid state.
         */
        #region Error Handling

        /// <summary>
        /// Verifies that BuildStack propagates <see cref="ArgumentNullException"/> 
        /// when the resolver returns a collection containing a null config.
        /// Expects the exception to be thrown when attempting to create a RuntimePluginLoader
        /// with a null config.
        /// </summary>
        [TestMethod]
        public void BuildStack_NullConfigInArray_ThrowsArgumentNullException()
        {
            PluginStack stack = new(CreateTestResolver([ null! ]), null);

            Assert.ThrowsExactly<ArgumentNullException>(stack.BuildStack);
        }

        /// <summary>
        /// Verifies that BuildStack propagates <see cref="ArgumentNullException"/> 
        /// when the resolver returns null instead of a config collection.
        /// Expects ArgumentNullException since the LINQ Select operation cannot
        /// iterate over a null collection.
        /// </summary>
        [TestMethod]
        public void BuildStack_ResolverReturnsNull_ThrowsArgumentNullException()
        {
            PluginStack stack = new(CreateTestResolver(null!), null);

            Assert.ThrowsExactly<ArgumentNullException>(stack.BuildStack);
        }

        #endregion

        /*
         * Stub types for testing PluginStack without real assembly loading.
         * These fakes track method call counts and return configurable test data.
         */
        #region Helpers

        /// <summary>
        /// Creates a new <see cref="TestAssemblyResolver"/> that returns the specified configs.
        /// </summary>
        /// <param name="configs">The configs to return from the resolver.</param>
        /// <returns>A new <see cref="TestAssemblyResolver"/> instance.</returns>
        private static TestAssemblyResolver CreateTestResolver(params IPluginAssemblyLoadConfig[] configs)
            => new() { ConfigsToReturn = configs };

        /// <summary>
        /// Creates a new <see cref="TestAssemblyResolver"/> with the desired number of
        /// dummy <see cref="TestLoadConfig"/> instances in the ConfigsToReturn array.
        /// </summary>
        /// <param name="numConfigs">
        /// The number of dummy configs to return. Sets the number of resolved 
        /// assemblies during building
        /// </param>
        /// <returns>A new <see cref="TestAssemblyResolver"/> instance with the specified number of configs.</returns>
        private static TestAssemblyResolver CreateDummyTestResolver(int numConfigs)
        {
            IPluginAssemblyLoadConfig[] configs = Enumerable.Range(0, numConfigs)
                .Select(_ => new TestLoadConfig())
                .ToArray();

            return CreateTestResolver(configs);
        }

        /// <summary>
        /// Creates a new <see cref="TestAssemblyResolver"/> that returns an empty config array.
        /// </summary>
        /// <returns>A new <see cref="TestAssemblyResolver"/> instance with no configs.</returns>
        private static TestAssemblyResolver CreateEmptyTestResolver()
            => CreateDummyTestResolver(0);

        private sealed class TestAssemblyResolver : IPluginAssemblyResolver
        {
            public int DiscoverAssembliesCallCount { get; private set; }

            public IPluginAssemblyLoadConfig[] ConfigsToReturn { get; set; } = [];

            public Exception? ThrowOnDiscover { get; set; }

            public IEnumerable<IPluginAssemblyLoadConfig> DiscoverAssemblies()
            {
                DiscoverAssembliesCallCount++;

                return ThrowOnDiscover != null
                    ? throw ThrowOnDiscover
                    : ConfigsToReturn;
            }
        }

        private sealed class TestEventListener : IPluginEventListener
        {
            public void OnPluginLoaded(PluginController controller, object? state)
            { }

            public void OnPluginUnloaded(PluginController controller, object? state)
            { }
        }

        #endregion
    }
}

