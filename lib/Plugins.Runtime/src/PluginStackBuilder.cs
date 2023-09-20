/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;

using VNLib.Utils.IO;
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
        private byte[]? HostConfigData;
        private ILogProvider? DebugLog;

        private Func<IPluginConfig, IAssemblyLoader>? Loader;

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
        /// <param name="hostConfig"></param>
        /// <returns>The current builder instance for chaining</returns>
        public PluginStackBuilder WithConfigurationData(ReadOnlySpan<byte> hostConfig)
        {
            //Store binary copy
            HostConfigData = hostConfig.ToArray();
            return this;
        }

        /// <summary>
        /// The factory callback function used to get assembly loaders for 
        /// discovered plugins
        /// </summary>
        /// <param name="loaderFactory">The factory callback funtion</param>
        /// <returns>The current builder instance for chaining</returns>
        public PluginStackBuilder WithLoaderFactory(Func<IPluginConfig, IAssemblyLoader> loaderFactory)
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
        public IPluginStack BuildStack()
        {
            _ = DiscoveryManager ?? throw new ArgumentException("You must specify a plugin discovery manager");

            //Create a default config if none was specified
            HostConfigData ??= GetEmptyConfig();

            //Clone the current builder state
            PluginStackBuilder clone = (PluginStackBuilder)MemberwiseClone();

            return new PluginStack(clone);
        }

        private static byte[] GetEmptyConfig() => Encoding.UTF8.GetBytes("{}");


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
                    PlugingAssemblyConfig pConf = new(Builder.HostConfigData)
                    {
                        AssemblyFile = pluginPath,
                        WatchForReload = Builder.HotReload,
                        ReloadDelay = Builder.ReloadDelay,
                        Unloadable = Builder.HotReload
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

        internal sealed record class PluginAsmLoader(IAssemblyLoader Loader, IPluginConfig Config) : IPluginAssemblyLoader
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

        internal sealed record class PlugingAssemblyConfig(ReadOnlyMemory<byte> HostConfig) : IPluginConfig
        {
            ///<inheritdoc/>
            public bool Unloadable { get; init; }

            ///<inheritdoc/>
            public string AssemblyFile { get; init; } = string.Empty;

            ///<inheritdoc/>
            public bool WatchForReload { get; init; }

            ///<inheritdoc/>
            public TimeSpan ReloadDelay { get; init; }

            /*
             * The plugin config file is the same as the plugin assembly file, 
             * but with the .json extension
             */
            private string PluginConfigFile => Path.ChangeExtension(AssemblyFile, ".json");

            ///<inheritdoc/>
            public void ReadConfigurationData(Stream outputStream)
            {
                //Allow comments and trailing commas
                JsonDocumentOptions jdo = new()
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                };

                using JsonDocument hConfig = JsonDocument.Parse(HostConfig, jdo);

                //Read the plugin config file
                if (FileOperations.FileExists(PluginConfigFile))
                {
                    //Open file stream to read data
                    using FileStream confStream = File.OpenRead(PluginConfigFile);

                    //Parse the config file
                    using JsonDocument pConfig = JsonDocument.Parse(confStream, jdo);

                    //Merge the configs
                    using JsonDocument merged = hConfig.Merge(pConfig,"host", "plugin");

                    //Write the merged config to the output stream
                    using Utf8JsonWriter writer = new(outputStream);
                    merged.WriteTo(writer);
                }
                else
                {
                    byte[] pluginConfig = Encoding.UTF8.GetBytes("{}");

                    using JsonDocument pConfig = JsonDocument.Parse(pluginConfig, jdo);

                    //Merge the configs
                    using JsonDocument merged = hConfig.Merge(pConfig,"host", "plugin");

                    //Write the merged config to the output stream
                    using Utf8JsonWriter writer = new(outputStream);
                    merged.WriteTo(writer);
                }
            }
        }
    }
}
