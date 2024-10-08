﻿/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Linq;
using System.Reflection;

using VNLib.Utils.Resources;
using VNLib.Plugins.Attributes;

namespace VNLib.Plugins.Runtime
{

    /// <summary>
    /// <para>
    /// Wrapper for a loaded <see cref="IPlugin"/> instance, used internally 
    /// for a single instance. 
    /// </para>
    /// <para>
    /// Lifetime: for the existance of a single loaded 
    /// plugin instance. Created once per loaded plugin instance. Once the plugin
    /// is unloaded, it is no longer useable.
    /// </para>
    /// </summary>
    public class LivePlugin : IEquatable<IPlugin>, IEquatable<LivePlugin>
    {
        private bool _loaded;

        /// <summary>
        /// The plugin's <see cref="IPlugin.PluginName"/> property during load time
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public string PluginName => Plugin?.PluginName ?? throw new InvalidOperationException("Plugin is not loaded");

        /// <summary>
        /// The underlying <see cref="IPlugin"/> that is warpped
        /// by he current instance
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
       
        private ConsoleEventHandlerSignature? PluginConsoleHandler;

        internal LivePlugin(IPlugin plugin, Assembly originAsm)
        {
            Plugin = plugin;
            OriginAsm = originAsm;
            PluginType = plugin.GetType();
            PluginConsoleHandler = GetConsoleHandler(plugin);
        }

        private static ConsoleEventHandlerSignature? GetConsoleHandler(IPlugin plugin)
        {
            //Get a delegate handler for the plugin
            return ManagedLibrary.GetMethodsWithAttribute<ConsoleEventHandlerAttribute, ConsoleEventHandlerSignature>(plugin)
                .FirstOrDefault();
        }

        /// <summary>
        /// Sets the plugin's configuration if it defines a <see cref="ConfigurationInitalizerAttribute"/>
        /// on an instance method
        /// </summary>
        /// <param name="configData">The host configuration DOM</param>
        internal void InitConfig(ReadOnlySpan<byte> configData)
        {
            ManagedLibrary.GetMethodsWithAttribute<ConfigurationInitalizerAttribute, ConfigInitializer>(Plugin!)
                .FirstOrDefault()
                ?.Invoke(configData);
        }
        
        /// <summary>
        /// Invokes the plugin's log initalizer method if it defines a <see cref="LogInitializerAttribute"/>
        /// on an instance method
        /// </summary>
        /// <param name="cliArgs">The current process's CLI args</param>
        internal void InitLog(string[] cliArgs)
        {
            ManagedLibrary.GetMethodsWithAttribute<LogInitializerAttribute, LogInitializer>(Plugin!)
                .FirstOrDefault()
                ?.Invoke(cliArgs);
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
        /// Invokes the plugins console event handler if the type has one 
        /// and the plugin is loaded.
        /// </summary>
        /// <param name="message">The message to pass to the plugin handler</param>
        /// <returns>
        /// True if the command was sent to the plugin, false if the plugin is
        /// unloaded or did not export a console event handler
        /// </returns>
        public bool SendConsoleMessage(string message)
        {
            //Make sure plugin is loaded and has a console handler
            if (PluginConsoleHandler == null)
            {
                return false;
            }
            //Invoke plugin console handler
            PluginConsoleHandler(message);
            return true;
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
            //Remove delegate handler to the plugin to remove refs
            PluginConsoleHandler = null;

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
        public override bool Equals(object? obj)
        {
            Type? pluginType = Plugin?.GetType();
            Type? otherType = obj?.GetType();
            if(pluginType == null || otherType == null)
            {
                return false;
            }
            //If the other plugin is the same type as the current instance return true
            return pluginType.FullName == otherType.FullName;
        }
        ///<inheritdoc/>
        public bool Equals(LivePlugin? other)
        {
            return Equals(other?.Plugin);
        }
        ///<inheritdoc/>
        public bool Equals(IPlugin? other)
        {
            return Equals((object?)other);
        }
        ///<inheritdoc/>
        public override int GetHashCode()
        {
            return Plugin?.GetHashCode() ?? throw new InvalidOperationException("Plugin is null");
        }       
    }
}
