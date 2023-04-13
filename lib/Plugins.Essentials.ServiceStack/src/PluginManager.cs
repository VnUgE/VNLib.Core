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
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// A sealed type that manages the plugin interaction layer. Manages the lifetime of plugin
    /// instances, exposes controls, and relays stateful plugin events.
    /// </summary>
    internal sealed class PluginManager : VnDisposeable, IPluginManager, IPluginEventListener
    {
        private const string PLUGIN_FILE_EXTENSION = ".dll";

        private readonly List<ManagedPlugin> _plugins;
        private readonly IReadOnlyCollection<ServiceGroup> _dependents;
      

        private IEnumerable<LivePlugin> _livePlugins => _plugins.SelectMany(static p => p.Controller.Plugins);

        /// <summary>
        /// The collection of internal controllers
        /// </summary>
        public IEnumerable<IManagedPlugin> Plugins => _plugins;

        public PluginManager(IReadOnlyCollection<ServiceGroup> dependents)
        {
            _plugins = new();
            _dependents = dependents;
        }

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public Task LoadPluginsAsync(PluginLoadConfiguration config, ILogProvider appLog)
        {
            Check();            

            //Load all virtual file assemblies withing the plugin folder
            DirectoryInfo dir = new(config.PluginDir);

            if (!dir.Exists)
            {
                appLog.Warn("Plugin directory {dir} does not exist. No plugins were loaded", config.PluginDir);
                return Task.CompletedTask;
            }

            appLog.Information("Loading plugins. Hot-reload: {en}", config.HotReload);

            //Enumerate all dll files within this dir
            IEnumerable<DirectoryInfo> dirs = dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);

            //Select only dirs with a dll that is named after the directory name
            IEnumerable<string> pluginPaths = GetPluginPaths(dirs);

            IEnumerable<string> pluginFileNames = pluginPaths.Select(static s => $"{Path.GetFileName(s)}\n");

            appLog.Debug("Found plugin files: \n{files}", string.Concat(pluginFileNames));

            //Initialze plugin managers
            ManagedPlugin[] wrappers = pluginPaths.Select(pw => new ManagedPlugin(pw, config, this)).ToArray();

            //Add to loaded plugins
            _plugins.AddRange(wrappers);

            //Load plugins
            return InitiailzeAndLoadAsync(appLog);
        }

        private static IEnumerable<string> GetPluginPaths(IEnumerable<DirectoryInfo> dirs)
        {
            //Select only dirs with a dll that is named after the directory name
            return dirs.Where(static pdir =>
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
        }

        private async Task InitiailzeAndLoadAsync(ILogProvider debugLog) 
        {
            //Load all async
            Task[] initAll = _plugins.Select(p => InitializePlugin(p, debugLog)).ToArray();

            //Wait for initalization
            await Task.WhenAll(initAll).ConfigureAwait(false);

            //Load stage, load all multithreaded
            Parallel.ForEach(_plugins, p => LoadPlugin(p, debugLog));

            debugLog.Information("Plugin loading completed");
        }

        private async Task InitializePlugin(ManagedPlugin plugin, ILogProvider debugLog)
        {
            void LogAndRemovePlugin(Exception ex)
            {
                debugLog.Error(ex, $"Exception raised during initialzation of {plugin.PluginFileName}. It has been removed from the collection\n{ex}");

                //Remove the plugin from the list while locking it
                lock (_plugins)
                {
                    _plugins.Remove(plugin);
                }

                //Dispose the plugin
                plugin.Dispose();
            }

            try
            {
                //Load wrapper
                await plugin.InitializePluginsAsync().ConfigureAwait(true);
            }
            catch(AggregateException ae) when (ae.InnerException != null) 
            {
                LogAndRemovePlugin(ae.InnerException);
            }
            catch (Exception ex)
            {
                LogAndRemovePlugin(ex);
            }
        }

        private static void LoadPlugin(ManagedPlugin plugin, ILogProvider debugLog)
        {
            Stopwatch sw = new();
            try
            {
                sw.Start();

                //Load wrapper
                plugin.LoadPlugins();

                sw.Stop();

                /*
                 * If the plugin assembly does not expose any plugin types or there is an issue loading the assembly, 
                 * its types my not unify, then we should give the user feedback insead of a silent fail.
                 */
                if (!plugin.Controller.Plugins.Any())
                {
                    debugLog.Warn("No plugin instances were exposed via {ams} assembly. This may be due to an assebmly mismatch", plugin.PluginFileName);
                }
                else
                {
                    debugLog.Verbose("Loaded {pl} in {tm} ms", plugin.PluginFileName, sw.ElapsedMilliseconds);
                }

            }
            catch (Exception ex) 
            {
                debugLog.Error(ex, $"Exception raised during loading {plugin.PluginFileName}. Failed to load plugin \n{ex}");
            }
            finally
            {
                sw.Stop();
            }
        }

        /// <inheritdoc/>
        public bool SendCommandToPlugin(string pluginName, string message, StringComparison nameComparison = StringComparison.Ordinal)
        {
            Check();

            //Find the single plugin by its name
            LivePlugin? pl = _livePlugins.Where(p => pluginName.Equals(p.PluginName, nameComparison)).SingleOrDefault();

            //Send the command
            return pl?.SendConsoleMessage(message) ?? false;
        }

        /// <inheritdoc/>
        public void ForceReloadAllPlugins()
        {
            //Reload all plugin managers
            _plugins.TryForeach(static p => p.ReloadPlugins());
        }

        /// <inheritdoc/>
        public void UnloadPlugins()
        {
            //Unload all plugin controllers
            _plugins.TryForeach(static p => p.UnloadPlugins());

            /*
             * All plugin instances must be destroyed because the 
             * only way they will be loaded is from their files 
             * again, so they must be released
             */
            _plugins.TryForeach(static p => p.Dispose());
            _plugins.Clear();
        }

        protected override void Free()
        {
            //Cleanup on dispose if unload failed
            _plugins.TryForeach(static p => p.Dispose());
            _plugins.Clear();
        }

        void IPluginEventListener.OnPluginLoaded(PluginController controller, object? state)
        {
            //Get event listeners at event time because deps may be modified by the domain
            ServiceGroup[] deps = _dependents.Select(static d => d).ToArray();

            //run onload method
            deps.TryForeach(d => d.OnPluginLoaded((IManagedPlugin)state!));
        }

        void IPluginEventListener.OnPluginUnloaded(PluginController controller, object? state)
        {
            //Get event listeners at event time because deps may be modified by the domain
            ServiceGroup[] deps = _dependents.Select(static d => d).ToArray();

            //Run unloaded method
            deps.TryForeach(d => d.OnPluginUnloaded((IManagedPlugin)state!));
        }
    }
}
