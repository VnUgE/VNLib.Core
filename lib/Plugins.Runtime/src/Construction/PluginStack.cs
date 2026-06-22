/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: PluginStack.cs 
*
* PluginStack.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime.Events;

namespace VNLib.Plugins.Runtime.Construction
{
    /// <summary>
    /// A default implementation of a <see cref="IPluginStack"/> that uses an 
    /// assembly resolver to maintain a collection of <see cref="RuntimePluginLoader"/>s
    /// </summary>
    public sealed class PluginStack : VnDisposeable, IPluginStack
    {
        private readonly IPluginAssemblyResolver _resolver;
        private readonly ILogProvider? _debugLog;
        private readonly IPluginEventListener[] _initListeners;
        private RuntimePluginLoader[] _plugins = [];
        private bool _isBuilt;

        /// <summary>
        /// Creates a new default instance of a <see cref="PluginStack"/> that builds the stack
        /// from the specified assembly resolver.
        /// </summary>
        /// <param name="resolver">The <see cref="IPluginAssemblyResolver"/> used to resolve plugin assemblies</param>
        /// <param name="debugLog">An optional logger used for writing internal information to, such as errors, or debugging information</param>
        public PluginStack(IPluginAssemblyResolver resolver, ILogProvider? debugLog)
            : this(resolver, [], debugLog)
        { }

        /// <summary>
        /// Creates a new default instance of a <see cref="PluginStack"/> that builds the stack
        /// from the specified assembly resolver.
        /// </summary>
        /// <param name="resolver">The <see cref="IPluginAssemblyResolver"/> used to resolve plugin assemblies</param>
        /// <param name="listeners">An optional array of event listeners to add when the stack is configured</param>
        /// <param name="debugLog">An optional logger used for writing internal information to, such as errors, or debugging information</param>
        public PluginStack(IPluginAssemblyResolver resolver, IPluginEventListener[] listeners, ILogProvider? debugLog)
        {
            ArgumentNullException.ThrowIfNull(resolver);
            ArgumentNullException.ThrowIfNull(listeners);

            _resolver = resolver;
            _debugLog = debugLog;
            _initListeners = listeners;
        }

        ///<inheritdoc/>
        public IReadOnlyCollection<RuntimePluginLoader> Plugins => _plugins;

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void BuildStack()
        {
            Check();

            if (_isBuilt)
            {
                throw new InvalidOperationException("Plugin stack has already been built.");
            }

            // Create a loader for each plugin
            _plugins = _resolver.DiscoverAssemblies()
                .Select(c => new RuntimePluginLoader(c, _debugLog))
                .ToArray();

            // Register any pending listeners to the collection
            foreach (IPluginEventListener listener in _initListeners)
            {
                Array.ForEach(_plugins, p => p.Controller.Register(listener, null));
            }

            _isBuilt = true;
        }

        /// <inheritdoc/>
        protected override void Free()
        {
            // Dispose all loaders
            _plugins.TryForeach(static p => p.Dispose());
            _plugins = [];
        }                 
    }
}
