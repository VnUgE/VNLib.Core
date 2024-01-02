/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Reflection;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// A runtime .NET assembly loader specialized to load
    /// assemblies that export <see cref="IPlugin"/> types.
    /// </summary>
    public sealed class RuntimePluginLoader : VnDisposeable, IPluginReloadEventHandler
    {
        private static readonly IPluginAssemblyWatcher Watcher = new AssemblyWatcher();
       
        private readonly IPluginAssemblyLoader Loader;      
        private readonly ILogProvider? Log;

        /// <summary>
        /// Gets the plugin assembly loader configuration information
        /// </summary>
        public IPluginAssemblyLoadConfig Config => Loader.Config;

        /// <summary>
        /// Gets the plugin lifecycle controller
        /// </summary>
        public PluginController Controller { get; }

        /// <summary>
        /// Creates a new <see cref="RuntimePluginLoader"/> with the specified config and host config dom.
        /// </summary>
        /// <param name="loader">The plugin's assembly loader</param>
        /// <param name="log">A log provider to write plugin unload log events to</param>
        /// <exception cref="ArgumentNullException"></exception>
        public RuntimePluginLoader(IPluginAssemblyLoader loader, ILogProvider? log)
        {
            Log = log;
            Loader = loader ?? throw new ArgumentNullException(nameof(loader));

            //Configure watcher if requested
            if (loader.Config.WatchForReload)
            {
                Watcher.WatchAssembly(this, loader);
            }

            //Init container
            Controller = new();
        }

        /// <summary>
        /// Initializes the plugin loader, and populates the <see cref="Controller"/>
        /// with initialized plugins.
        /// </summary>
        /// <returns>A task that represents the initialization</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public void InitializeController()
        {
            //Prep the assembly loader
            Loader.Load();

            //Load the main assembly
            Assembly PluginAsm = Loader.GetAssembly();

            //Init container from the assembly
            Controller.InitializePlugins(PluginAsm);

            string[] cliArgs = Environment.GetCommandLineArgs();

            //Write the config to binary to pass it to the plugin
            using VnMemoryStream vms = new();

            //Read config data
            Loader.Config.ReadConfigurationData(vms);

            //Reset memstream
            vms.Seek(0, SeekOrigin.Begin);

            //Configure log/doms
            Controller.ConfigurePlugins(vms, cliArgs);
        }

        /// <summary>
        /// Loads all configured plugins by calling <see cref="IPlugin.Load"/>
        /// event hook on the current thread. Loading exceptions are aggregated so not
        /// to block individual loading.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void LoadPlugins() => Controller.LoadPlugins();

        /// <summary>
        /// Manually reload the internal <see cref="IPluginAssemblyLoader"/>
        /// which will reload the assembly and re-initialize the controller
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public void ReloadPlugins()
        {
            //Not unloadable
            if (!Loader.Config.Unloadable)
            {
                throw new NotSupportedException("The loading context is not unloadable, you may not dynamically reload plugins");
            }
            
            //All plugins must be unloaded first
            UnloadPlugins();

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
        public void UnloadPlugins() => Controller.UnloadPlugins();

        /// <summary>
        /// Attempts to unload all plugins within the lifecycle controller, all event handlers
        /// then attempts to unload the <see cref="IPluginAssemblyLoader"/> if dynamic unloading 
        /// is enabled, otherwise does nothing.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void UnloadAll()
        {
            UnloadPlugins();

            //If the assembly loader is unloadable calls its unload method
            if (Config.Unloadable)
            {
                Loader.Unload();
            }
        }

        //Process unload events

        void IPluginReloadEventHandler.OnPluginUnloaded(IPluginAssemblyLoader loader)
        {
            try
            {
                //All plugins must be unloaded before the assembly loader
                UnloadPlugins();

                //Unload the loader before initializing
                loader.Unload();

                //Reload the assembly and controller
                InitializeController();

                //Load plugins
                LoadPlugins();
            }
            catch (Exception ex)
            {
                Log?.Error("Failed reload plugins for {loader}\n{ex}", Config.AssemblyFile, ex);
            }
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            //Stop watching for events
            Watcher.StopWatching(this);

            //Cleanup
            Controller.Dispose();
            Loader.Dispose();
        }
    }
}
