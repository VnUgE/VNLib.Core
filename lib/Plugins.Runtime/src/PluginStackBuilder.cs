/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: PluginStackBuilder.cs 
*
* PluginStackBuilder.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Runtime
{

    /// <summary>
    /// A construction class used to build a single plugin stack. 
    /// </summary>
    public sealed class PluginStackBuilder
    {
        private IPluginDiscoveryManager? DiscoveryManager;
        private bool HotReload;
        private TimeSpan ReloadDelay;
        private IPluginConfigReader? PluginConfig;
        private ILogProvider? DebugLog;

        private Func<IPluginAssemblyLoadConfig, IAssemblyLoader>? Loader;

        /// <summary>
        /// Shortcut constructor for easy fluent chaining.
        /// </summary>
        /// <returns>A new <see cref="PluginStackBuilder"/></returns>
        public static PluginStackBuilder Create() => new();

        /// <summary>
        /// Sets the plugin discovery manager used to find plugins
        /// </summary>
        /// <param name="discoveryManager">The discovery manager instance</param>
        /// <returns>The current builder instance for chaining</returns>
        public PluginStackBuilder WithDiscoveryManager(IPluginDiscoveryManager discoveryManager)
        {
            DiscoveryManager = discoveryManager;
            return this;
        }

        /// <summary>
        /// Enables hot reloading of the plugin assembly
        /// </summary>
        /// <param name="reloadDelay">The delay time after a change is detected before the assembly is reloaded</param>
        /// <returns>The current builder instance for chaining</returns>
        public PluginStackBuilder EnableHotReload(TimeSpan reloadDelay)
        {
            HotReload = true;
            ReloadDelay = reloadDelay;
            return this;
        }

        /// <summary>
        /// Specifies the JSON host configuration data to pass to the plugin
        /// </summary>
        /// <param name="pluginConfig">The plugin configuration data</param>
        /// <returns>The current builder instance for chaining</returns>
        public PluginStackBuilder WithConfigurationReader(IPluginConfigReader pluginConfig)
        {
            ArgumentNullException.ThrowIfNull(pluginConfig);

            //Store binary copy
            PluginConfig = pluginConfig;
            return this;
        }

        /// <summary>
        /// The factory callback function used to get assembly loaders for 
        /// discovered plugins
        /// </summary>
        /// <param name="loaderFactory">The factory callback funtion</param>
        /// <returns>The current builder instance for chaining</returns>
        public PluginStackBuilder WithLoaderFactory(Func<IPluginAssemblyLoadConfig, IAssemblyLoader> loaderFactory)
        {
            Loader = loaderFactory;
            return this;
        }

        /// <summary>
        /// Specifies the optional debug log provider to use for the plugin loader.
        /// </summary>
        /// <param name="logProvider">The optional log provider instance</param>
        ///<returns>The current builder instance for chaining</returns>
        public PluginStackBuilder WithDebugLog(ILogProvider logProvider)
        {
            DebugLog = logProvider;
            return this;
        }

        /// <summary>
        /// Creates a snapshot of the current builder state and builds a plugin stack
        /// </summary>
        /// <returns>The current builder instance for chaining</returns>
        /// <exception cref="ArgumentException"></exception>
        public IPluginStack ConfigureStack()
        {
            _ = DiscoveryManager ?? throw new ArgumentException("You must specify a plugin discovery manager");
            _ = PluginConfig ?? throw new ArgumentException("A plugin confuration reader must be specified");

            //Clone the current builder state
            PluginStackBuilder clone = (PluginStackBuilder)MemberwiseClone();

            return new PluginStack(clone);
        }


        /*
         * 
         */
        internal sealed record class PluginStack(PluginStackBuilder Builder) : IPluginStack
        {
            private readonly LinkedList<RuntimePluginLoader> _plugins = new();

            ///<inheritdoc/>
            public IReadOnlyCollection<RuntimePluginLoader> Plugins => _plugins;

            ///<inheritdoc/>
            public void BuildStack()
            {
                //Discover all plugins
                IPluginAssemblyLoader[] loaders = DiscoverPlugins(Builder.DebugLog);

                //Create a loader for each plugin
                foreach (IPluginAssemblyLoader loader in loaders)
                {
                    RuntimePluginLoader plugin = new(loader, Builder.DebugLog);
                    _plugins.AddLast(plugin);
                }
            }

            private IPluginAssemblyLoader[] DiscoverPlugins(ILogProvider? debugLog)
            {
                //Select only dirs with a dll that is named after the directory name
                IEnumerable<string> pluginPaths = Builder.DiscoveryManager!.DiscoverPluginFiles();

                //Log the found plugin files
                IEnumerable<string> pluginFileNames = pluginPaths.Select(static s => $"{Path.GetFileName(s)}\n");
                debugLog?.Debug("Found plugin assemblies: \n{files}", string.Concat(pluginFileNames));

                LinkedList<IPluginAssemblyLoader> loaders = new ();

                //Create a loader for each plugin
                foreach (string pluginPath in pluginPaths)
                {
                    PlugingAssemblyConfig pConf = new(Builder.PluginConfig!)
                    {
                        AssemblyFile    = pluginPath,
                        WatchForReload  = Builder.HotReload,
                        ReloadDelay     = Builder.ReloadDelay,
                        Unloadable      = Builder.HotReload
                    };

                    //Get assembly loader from the configration
                    IAssemblyLoader loader = Builder.Loader!.Invoke(pConf);

                    //Add to list
                    loaders.AddLast(new PluginAsmLoader(loader, pConf));
                }

                return loaders.ToArray();
            }


            ///<inheritdoc/>
            public void Dispose()
            {
                //dispose all plugins
                _plugins.TryForeach(static p => p.Dispose());
                _plugins.Clear();
            }
        }

        internal sealed record class PluginAsmLoader(IAssemblyLoader Loader, IPluginAssemblyLoadConfig Config) : IPluginAssemblyLoader
        {
            ///<inheritdoc/>
            public void Dispose() => Loader.Dispose();

            ///<inheritdoc/>
            public Assembly GetAssembly() => Loader.GetAssembly();

            ///<inheritdoc/>
            public void Load() => Loader.Load();

            ///<inheritdoc/>
            public void Unload() => Loader.Unload();
        }

        internal sealed record class PlugingAssemblyConfig(IPluginConfigReader Config) : IPluginAssemblyLoadConfig
        {
            ///<inheritdoc/>
            public bool Unloadable { get; init; }

            ///<inheritdoc/>
            public string AssemblyFile { get; init; } = string.Empty;

            ///<inheritdoc/>
            public bool WatchForReload { get; init; }

            ///<inheritdoc/>
            public TimeSpan ReloadDelay { get; init; }

            ///<inheritdoc/>
            public void ReadConfigurationData(Stream outputStream) => Config.ReadPluginConfigData(this, outputStream);
        }
    }
}
