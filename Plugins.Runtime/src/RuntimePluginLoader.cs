/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: DynamicPluginLoader.cs 
*
* DynamicPluginLoader.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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
using System.Text.Json;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Collections.Generic;

using McMaster.NETCore.Plugins;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;


namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// A runtime .NET assembly loader specialized to load
    /// assemblies that export <see cref="IPlugin"/> types.
    /// </summary>
    public class RuntimePluginLoader : VnDisposeable
    {
        protected readonly PluginLoader Loader;
        protected readonly string PluginPath;
        protected readonly JsonDocument HostConfig;
        protected readonly ILogProvider? Log;
        protected readonly LinkedList<LivePlugin> LoadedPlugins;

        /// <summary>
        /// A readonly collection of all loaded plugin wrappers
        /// </summary>
        public IReadOnlyCollection<LivePlugin> LivePlugins => LoadedPlugins;

        /// <summary>
        /// An event that is raised before the loader 
        /// unloads all plugin instances
        /// </summary>
        protected event EventHandler<PluginReloadedEventArgs>? OnBeforeReloaded;
        /// <summary>
        /// An event that is raised after a successfull reload of all new
        /// plugins for the instance
        /// </summary>
        protected event EventHandler? OnAfterReloaded;

        /// <summary>
        /// Raised when the current loader has reloaded the assembly and 
        /// all plugins were successfully loaded.
        /// </summary>
        public event EventHandler? Reloaded;

        /// <summary>
        /// The current plugin's JSON configuration DOM loaded from the plugin's directory
        /// if it exists. Only valid after first initalization
        /// </summary>
        public JsonDocument? PluginConfigDOM { get; private set; }
        /// <summary>
        /// Optional loader arguments object for the plugin
        /// </summary>
        protected JsonElement? LoaderArgs { get; private set; }
        
        /// <summary>
        /// The path of the plugin's configuration file. (Default = pluginPath.json)
        /// </summary>
        public string PluginConfigPath { get; init; }
        /// <summary>
        /// Creates a new <see cref="RuntimePluginLoader"/> with the specified 
        /// assembly location and host config.
        /// </summary>
        /// <param name="pluginPath"></param>
        /// <param name="log">A nullable log provider</param>
        /// <param name="hostConfig">The configuration DOM to merge with plugin config DOM and pass to enabled plugins</param>
        /// <param name="unloadable">A value that specifies if the assembly can be unloaded</param>
        /// <param name="hotReload">A value that spcifies if the loader will listen for changes to the assembly file and reload the plugins</param>
        /// <param name="lazy">A value that specifies if assembly dependencies are loaded on-demand</param>
        /// <remarks>
        /// The <paramref name="log"/> argument may be null if <paramref name="unloadable"/> is false
        /// </remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public RuntimePluginLoader(string pluginPath, JsonDocument? hostConfig = null, ILogProvider? log = null, bool unloadable = false, bool hotReload = false, bool lazy = false)
            :this(
            new PluginConfig(pluginPath)
            {
                IsUnloadable = unloadable || hotReload,
                EnableHotReload = hotReload,
                IsLazyLoaded = lazy,
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
        public RuntimePluginLoader(PluginConfig config, JsonDocument? hostConfig, ILogProvider? log)
        {
            //Add the assembly from which the IPlugin library was loaded from
            config.SharedAssemblies.Add(typeof(IPlugin).Assembly.GetName());
            
            //Default to empty config if null
            HostConfig = hostConfig ?? JsonDocument.Parse("{}");
            Loader = new(config);
            PluginPath = config.MainAssemblyPath;
            Log = log;
            Loader.Reloaded += Loader_Reloaded;
            //Set the config path default 
            PluginConfigPath = Path.ChangeExtension(PluginPath, ".json");
            LoadedPlugins = new();
        }

        private async void Loader_Reloaded(object sender, PluginReloadedEventArgs eventArgs)
        {
            try
            {
                //Invoke reloaded events
                OnBeforeReloaded?.Invoke(this, eventArgs);
                //Unload all endpoints
                LoadedPlugins.TryForeach(lp => lp.UnloadPlugin(Log));
                //Clear list of loaded plugins
                LoadedPlugins.Clear();
                //Unload the plugin config
                PluginConfigDOM?.Dispose();
                //Reload the assembly and 
                await InitLoaderAsync();
                //fire after loaded
                OnAfterReloaded?.Invoke(this, eventArgs);
                //Raise the external reloaded event
                Reloaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log?.Error(ex);
            }
        }

        /// <summary>
        /// Initializes the plugin loader, the assembly, and all public <see cref="IPlugin"/> 
        /// types
        /// </summary>
        /// <returns>A task that represents the initialization</returns>
        public async Task InitLoaderAsync()
        {
            //Load the main assembly
            Assembly PluginAsm = Loader.LoadDefaultAssembly();
            //Get the plugin's configuration file
            if (FileOperations.FileExists(PluginConfigPath))
            {
                //Open and read the config file
                await using FileStream confStream = File.OpenRead(PluginConfigPath);
                JsonDocumentOptions jdo = new()
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                };
                //parse the plugin config file
                PluginConfigDOM = await JsonDocument.ParseAsync(confStream, jdo);
                //Store the config loader args
                if (PluginConfigDOM.RootElement.TryGetProperty("loader_args", out JsonElement loaderEl))
                {
                    LoaderArgs = loaderEl;
                }
            }
            else
            {
                //Set plugin config dom to an empty object if the file does not exist
                PluginConfigDOM = JsonDocument.Parse("{}");
                LoaderArgs = null;
            }
            
            string[] cliArgs = Environment.GetCommandLineArgs();
            
            //Get all types that implement the IPlugin interface
            IEnumerable<IPlugin> plugins = PluginAsm.GetTypes().Where(static type => !type.IsAbstract && typeof(IPlugin).IsAssignableFrom(type))
                                            //Create the plugin instances
                                           .Select(static type => (Activator.CreateInstance(type) as IPlugin)!);
            //Load all plugins that implement the Iplugin interface
            foreach (IPlugin plugin in plugins)
            {
                //Load wrapper
                LivePlugin lp = new(plugin);
                try
                {
                    //Init config
                    lp.InitConfig(HostConfig, PluginConfigDOM);
                    //Init log handler
                    lp.InitLog(cliArgs);
                    //Load the plugin
                    lp.LoadPlugin();
                    //Create new plugin loader for the plugin
                    LoadedPlugins.AddLast(lp);
                }
                catch (TargetInvocationException te) when (te.InnerException is not null)
                {
                    throw te.InnerException;
                }
            }
        }
        /// <summary>
        /// Manually reload the internal <see cref="PluginLoader"/>
        /// which will reload the assembly and its plugins and endpoints
        /// </summary>
        public void ReloadPlugin() => Loader.Reload();

        /// <summary>
        /// Attempts to unload all plugins. 
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        public void UnloadAll() => LoadedPlugins.TryForeach(lp => lp.UnloadPlugin(Log));

        ///<inheritdoc/>
        protected override void Free()
        {
            Loader.Dispose();
            PluginConfigDOM?.Dispose();
        }

    }
}