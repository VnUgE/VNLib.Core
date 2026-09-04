/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginSharedMemoryProvider.cs
*
* PluginSharedMemoryProvider.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger
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
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;

using VNLib.Utils;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Runtime.Events;
using VNLib.Plugins.Ipc.SharedMemory;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc
{
    /// <summary>
    /// Provides concrete, named, shared memory regions for plugins and injects
    /// IPC interface types declared in the <see cref="N:VNLib.Plugins.Ipc.SharedMemory"/>
    /// namespace to all plugins dynamically.
    /// </summary>
    public sealed class PluginSharedMemoryProvider: VnDisposeable
    {
        private readonly PluginSharedMemoryRegistry _registry;
        private readonly PluginSharedMemoryConfig _config;
        private readonly IIpcRegionOwner[] _reservedRegions;

        /// <summary>
        /// Creates a new <see cref="PluginSharedMemoryProvider"/> with the specified
        /// configuration and validates all configuration properties.
        /// </summary>
        /// <param name="config">The plugin shared memory configuration</param>
        /// <exception cref="ArgumentNullException">config or its Allocator is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// MinRegionSize is less than 1, or MaxRegionSize is less than MinRegionSize.
        /// </exception>
        public PluginSharedMemoryProvider(PluginSharedMemoryConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(config.Allocator);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.MinRegionSize, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.MaxRegionSize, config.MinRegionSize);

            _config = config;
            _registry = new(config);

            // reserve regions if any are set
            _reservedRegions = config.HostReservations is not null
                ? AllocHostReservedRegions(_registry, config.HostReservations)
                : [];
        }

        private static IIpcRegionOwner[] AllocHostReservedRegions(
            PluginSharedMemoryRegistry registry, 
            IEnumerable<PluginSharedMemoryHostReservation> res
        )
        {
            // Dictionary will catch duplicate keys, MapRegion will catch bad strings and bad sizes
           return res.ToDictionary(
                    static v => v.RegionName.ToLowerInvariant(),
                    static v => v.Size
                ) 
                .Select(kv => registry.MapRegion(kv.Key, kv.Value))
                .ToArray();
        }

        /// <summary>
        /// Creates a new <see cref="IPluginEventListener"/> that will manage runtime
        /// processing and service injection of plugin shared memory
        /// </summary>
        /// <returns>
        /// An <see cref="IPluginEventListener"/> that must be registered with the plugin 
        /// stack's controller(s) to enable IPC shared memory injection during plugin load/unload cycles.
        /// </returns>
        public IPluginEventListener GetListener()
        {
            Check();
            return new PluginIpcInitializer(_registry, _config);
        }


        ///<inheritdoc/>
        protected override void Free()
        {
            // Prefer explicitly unmapping regions before disposing for debugging/tracing reasons.
            Array.ForEach(_reservedRegions, _registry.ReleaseHandle);

            _registry.Dispose();
        }

        /*
         * This initializer hooks into the plugin load events to know when plugins
         * are loading and unloading. It's used to detect and inject shared memory regions
         * declared by a plugin. The user must manually add the listener to the plugin
         * stack using GetListener().
         *
         * It uses the declarative interface that the Plugins.IPC library provides using
         * attributes and shared interface contracts. It detects region owners and accessors
         * maps and assigns them to properties exposed on the IPlugin's type.
         */

        private sealed class PluginIpcInitializer(PluginSharedMemoryRegistry registry, PluginSharedMemoryConfig config)
            : IPluginEventListener
        {
            /*
             * This table tracks shared region mappings for all plugin instances. Using weak references
             * to avoid GC issues on unclean exits. The registry will handle memory leaks on exit if
             * it needs to
             */
            private readonly ConditionalWeakTable<LivePlugin, PluginRegionMappings> _pluginRegions = [];

            private void AllocRegionForPlugin(LivePlugin plugin, SharedRegionAllocAttribute attr, PropertyInfo property)
            {
                // Validate property type before allocating
                if (!typeof(IPluginMemoryRegion).IsAssignableFrom(property.PropertyType))
                {
                    throw new InvalidOperationException(
                        $"Property '{property.Name}' on '{plugin.PluginName}' is decorated with " +
                        $"[{nameof(SharedRegionAllocAttribute)}] but its type is not assignable from {nameof(IPluginMemoryRegion)}."
                    );
                }

                // Guard sizes with more informative exception
                if (attr.Size > config.MaxRegionSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(attr.Size),
                        $"Plugin {plugin.PluginName} requested a shared region {attr.Name} of size {attr.Size}, " +
                        $"which exceeds the maximum allowed size of {config.MaxRegionSize}."
                    );
                }

                if (attr.Size < config.MinRegionSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(attr.Size),
                        $"Plugin {plugin.PluginName} requested a shared region {attr.Name} of size {attr.Size}, " +
                        $"which is below the minimum allowed size of {config.MinRegionSize}."
                    );
                }

                // Alloc region for plugin
                IIpcRegionOwner owner = registry.MapRegion(attr.Name, attr.Size);

                try
                {
                    // Attempt to set, otherwise always clean up.
                    property.SetValue(plugin.Plugin, owner.Region);

                    // Store owned mapping
                    PluginRegionMappings mapping = _pluginRegions.GetOrCreateValue(plugin);
                    mapping.MappedHandles.AddLast(owner);
                }
                catch
                {
                    registry.ReleaseHandle(owner);
                    throw;
                }
            }

            private void OpenExistingRegionForPlugin(LivePlugin plugin, SharedRegionOpenAttribute attr, PropertyInfo property)
            {
                // Validate property type before opening
                if (!typeof(IPluginMemoryRegionAccessor).IsAssignableFrom(property.PropertyType))
                {
                    throw new InvalidOperationException(
                        $"Property '{property.Name}' on '{plugin.PluginType.FullName}' is decorated with " +
                        $"[{nameof(SharedRegionOpenAttribute)}] but its type is not assignable from {nameof(IPluginMemoryRegionAccessor)}."
                    );
                }

                // Create new accessor ticket
                IPluginMemoryRegionAccessor accessor = registry.AddReader(attr.Name);

                try
                {
                    // Attempt to assign the accessor
                    property.SetValue(plugin.Plugin, accessor);

                    // Store owned mapping
                    PluginRegionMappings mapping = _pluginRegions.GetOrCreateValue(plugin);
                    mapping.MappedHandles.AddLast(accessor);
                }
                catch
                {
                    // Failed, be sure to close accessor
                    registry.ReleaseHandle(accessor);
                    throw;
                }
            }

            private void InjectIpcProperties(LivePlugin plugin)
            {
                PropertyInfo[] props = plugin.PluginType.GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );

                // Handle all new mappings first
                props.Select(static p => (prop: p, attr: p.GetCustomAttribute<SharedRegionAllocAttribute>()))
                    .Where(static t => t.attr != null)
                    .ForEach(t => AllocRegionForPlugin(plugin, t.attr!, t.prop));

                // Handle open existing properties
                props.Select(static p => (prop: p, attr: p.GetCustomAttribute<SharedRegionOpenAttribute>()))
                    .Where(static t => t.attr != null)
                    .ForEach(t => OpenExistingRegionForPlugin(plugin, t.attr!, t.prop));
            }

            /*
             *  Hooks into the pre-load plugin function and attempts to discover and inject
             *  shared memory mappings to plugins.
             */

            /// <inheritdoc/>
            public void OnBeforeLoading(PluginController controller, object? state)
            {
                List<Exception>? failures = null;

                foreach (LivePlugin plugin in controller.Plugins)
                {
                    try
                    {
                        InjectIpcProperties(plugin);
                    }
                    catch (Exception ex)
                    {
                        (failures ??= []).Add(ex);
                    }
                }

                if (failures is { Count: > 0 })
                {
                    throw new AggregateException(
                        "One or more plugins failed IPC shared memory injection. See inner exceptions for details.",
                        failures
                    );
                }
            }

            /// <inheritdoc/>
            public void OnPluginLoaded(PluginController controller, object? state) { }

            /// <inheritdoc/>
            public void OnPluginUnloaded(PluginController controller, object? state) { }

            /*
             * Hooks into plugin after unloaded to ensure that memory was cleaned up
             * during "process" exit. When a plugin loader exits, all of the plugins
             * in it's assembly are unloaded. We know that it's safe to completely
             * unmap all regions and accessors.
             *
             * This hook uses the mapping table to determine what regions and accessors
             * belong to a plugin, then unmaps them.
             */

            /// <inheritdoc/>
            public void OnAfterUnloaded(PluginController controller, object? state)
            {
                foreach (LivePlugin plugin in controller.Plugins)
                {
                    if (_pluginRegions.TryGetValue(plugin, out PluginRegionMappings? mapping))
                    {
                        // Unmaps all handles held by a plugin, registry will decide weather it's
                        // a reader or a region owner.
                        mapping.MappedHandles.ForEach(registry.ReleaseHandle);

                        _pluginRegions.Remove(plugin);
                    }
                }
            }

            private sealed class PluginRegionMappings
            {
                public readonly LinkedList<object> MappedHandles = [];
            }
        }
    }
}
