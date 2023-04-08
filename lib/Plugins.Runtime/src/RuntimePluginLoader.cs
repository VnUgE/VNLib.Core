/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Text.Json;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

using McMaster.NETCore.Plugins;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// A runtime .NET assembly loader specialized to load
    /// assemblies that export <see cref="IPlugin"/> types.
    /// </summary>
    public sealed class RuntimePluginLoader : VnDisposeable
    {
        private readonly PluginLoader Loader;
        private readonly string PluginPath;
        private readonly JsonDocument HostConfig;
        private readonly ILogProvider? Log;

        /// <summary>
        /// Gets the plugin lifetime manager.
        /// </summary>
        public PluginController Controller { get; }
        
        /// <summary>
        /// The path of the plugin's configuration file. (Default = pluginPath.json)
        /// </summary>
        public string PluginConfigPath { get; }
        
        /// <summary>
        /// Creates a new <see cref="RuntimePluginLoader"/> with the specified 
        /// assembly location and host config.
        /// </summary>
        /// <param name="pluginPath"></param>
        /// <param name="log">A nullable log provider</param>
        /// <param name="hostConfig">The configuration DOM to merge with plugin config DOM and pass to enabled plugins</param>
        /// <param name="unloadable">A value that specifies if the assembly can be unloaded</param>
        /// <param name="hotReload">A value that spcifies if the loader will listen for changes to the assembly file and reload the plugins</param>
        /// <remarks>
        /// The <paramref name="log"/> argument may be null if <paramref name="unloadable"/> is false
        /// </remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public RuntimePluginLoader(string pluginPath, JsonElement? hostConfig = null, ILogProvider? log = null, bool unloadable = false, bool hotReload = false)
            :this(
            new PluginConfig(pluginPath)
            {
                IsUnloadable = unloadable || hotReload,
                EnableHotReload = hotReload,
                ReloadDelay = TimeSpan.FromSeconds(1),
                PreferSharedTypes = true,
                DefaultContext = AssemblyLoadContext.Default
            },
            hostConfig, log)
        {
        }
        
        /// <summary>
        /// Creates a new <see cref="RuntimePluginLoader"/> with the specified config and host config dom.
        /// </summary>
        /// <param name="config">The plugin's loader configuration </param>
        /// <param name="hostConfig">The host/process configuration DOM</param>
        /// <param name="log">A log provider to write plugin unload log events to</param>
        /// <exception cref="ArgumentNullException"></exception>
        public RuntimePluginLoader(PluginConfig config, JsonElement? hostConfig, ILogProvider? log)
        {
            //Shared types is required so the default load context shares types
            config.PreferSharedTypes = true;

            //Default to empty config if null, otherwise clone a copy of the host config element
            HostConfig = hostConfig.HasValue ? Clone(hostConfig.Value) : JsonDocument.Parse("{}");

            Loader = new(config);
            PluginPath = config.MainAssemblyPath;
            Log = log;

            //Only regiser reload handler if the load context is unloadable
            if (config.IsUnloadable)
            {
                //Init reloaded event handler
                Loader.Reloaded += Loader_Reloaded;
            }

            //Set the config path default 
            PluginConfigPath = Path.ChangeExtension(PluginPath, ".json");

            //Init container
            Controller = new();
        }

        private async void Loader_Reloaded(object sender, PluginReloadedEventArgs eventArgs)
        {
            try
            {
                //All plugins must be unloaded forst
                UnloadAll();

                //Reload the assembly and 
                await InitializeController();

                //Load plugins
                LoadPlugins();
            }
            catch (Exception ex)
            {
                Log?.Error("Failed reload plugins for {loader}\n{ex}", PluginPath, ex);
            }
        }

        /// <summary>
        /// Initializes the plugin loader, and populates the <see cref="Controller"/>
        /// with initialized plugins.
        /// </summary>
        /// <returns>A task that represents the initialization</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task InitializeController()
        {
            JsonDocument? pluginConfig = null;

            try
            {
                //Get the plugin's configuration file
                if (FileOperations.FileExists(PluginConfigPath))
                {
                    pluginConfig = await this.GetPluginConfigAsync();
                }
                else
                {
                    //Set plugin config dom to an empty object if the file does not exist
                    pluginConfig = JsonDocument.Parse("{}");
                }

                //Load the main assembly
                Assembly PluginAsm = Loader.LoadDefaultAssembly();

                //Init container from the assembly
                Controller.InitializePlugins(PluginAsm);

                string[] cliArgs = Environment.GetCommandLineArgs();

                //Configure log/doms
                Controller.ConfigurePlugins(HostConfig, pluginConfig, cliArgs);
            }
            finally
            {
                pluginConfig?.Dispose();
            }
        }

        /// <summary>
        /// Loads all configured plugins by calling <see cref="IPlugin.Load"/>
        /// event hook on the current thread. Loading exceptions are aggregated so not
        /// to block individual loading.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void LoadPlugins() => Controller.LoadPlugins();

        /// <summary>
        /// Manually reload the internal <see cref="PluginLoader"/>
        /// which will reload the assembly and its plugins
        /// </summary>
        public void ReloadPlugins() => Loader.Reload();

        /// <summary>
        /// Attempts to unload all plugins. 
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void UnloadAll() => Controller.UnloadPlugins();

        ///<inheritdoc/>
        protected override void Free()
        {
            Controller.Dispose();
            Loader.Dispose();
            HostConfig.Dispose();
        }


        private static JsonDocument Clone(JsonElement hostConfig)
        {
            //Crate ms to write the current doc data to
            using VnMemoryStream ms = new();

            using (Utf8JsonWriter writer = new(ms))
            {
                hostConfig.WriteTo(writer);
            }

            //Reset ms
            ms.Seek(0, SeekOrigin.Begin);
            
            return JsonDocument.Parse(ms);
        }
    }
}