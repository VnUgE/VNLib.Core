/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: RuntimePluginLoader.cs 
*
* RuntimePluginLoader.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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
using System.IO;
using System.Diagnostics;
using System.Reflection;

using VNLib.Utils;
using VNLib.Utils.Logging;

using VNLib.Plugins.Runtime.Events;
using VNLib.Plugins.Runtime.Watcher;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// A runtime .NET assembly loader specialized to load
    /// assemblies that export <see cref="IPlugin"/> types.
    /// </summary>
    public sealed class RuntimePluginLoader : VnDisposeable, IPluginReloadEventHandler
    {
        private readonly IAssemblyLoader _loader;
        private readonly ILogProvider? _log;
        private readonly IDisposable? _watcher;

        /// <summary>
        /// Gets the plugin assembly loader configuration information
        /// </summary>
        public IPluginAssemblyLoadConfig Config { get; }

        /// <summary>
        /// Gets the plugin lifecycle controller
        /// </summary>
        public PluginController Controller { get; }

        /// <summary>
        /// Creates a new <see cref="RuntimePluginLoader"/> with the specified config and host config dom.
        /// </summary>
        /// <param name="config">The plugin's assembly loader configuration</param>
        /// <param name="log">A log provider to write plugin unload log events to</param>
        /// <exception cref="ArgumentNullException"></exception>
        public RuntimePluginLoader(IPluginAssemblyLoadConfig config, ILogProvider? log)
        {
            ArgumentNullException.ThrowIfNull(config);

            Config = config;

            _log = log;
            _loader = config.GetLoader();

            //Configure watcher if requested
            if (config.WatchForReload)
            {
                _watcher = AssemblyWatcher.WatchAssembly(this, config);
            }

            //Init container
            Controller = new(config);
        }

        /// <summary>
        /// Initializes the plugin loader, and populates the <see cref="Controller"/>
        /// with initialized plugins.
        /// </summary>
        /// <exception cref="IOException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void InitializeController()
        {
            Check();

            //Prep the assembly loader
            _loader.Load();

            //Load the main assembly
            Assembly PluginAsm = _loader.GetAssembly();

            //Init container from the assembly
            Controller.InitializePlugins(PluginAsm);
        }

        /// <summary>
        /// Loads all configured plugins by calling <see cref="IPlugin.Load"/>
        /// event hook on the current thread. Loading exceptions are aggregated so not
        /// to block individual loading.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void LoadPlugins()
        {
            Check();

            Controller.LoadPlugins();
        }

        /// <summary>
        /// Manually reload the internal <see cref="IAssemblyLoader"/>
        /// which will reload the assembly and re-initialize the controller
        /// </summary>
        /// <param name="forceGc">A value that indicates if the current unload should cause a manual garbage collection</param>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void ReloadPlugins(bool forceGc)
        {
            Check();

            //Not unloadable
            if (!Config.Unloadable)
            {
                throw new NotSupportedException("The loading context is not unloadable, you may not dynamically reload plugins");
            }

            //All plugins must be unloaded first
            UnloadAll(forceGc);

            //Reload the assembly and 
            InitializeController();

            //Load plugins
            LoadPlugins();
        }

        /// <summary>
        /// Calls the <see cref="IPlugin.Unload"/> method for all plugins within the lifecycle controller
        /// and invokes the <see cref="IPluginEventListener.OnPluginUnloaded(PluginController, object?)"/>
        /// for all listeners.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void UnloadPlugins()
        {
            Check();

            Controller.UnloadPlugins();
        }

        /// <summary>
        /// Attempts to unload all plugins within the lifecycle controller, all event handlers
        /// then attempts to unload the <see cref="IAssemblyLoader"/> if dynamic unloading 
        /// is enabled, otherwise does nothing.
        /// </summary>
        /// <param name="forceGc">A value that indicates if the current unload should cause a manual garbage collection</param>
        /// <exception cref="AggregateException"></exception>
        public void UnloadAll(bool forceGc)
        {
            UnloadPlugins(); // Guards disposed state

            //If the assembly loader is unloadable calls its unload method
            if (Config.Unloadable)
            {
                _loader.Unload();
            }

            //Optionally wait for GC to finish
            if (forceGc)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        //Process unload events

        void IPluginReloadEventHandler.OnAssemblyFileChanged()
        {
            Debug.Assert(!Disposed, "Received assembly file change event after disposal, this should not happen");

            try
            {
                //All plugins must be unloaded before the assembly loader
                UnloadPlugins();

                //Unload the loader before initializing
                _loader.Unload();

                //Reload the assembly and controller
                InitializeController();

                //Load plugins
                LoadPlugins();
            }
            catch (Exception ex)
            {
                _log?.Error("Failed reload plugins for {loader}\n{ex}", Config.AssemblyFile, ex);
            }
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            //Cleanup
            _watcher?.Dispose();
            Controller.Dispose();
            _loader.Dispose();
        }    
    }
}
