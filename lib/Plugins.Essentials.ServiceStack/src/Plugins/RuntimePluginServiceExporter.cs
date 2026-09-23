/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: RuntimePluginServiceExporter.cs
*
* RuntimePluginServiceExporter.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.Collections.Concurrent;

using VNLib.Plugins.Runtime;
using VNLib.Plugins.Runtime.Events;
using VNLib.Plugins.Runtime.Services;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins
{
    /// <summary>
    /// An event driven plugin service binder that listens to plugin load/unload events and binds/unbinds
    /// services to a target <see cref="IServiceBinder"/>.
    /// <para>
    /// Plugin load events are 1:1 with service bindings and must be reentrant to support concurrent 
    /// loading. When a plugin loads, its services are gathered and bound to the target binder. When a 
    /// plugin unloads, the converse occurs unbinding the service pool.
    /// </para>
    /// </summary>
    /// <param name="binder">The target service binder to bind plugin exported services to at runtime</param>
    public sealed class RuntimePluginServiceExporter(IServiceBinder binder) : IPluginEventListener
    {
        private readonly ConcurrentDictionary<PluginController, PluginServiceBindingAdapter> _boundPlugins = [];
        private readonly IServiceBinder _binder = binder ?? throw new ArgumentNullException(nameof(binder));

        /*
         * Overview: 
         * 
         * The purpose of this event handler is to listen for plugin load events and safely export their services
         * to an IServiceBinder that's waiting for services. 
         * 
         * This class tracks successful bindings so when a plugin unload event occurs, the services will be safely removed
         * before the plugin instance is actually unloaded, removing any references to plugin objects/services. 
         * 
         * A new service pool is created on every event to avoid complicated tracking of plugin lifecycle. The binder will be
         * responsible for actually tracking its own service pool across a stack of plugins. This allows the lifecycle to be
         * idempotent, meaning it safely supports reloading safely, but a full load and unload cycle.  
         * 
         * Threading note - Plugin events are re-entrant, but guaranteed to be called serially for every PluginController in 
         * a stack. So it's safe to access any objects or functions of a controller or state associated without risk of race 
         * conditions
         */

        /// <inheritdoc/>
        public void OnPluginLoaded(PluginController controller, object? state)
        {
            // Bindings should be pre-created
            PluginServiceBindingAdapter adapter = new();

            adapter.Listener.OnPluginLoaded(controller, state);

            // Export plugin services to the service binder scoped by this controller's plugin assembly
            _binder.Bind(adapter);

            // Register with bound services
            _boundPlugins[controller] = adapter;
        }

        /// <inheritdoc/>
        public void OnPluginUnloaded(PluginController controller, object? state)
        {
            // Unload all existing registrations from the service binder.
            if (_boundPlugins.TryRemove(controller, out PluginServiceBindingAdapter? adapter))
            {
                _binder.Unbind(adapter);

                adapter.Listener.OnPluginUnloaded(controller, state);

                adapter.DisposePool();
            }
        }

        /*
         * Wraps a plugin's service export in a service binding, by sort of abusing
         * the SharedPluginServiceProvider as a pool for a single plugin's services.
         */
        private sealed class PluginServiceBindingAdapter : IServiceBinding
        {

            private readonly SharedPluginServiceProvider _servicePool = new();

            /// <summary>
            /// Exposes the service pool's event listener to allow invoking lifecycle events
            /// </summary>
            internal IPluginEventListener Listener => _servicePool;

            /// <summary>
            /// Disposes the service pool and frees all resources associated with the plugin's services.
            /// </summary>
            internal void DisposePool() => _servicePool.Dispose();

            ///<inheritdoc/>
            IServiceProvider IServiceBinding.Services => _servicePool;
        }
    }
}
