/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: PluginControllerTests.cs 
*
* PluginControllerTests.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
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
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Plugins.Runtime.Events;
using VNLib.Plugins.Runtime.Tests.Helpers;

namespace VNLib.Plugins.Runtime.Tests
{
    [TestClass]
    public class PluginControllerTests
    {
        // Creates a controller via its internal constructor with a no-op config stub,
        // since registration tests do not exercise assembly loading.
        private static PluginController CreateController() => new(new TestLoadConfig());

        #region Registration

        /// <summary>
        /// Validates that registering a null listener is rejected at the point of registration.
        /// </summary>
        [TestMethod]
        public void Register_NullListener_ThrowsArgumentNullException()
        {            
            PluginController controller = CreateController();

            Assert.ThrowsExactly<ArgumentNullException>(
                () => controller.Register(null!)
            );
        }

        /// <summary>
        /// Validates that re-registering the same listener does not duplicate event dispatch,
        /// producing only one hook call per event.
        /// </summary>
        [TestMethod]
        public void Register_SameListenerTwice_DoesNotDuplicate()
        {          
            PluginController controller = CreateController();
            TrackingListener listener = new();

            controller.Register(listener);
            controller.Register(listener);
           
            controller.LoadPlugins();

            Assert.AreEqual(expected: 1, listener.BeforeLoadingCount);
            Assert.AreEqual(expected: 1, listener.LoadedCount);
        }

        /// <summary>
        /// Validates that re-registering a listener with different state replaces the previous
        /// state binding while still invoking the listener exactly once.
        /// </summary>
        [TestMethod]
        public void Register_ReregisteredListener_OverwritesState()
        {            
            PluginController controller = CreateController();
            TrackingListener listener = new();

            object firstState = new();
            object secondState = new();

            controller.Register(listener, firstState);
            controller.Register(listener, secondState);

            controller.LoadPlugins();

            Assert.AreEqual(expected: 1, listener.LoadedCount);
            Assert.AreSame(expected: secondState, listener.LastLoadedState);
        }

        /// <summary>
        /// Validates that re-registering a listener preserves its original dispatch position
        /// in the registration order rather than moving it to the end.
        /// </summary>
        [TestMethod]
        public void Register_ReregisteredListener_PreservesDispatchOrder()
        {            
            PluginController controller = CreateController();
            List<int> dispatchOrder = [];

            OrderedListener first  = new(1, dispatchOrder);
            OrderedListener second = new(2, dispatchOrder);
            OrderedListener third  = new(3, dispatchOrder);

            controller.Register(first);
            controller.Register(second);
            controller.Register(third);
            controller.Register(second);

            controller.LoadPlugins();

            CollectionAssert.AreEqual(
                new[] { 1, 2, 3 },
                dispatchOrder,
                "Re-registering a listener must preserve its original dispatch position"
            );
        }

        /// <summary>
        /// Validates that unregistering a registered listener returns true and prevents
        /// the listener from receiving further events.
        /// </summary>
        [TestMethod]
        public void Unregister_RegisteredListener_ReturnsTrueAndStopsDispatch()
        {
            PluginController controller = CreateController();
            TrackingListener listener = new();

            controller.Register(listener);

            Assert.IsTrue(controller.Unregister(listener));

            controller.LoadPlugins();         

            Assert.AreEqual(expected: 0, listener.LoadedCount);
        }

        /// <summary>
        /// Validates that unregistering a listener that was never registered returns false
        /// without throwing, signaling the listener was not found.
        /// </summary>
        [TestMethod]
        public void Unregister_UnknownListener_ReturnsFalse()
        {
            PluginController controller = CreateController();
            TrackingListener listener = new();
            
            Assert.IsFalse(controller.Unregister(listener));
        }

        #endregion

        #region Dispatch

        /// <summary>
        /// Validates that multiple listeners are dispatched in registration order,
        /// which is a documented API contract.
        /// </summary>
        [TestMethod]
        public void Register_MultipleListeners_DispatchesInRegistrationOrder()
        {
            PluginController controller = CreateController();
            List<int> dispatchOrder = [];

            OrderedListener first  = new(1, dispatchOrder);
            OrderedListener second = new(2, dispatchOrder);
            OrderedListener third  = new(3, dispatchOrder);

            controller.Register(first);
            controller.Register(second);
            controller.Register(third);

            controller.LoadPlugins();

            CollectionAssert.AreEqual(
                new[] { 1, 2, 3 },
                dispatchOrder,
                "Listeners were not dispatched in registration order"
            );
        }

        /// <summary>
        /// Validates that exceptions from OnBeforeLoading propagate to the caller
        /// rather than being swallowed.
        /// </summary>
        [TestMethod]
        public void LoadPlugins_OnBeforeLoadingException_ExceptionPropagates()
        {
            PluginController controller = CreateController();
            TrackingListener faultyListener = new() { ThrowOnBeforeLoading = true };

            controller.Register(faultyListener);

            Assert.ThrowsExactly<InvalidOperationException>(controller.LoadPlugins);
        }

        /// <summary>
        /// Validates that an exception during OnBeforeLoading aborts dispatch in registration order,
        /// preventing later listeners from being reached.
        /// </summary>
        [TestMethod]
        public void LoadPlugins_OnBeforeLoadingException_AbortsDispatchInOrder()
        {
            PluginController controller = CreateController();

            TrackingListener faultyFirst = new() { ThrowOnBeforeLoading = true };
            TrackingListener secondListener = new();

            controller.Register(faultyFirst);
            controller.Register(secondListener);

            try
            {
                controller.LoadPlugins();
            }
            catch (InvalidOperationException)
            {
            }

            Assert.AreEqual(expected: 0, secondListener.BeforeLoadingCount);
            Assert.AreEqual(expected: 0, secondListener.LoadedCount);
        }

        /// <summary>
        /// Validates that both unload hooks fire exactly once per load/unload cycle.
        /// </summary>
        [TestMethod]
        public void UnloadPlugins_AfterLoad_FiresAllUnloadHooks()
        {
            PluginController controller = CreateController();
            TrackingListener listener = new();

            controller.Register(listener);
            controller.LoadPlugins();

            controller.UnloadPlugins();

            Assert.AreEqual(expected: 1, listener.UnloadedCount);
            Assert.AreEqual(expected: 1, listener.AfterUnloadedCount);
        }

        #endregion

        #region Stub helpers

        /*
         * Records its registration ID into a shared list when OnPluginLoaded fires,
         * letting tests verify that dispatch happened in the expected order.
         */
        private sealed class OrderedListener(int id, List<int> order) : IPluginEventListener
        {
            public void OnPluginLoaded(PluginController controller, object? state)   => order.Add(id);

            public void OnPluginUnloaded(PluginController controller, object? state) { }
        }

        private sealed class TrackingListener : IPluginEventListener
        {
            public int BeforeLoadingCount { get; private set; }

            public int LoadedCount { get; private set; }

            public int UnloadedCount { get; private set; }

            public int AfterUnloadedCount { get; private set; }

            public object? LastLoadedState { get; private set; }

            public bool ThrowOnBeforeLoading { get; init; }

            public void OnBeforeLoading(PluginController controller, object? state)
            {
                BeforeLoadingCount++;

                if (ThrowOnBeforeLoading)
                {
                    throw new InvalidOperationException("Simulated failure in OnBeforeLoading");
                }
            }

            public void OnPluginLoaded(PluginController controller, object? state)
            {
                LoadedCount++;
                LastLoadedState = state;
            }

            public void OnPluginUnloaded(PluginController controller, object? state) => UnloadedCount++;

            public void OnAfterUnloaded(PluginController controller, object? state) => AfterUnloadedCount++;
        }

        #endregion
    }
}
