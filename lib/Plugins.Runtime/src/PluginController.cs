/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: PluginController.cs 
*
* PluginController.cs is part of VNLib.Plugins.Runtime which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime.Services;
using VNLib.Plugins.Runtime.Events;

namespace VNLib.Plugins.Runtime
{

    /// <summary>
    /// Manages the lifetime of a collection of <see cref="IPlugin"/> instances,
    /// and their dependent event listeners
    /// </summary>
    public sealed class PluginController : IPluginEventRegistrar
    {
        /*
         * Listeners are stored in a plain List to preserve insertion order — dispatch order
         * is documented as part of the public API contract. The list is only mutated under
         * _listenerLock; dispatch always operates on a snapshot taken under the lock so no
         * handler is ever called while the lock is held.
         */
        private readonly object _listenerLock = new();
        private readonly List<ListenerRegistration> _listeners = [];
        private readonly PluginServicePool _servicePool = new();

        private LivePlugin[] _plugins = [];

        internal PluginController(IPluginAssemblyLoadConfig config) => LoaderConfig = config;

        /// <summary>
        /// Gets the <see cref="IPluginAssemblyLoadConfig"/> to which this controller belongs
        /// </summary>
        public IPluginAssemblyLoadConfig LoaderConfig { get; }

        /// <summary>
        /// The current collection of plugins. Valid before the unload event.
        /// </summary>
        public IReadOnlyCollection<LivePlugin> Plugins => _plugins;

        /// <summary>
        /// <para>
        /// Registers a listener for plugin lifecycle events.
        /// </para>
        /// <para>
        /// Overwrites any pre-existing registrations if called more than once with the same 
        /// listener instance. Preserves dispatch order when updating existing registrations.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Calling <see cref="Register(IPluginEventListener, object?)"/>
        /// or <see cref="Unregister(IPluginEventListener)"/> during lifecycle events (loading/unload/reload) may cause the
        /// hooks to be missed.
        /// </remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public void Register(IPluginEventListener listener, object? state = null)
        {
            ArgumentNullException.ThrowIfNull(listener);

            lock (_listenerLock)
            {
                // Find existing registration by listener reference
                for (int i = 0; i < _listeners.Count; i++)
                {
                    if (ReferenceEquals(_listeners[i].Listener, listener))
                    {
                        // Update state in-place to preserve registration order
                        _listeners[i] = new ListenerRegistration(listener, state);
                        return;
                    }
                }

                // No existing registration found, add to end
                _listeners.Add(new ListenerRegistration(listener, state));
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Calling <see cref="Register(IPluginEventListener, object?)"/>
        /// or <see cref="Unregister(IPluginEventListener)"/> during lifecycle events (loading/unload/reload) may cause the
        /// hooks to be missed.
        /// </remarks>
        public bool Unregister(IPluginEventListener listener)
        {
            ArgumentNullException.ThrowIfNull(listener);

            lock (_listenerLock)
            {
                return _listeners.RemoveAll(l => ReferenceEquals(l.Listener, listener)) > 0;
            }
        }

        /// <summary>
        /// Gets all services exported by the currently loaded plugins.
        /// </summary>
        /// <returns>An array of <see cref="PluginServiceExport"/> instances from all loaded plugins.</returns>
        public PluginServiceExport[] GetExportedServices() => _servicePool.GetServices();

        /// <summary>
        /// Discovers plugin types from the supplied assembly, creates plugin instances, and 
        /// stores them in the controller.
        /// </summary>
        /// <param name="asm">The assembly to scan for plugin types</param>
        internal void InitializePlugins(Assembly asm)
        {
            //get all IPlugin types
            Type[] types = asm
                .GetTypes()
                .Where(static type => !type.IsAbstract && typeof(IPlugin).IsAssignableFrom(type))
                .ToArray();

            //Initialize the new plugin instances
            IPlugin[] plugins = types
                .Select(static t => (IPlugin)Activator.CreateInstance(t)!)
                .ToArray();

            //Create new containers
            _plugins = plugins
                .Select(p => new LivePlugin(p, asm))
                .ToArray();
        }

        internal void LoadPlugins()
        {
            ListenerRegistration[] hooks;
            lock (_listenerLock)
            {
                hooks = _listeners.ToArray();
            }

            // Notify of pre-load
            hooks.ForEach(l => l.OnBeforeLoad(this));

            // Load all plugins
            _plugins.TryForeach(static p => p.LoadPlugin());

            // Load all services into the service pool
            _plugins.ForEach(p => p.GetServices(_servicePool));

            // Notify event handlers
            hooks.ForEach(l => l.OnLoaded(this));
        }

        internal void UnloadPlugins()
        {
            ListenerRegistration[] hooks;
            lock (_listenerLock)
            {
                hooks = _listeners.ToArray();
            }

            try
            {
                //Notify event handlers
                hooks.ForEach(l => l.OnUnloaded(this));

                // Best effort unload all plugins.
                _plugins.TryForeach(static p => p.UnloadPlugin());

                // Best effort call after unloaded for cleanup tasks
                hooks.TryForeach(l => l.OnAfterUnloaded(this));
            }
            finally
            {
                /*
                 * Always clear stateful collections during unload regardless of 
                 * exceptions to avoid leaving the controller in a broken state.
                 * 
                 * Unload is considered the "end" of the plugin lifecycle. Init must
                 * be called again with Load following to reuse the controller.
                 */

                _plugins = [];
                _servicePool.Clear();
            }
        }

        internal void Dispose()
        {
            _plugins = [];
            _servicePool.Clear();

            lock (_listenerLock)
            {
                _listeners.Clear();
            }
        }


        private sealed record ListenerRegistration(
            IPluginEventListener Listener,
            object? State
        )
        {
            internal void OnBeforeLoad(PluginController controller) 
                => Listener.OnBeforeLoading(controller, State);

            internal void OnLoaded(PluginController controller) 
                => Listener.OnPluginLoaded(controller, State);

            internal void OnUnloaded(PluginController controller)
                => Listener.OnPluginUnloaded(controller, State);

            internal void OnAfterUnloaded(PluginController controller)
                => Listener.OnAfterUnloaded(controller, State);
        }
    }
}
