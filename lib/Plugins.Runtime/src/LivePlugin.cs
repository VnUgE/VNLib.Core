/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: LivePlugin.cs 
*
* LivePlugin.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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
using System.Reflection;

namespace VNLib.Plugins.Runtime
{

    /// <summary>
    /// <para>
    /// Wrapper for a loaded <see cref="IPlugin"/> instance, used internally 
    /// for a single instance. 
    /// </para>
    /// <para>
    /// Lifetime: for the existence of a single loaded plugin instance. Created once
    /// per loaded plugin instance. Once the plugin is unloaded, it is no longer useable.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Users are encouraged to hold a reference to <see cref="LivePlugin"/> instead of the 
    /// underlying <see cref="IPlugin"/> to ensure that the plugin lifecycle and garbage collection
    /// is respected.
    /// </remarks>
    public class LivePlugin : IEquatable<IPlugin>, IEquatable<LivePlugin>
    {
        private bool _loaded;

        /// <summary>
        /// The plugin's <see cref="IPlugin.PluginName"/> property during load time
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public string PluginName => Plugin?.PluginName ?? throw new InvalidOperationException("Plugin is not loaded");

        /// <summary>
        /// The underlying <see cref="IPlugin"/> that is wrapped
        /// by the current instance
        /// </summary>
        public IPlugin? Plugin { get; private set; }

        /// <summary>
        /// The assembly that this plugin was created from
        /// </summary>
        public Assembly OriginAsm { get; }

        /// <summary>
        /// The exposed runtime type of the plugin. Equivalent to 
        /// calling <code>Plugin.GetType()</code>
        /// </summary>
        public Type PluginType { get; }
       
        internal LivePlugin(IPlugin plugin, Assembly originAsm)
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            OriginAsm = originAsm ?? throw new ArgumentNullException(nameof(originAsm));
            PluginType = plugin.GetType();           
        }

        /// <summary>
        /// Gets services from the plugin if it is loaded and 
        /// publishes them to the pool
        /// </summary>
        /// <param name="pool">The service pool to collect services into</param>
        /// <exception cref="InvalidOperationException"></exception>
        internal void GetServices(IPluginServicePool pool)
        {
            if (!_loaded)
            {
                throw new InvalidOperationException("Plugin is not loaded");
            }

            //Load services into pool
            Plugin?.PublishServices(pool);
        }
       

        /// <summary>
        /// Calls the <see cref="IPlugin.Load"/> method on the plugin if its loaded
        /// </summary>
        internal void LoadPlugin()
        {
            //Load and set loaded flag
            Plugin?.Load();
            _loaded = true;
        }

        /// <summary>
        /// Unloads the plugin, only if the plugin was successfully loaded by 
        /// calling the <see cref="IPlugin.Unload"/> event hook.
        /// </summary>
        internal void UnloadPlugin()
        {
            //Only call unload if the plugin successfully loaded
            if (!_loaded)
            {
                return;
            }

            try
            {
                Plugin?.Unload();
            }
            finally
            {
                Plugin = null;
            }
        }

        ///<inheritdoc/>
        /// <remarks>
        /// Compares plugins by their type's full name (namespace + type name) rather than 
        /// reference equality. This allows the runtime to unify plugins across multiple 
        /// assembly loads (e.g., hot-reload scenarios) where the same type may be loaded 
        /// from different assembly instances.
        /// </remarks>
        public override bool Equals(object? obj)
        {
            Type? pluginType = Plugin?.GetType();
            Type? otherType = obj?.GetType();

            if (pluginType == null || otherType == null)
            {
                return false;
            }

            // Compare by type full name to handle multiple assembly loads of the same plugin type
            return pluginType.FullName == otherType.FullName;
        }
        /// <inheritdoc/>
        public bool Equals(LivePlugin? other) => Equals(other?.Plugin);

        /// <inheritdoc/>
        public bool Equals(IPlugin? other) => Equals((object?)other);

        /// <inheritdoc/>
        public override int GetHashCode() 
            => Plugin?.GetHashCode() ?? throw new InvalidOperationException("Plugin is null");
    }
}
