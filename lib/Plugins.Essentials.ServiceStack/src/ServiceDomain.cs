/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: ServiceDomain.cs 
*
* ServiceDomain.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
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
using System.Net;
using System.Text.Json;
using System.Diagnostics;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Extensions;
using VNLib.Utils.Logging;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Essentials.Content;
using VNLib.Plugins.Essentials.Sessions;


namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Represents a domain of services and thier dynamically loaded plugins 
    /// that will be hosted by an application service stack
    /// </summary>
    public sealed class ServiceDomain : VnDisposeable, IPluginController
    {
        private const string PLUGIN_FILE_EXTENSION = ".dll";
        private const string DEFUALT_PLUGIN_DIR = "/plugins";
        private const string PLUGINS_CONFIG_ELEMENT = "plugins";

        private readonly LinkedList<ServiceGroup> _serviceGroups;
        private readonly LinkedList<RuntimePluginLoader> _pluginLoaders;
        
        /// <summary>
        /// Enumerates all loaded plugin instances
        /// </summary>
        public IEnumerable<IPlugin> Plugins => _pluginLoaders.SelectMany(static s => 
                    s.LivePlugins.Where(static p => p.Plugin != null)
                    .Select(static s => s.Plugin!)
                );

        /// <summary>
        /// Gets all service groups loaded in the service manager
        /// </summary>
        public IReadOnlyCollection<ServiceGroup> ServiceGroups => _serviceGroups;

        /// <summary>
        /// Initializes a new empty <see cref="ServiceDomain"/>
        /// </summary>
        public ServiceDomain()
        {
            _serviceGroups = new();
            _pluginLoaders = new();
        }

        /// <summary>
        /// Uses the supplied callback to get a collection of virtual hosts
        /// to build the current domain with
        /// </summary>
        /// <param name="hostBuilder">The callback method to build virtual hosts</param>
        /// <returns>A value that indicates if any virtual hosts were successfully loaded</returns>
        public bool BuildDomain(Action<ICollection<IServiceHost>> hostBuilder)
        {
            //LL to store created hosts
            LinkedList<IServiceHost> hosts = new();

            //build hosts
            hostBuilder.Invoke(hosts);

            return FromExisting(hosts);
        }

        /// <summary>
        /// Builds the domain from an existing enumeration of virtual hosts
        /// </summary>
        /// <param name="hosts">The enumeration of virtual hosts</param>
        /// <returns>A value that indicates if any virtual hosts were successfully loaded</returns>
        public bool FromExisting(IEnumerable<IServiceHost> hosts)
        {
            //Get service groups and pass service group list
            CreateServiceGroups(_serviceGroups, hosts);
            return _serviceGroups.Any();
        }
       
        private static void CreateServiceGroups(ICollection<ServiceGroup> groups, IEnumerable<IServiceHost> hosts)
        {
            //Get distinct interfaces
            IPEndPoint[] interfaces = hosts.Select(static s => s.TransportInfo.TransportEndpoint).Distinct().ToArray();

            //Select hosts of the same interface to create a group from
            foreach (IPEndPoint iface in interfaces)
            {
                IEnumerable<IServiceHost> groupHosts = hosts.Where(host => host.TransportInfo.TransportEndpoint.Equals(iface));

                IServiceHost[]? overlap = groupHosts.Where(vh => groupHosts.Select(static s => s.Processor.Hostname).Count(hostname => vh.Processor.Hostname == hostname) > 1).ToArray();

                foreach (IServiceHost vh in overlap)
                {
                    throw new ArgumentException($"The hostname '{vh.Processor.Hostname}' is already in use by another virtual host");
                }

                //init new service group around an interface and its roots
                ServiceGroup group = new(iface, groupHosts);

                groups.Add(group);
            }
        }

        ///<inheritdoc/>
        public Task LoadPlugins(JsonDocument config, ILogProvider appLog)
        {
            if (!config.RootElement.TryGetProperty(PLUGINS_CONFIG_ELEMENT, out JsonElement pluginEl))
            {
                appLog.Information("Plugins element not defined in config, skipping plugin loading");
                return Task.CompletedTask;
            }

            //Get the plugin directory, or set to default
            string pluginDir = pluginEl.GetPropString("path") ?? Path.Combine(Directory.GetCurrentDirectory(), DEFUALT_PLUGIN_DIR);
            //Get the hot reload flag
            bool hotReload = pluginEl.TryGetProperty("hot_reload", out JsonElement hrel) && hrel.GetBoolean();

            //Load all virtual file assemblies withing the plugin folder
            DirectoryInfo dir = new(pluginDir);

            if (!dir.Exists)
            {
                appLog.Warn("Plugin directory {dir} does not exist. No plugins were loaded", pluginDir);
                return Task.CompletedTask;
            }

            appLog.Information("Loading plugins. Hot-reload: {en}", hotReload);

            //Enumerate all dll files within this dir
            IEnumerable<DirectoryInfo> dirs = dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);

            //Select only dirs with a dll that is named after the directory name
            IEnumerable<string> pluginPaths = dirs.Where(static pdir =>
            {
                string compined = Path.Combine(pdir.FullName, pdir.Name);
                string FilePath = string.Concat(compined, PLUGIN_FILE_EXTENSION);
                return FileOperations.FileExists(FilePath);
            })
            //Return the name of the dll file to import
            .Select(static pdir =>
            {
                string compined = Path.Combine(pdir.FullName, pdir.Name);
                return string.Concat(compined, PLUGIN_FILE_EXTENSION);
            });

            IEnumerable<string> pluginFileNames = pluginPaths.Select(static s => $"{Path.GetFileName(s)}\n");

            appLog.Debug("Found plugin files: \n{files}", string.Concat(pluginFileNames));

            LinkedList<Task> loading = new();

            object listLock = new();

            foreach (string pluginPath in pluginPaths)
            {
                async Task Load()
                {
                    string pluginName = Path.GetFileName(pluginPath);

                    RuntimePluginLoader plugin = new(pluginPath, config, appLog, hotReload, hotReload);
                    Stopwatch sw = new();
                    try
                    {
                        sw.Start();
                        await plugin.InitLoaderAsync();
                        //Listen for reload events to remove and re-add endpoints
                        plugin.Reloaded += OnPluginReloaded;

                        lock (listLock)
                        {
                            //Add to list
                            _pluginLoaders.AddLast(plugin);
                        }

                        sw.Stop();

                        appLog.Verbose("Loaded {pl} in {tm} ms", pluginName, sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        appLog.Error(ex, $"Exception raised during loading {pluginName}. Failed to load plugin \n{ex}");
                        plugin.Dispose();
                    }
                    finally
                    {
                        sw.Stop();
                    }
                }

                loading.AddLast(Load());
            }

            //Continuation to add all initial plugins to the service manager
            void Continuation(Task t)
            {
                appLog.Verbose("Plugins loaded");

                //Add inital endpoints for all plugins
                _pluginLoaders.TryForeach(ldr => _serviceGroups.TryForeach(sg => sg.AddOrUpdateEndpointsForPlugin(ldr)));

                //Init session provider
                InitSessionProvider();

                //Init page router
                InitPageRouter();
            }

            //wait for loading to completed
            return Task.WhenAll(loading.ToArray()).ContinueWith(Continuation, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        ///<inheritdoc/>
        public bool SendCommandToPlugin(string pluginName, string message, StringComparison nameComparison = StringComparison.Ordinal)
        {
            Check();
            //Find the single plugin by its name
            LivePlugin? pl = _pluginLoaders.Select(p =>
                                    p.LivePlugins.Where(lp => pluginName.Equals(lp.PluginName, nameComparison))
                                )
                            .SelectMany(static lp => lp)
                            .SingleOrDefault();
            //Send the command
            return pl?.SendConsoleMessage(message) ?? false;
        }

        ///<inheritdoc/>
        public void ForceReloadAllPlugins()
        {
            Check();
            _pluginLoaders.TryForeach(static pl => pl.ReloadPlugin());
        }

        ///<inheritdoc/>
        public void UnloadAll()
        {
            Check();

            //Unload service groups before unloading plugins
            _serviceGroups.TryForeach(static sg => sg.UnloadAll());
            //empty service groups
            _serviceGroups.Clear();

            //Unload all plugins
            _pluginLoaders.TryForeach(static pl => pl.UnloadAll());
        }

        private void OnPluginReloaded(object? plugin, EventArgs empty)
        {
            //Update endpoints for the loader
            RuntimePluginLoader reloaded = (plugin as RuntimePluginLoader)!;

            //Update all endpoints for the plugin
            _serviceGroups.TryForeach(sg => sg.AddOrUpdateEndpointsForPlugin(reloaded));
        }

        private void InitSessionProvider()
        {
            //Callback to reload provider
            void onSessionProviderReloaded(ISessionProvider old, ISessionProvider current)
            {
                _serviceGroups.TryForeach(sg => sg.UpdateSessionProvider(current));
            }

            try
            {
                //get the loader that contains the single session provider
                RuntimePluginLoader? sessionLoader = _pluginLoaders
                    .Where(static s => s.ExposesType<ISessionProvider>())
                    .SingleOrDefault();

                //If session provider has been supplied, load it
                if (sessionLoader != null)
                {
                    //Get the session provider from the plugin loader
                    ISessionProvider sp = sessionLoader.GetExposedTypeFromPlugin<ISessionProvider>()!;

                    //Init inital provider
                    onSessionProviderReloaded(null!, sp);

                    //Register reload event
                    sessionLoader.RegisterListenerForSingle<ISessionProvider>(onSessionProviderReloaded);
                }
            }
            catch (InvalidOperationException)
            {
                throw new TypeLoadException("More than one session provider plugin was defined in the plugin directory, cannot continue");
            }
        }

        private void InitPageRouter()
        {
            //Callback to reload provider
            void onRouterReloaded(IPageRouter old, IPageRouter current)
            {
                _serviceGroups.TryForeach(sg => sg.UpdatePageRouter(current));
            }

            try
            {

                //get the loader that contains the single page router
                RuntimePluginLoader? routerLoader = _pluginLoaders
                    .Where(static s => s.ExposesType<IPageRouter>())
                    .SingleOrDefault();

                //If router has been supplied, load it
                if (routerLoader != null)
                {
                    //Get initial value
                    IPageRouter sp = routerLoader.GetExposedTypeFromPlugin<IPageRouter>()!;

                    //Init inital provider
                    onRouterReloaded(null!, sp);

                    //Register reload event
                    routerLoader.RegisterListenerForSingle<IPageRouter>(onRouterReloaded);
                }
            }
            catch (InvalidOperationException)
            {
                throw new TypeLoadException("More than one page router plugin was defined in the plugin directory, cannot continue");
            }
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            //Dispose loaders
            _pluginLoaders.TryForeach(static pl => pl.Dispose());
            _pluginLoaders.Clear();
            _serviceGroups.Clear();
        }
    }
}
