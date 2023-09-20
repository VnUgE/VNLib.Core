/*
* Copyright (c) 2023 Vaughn Nugent
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

using VNLib.Utils.IO;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Manages the lifetime of a collection of <see cref="IPlugin"/> instances,
    /// and their dependent event listeners
    /// </summary>
    public sealed class PluginController : IPluginEventRegistrar
    {
        /*
         * Lock must be held any time the internals lists are read/written
         * to avoid read/write enumeration issues.
         * 
         * This can happen when a manual unload is called duiring an automatic 
         * reload, or a runtime is tearing down the plugin environment 
         * when an automatic reload is happening.
         * 
         * This also allows thread safe register/unregister event listeners
         */
        private readonly object _stateLock = new();

        private readonly List<LivePlugin> _plugins;
        private readonly List<KeyValuePair<IPluginEventListener, object?>> _listeners;

        internal PluginController()
        {
            _plugins = new ();
            _listeners = new ();
        }

        /// <summary>
        /// The current collection of plugins. Valid before the unload event.
        /// </summary>
        public IEnumerable<LivePlugin> Plugins => _plugins;     

        ///<inheritdoc/>
        ///<exception cref="ArgumentNullException"></exception>
        public void Register(IPluginEventListener listener, object? state = null)
        {
            _ = listener ?? throw new ArgumentNullException(nameof(listener));

            lock (_stateLock)
            {
                _listeners.Add(new(listener, state));
            }
        }

        ///<inheritdoc/>
        public bool Unregister(IPluginEventListener listener)
        {
            lock(_stateLock)
            {
                //Remove listener
                return _listeners.RemoveAll(p => p.Key == listener) > 0;
            }
        }


        internal void InitializePlugins(Assembly asm)
        {
            lock (_stateLock)
            {
                //get all Iplugin types
                Type[] types = asm.GetTypes().Where(static type => !type.IsAbstract && typeof(IPlugin).IsAssignableFrom(type)).ToArray();

                //Initialize the new plugin instances
                IPlugin[] plugins = types.Select(static t => (IPlugin)Activator.CreateInstance(t)!).ToArray();

                //Crate new containers
                LivePlugin[] lps = plugins.Select(p => new LivePlugin(p, asm)).ToArray();

                //Store containers
                _plugins.AddRange(lps);
            }
        }

        internal void ConfigurePlugins(VnMemoryStream configData, string[] cliArgs)
        {
            lock (_stateLock)
            {
                _plugins.TryForeach(lp => lp.InitConfig(configData.AsSpan()));
                _plugins.TryForeach(lp => lp.InitLog(cliArgs));
            }
        }

        internal void LoadPlugins()
        {
            lock( _stateLock)
            {
                //Load all plugins
                _plugins.TryForeach(static p => p.LoadPlugin());

                //Notify event handlers
                _listeners.TryForeach(l => l.Key.OnPluginLoaded(this, l.Value));
            }
        }

        internal void UnloadPlugins()
        {
            lock (_stateLock)
            {
                try
                {
                    //Notify event handlers
                    _listeners.TryForeach(l => l.Key.OnPluginUnloaded(this, l.Value));

                    //Unload plugin instances
                    _plugins.TryForeach(static p => p.UnloadPlugin());
                }
                finally
                {
                    //Always 
                    _plugins.Clear();
                }
            }
        }

        internal void Dispose()
        {
            _plugins.Clear();
            _listeners.Clear();
        }
     
    }
}
