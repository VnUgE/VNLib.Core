/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginManager.cs 
*
* PluginManager.cs is part of VNLib.Plugins.Essentials.ServiceStack which 
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
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
using System.Collections.Generic;

using VNLib.Utils;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// A sealed type that manages the plugin interaction layer. Manages the lifetime of plugin
    /// instances, exposes controls, and relays stateful plugin events.
    /// </summary>
    internal sealed class PluginManager : VnDisposeable, IHttpPluginManager
    {
        private readonly IPluginInitializer _stack;     

        /// <summary>
        /// The collection of internal controllers
        /// </summary>
        public IEnumerable<IManagedPlugin> Plugins => _loadedPlugins;

        private IManagedPlugin[] _loadedPlugins;

        public PluginManager(IPluginInitializer stack)
        {
            _stack = stack;
            _loadedPlugins = Array.Empty<IManagedPlugin>();
        }

        /// <summary>
        /// Configures the manager to capture and manage plugins within a plugin stack
        /// </summary>
        /// <param name="debugLog"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AggregateException"></exception>
        public void LoadPlugins(ILogProvider debugLog)
        {
            _ = _stack ?? throw new InvalidOperationException("Plugin stack has not been set.");

            Check();

            //Initialize the plugin stack and store the loaded plugins
            _loadedPlugins = _stack.InitializePluginStack(debugLog);

            debugLog.Information("Plugin loading completed");
        }


        /// <inheritdoc/>
        public bool SendCommandToPlugin(string pluginName, string message, StringComparison nameComparison = StringComparison.Ordinal)
        {
            Check();

            foreach(IManagedPlugin plugin in _loadedPlugins)
            {
                if(plugin.SendCommandToPlugin(pluginName, message, nameComparison))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public void ForceReloadAllPlugins()
        {
            Check();

            //Reload all plugins, causing an event cascade
            _stack.ReloadPlugins();
        }

        /// <inheritdoc/>
        public void UnloadPlugins()
        {
            Check();

            //Unload all plugin controllers
            _stack.UnloadPlugins();

            /*
             * All plugin instances must be destroyed because the 
             * only way they will be loaded is from their files 
             * again, so they must be released
             */
            Free();
        }

        protected override void Free()
        {
            //Clear plugin table
            _loadedPlugins = Array.Empty<IManagedPlugin>();

            //Dispose the plugin stack
            _stack.Dispose();
        }
    }
}
