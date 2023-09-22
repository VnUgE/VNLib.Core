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
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// A sealed type that manages the plugin interaction layer. Manages the lifetime of plugin
    /// instances, exposes controls, and relays stateful plugin events.
    /// </summary>
    internal sealed class PluginManager : VnDisposeable, IHttpPluginManager, IPluginEventListener
    {
        private readonly Dictionary<PluginController, ManagedPlugin> _managedPlugins;
        private readonly ServiceDomain _dependents;
        private readonly IPluginStack _stack;       

        private IEnumerable<LivePlugin> _livePlugins => _managedPlugins.SelectMany(static p => p.Key.Plugins);

        /// <summary>
        /// The collection of internal controllers
        /// </summary>
        public IEnumerable<IManagedPlugin> Plugins => _managedPlugins.Select(static p => p.Value);

        public PluginManager(ServiceDomain dependents, IPluginStack stack)
        {
            _dependents = dependents;
            _stack = stack;
            _managedPlugins = new();
        }

        /// <summary>
        /// Configures the manager to capture and manage plugins within a plugin stack
        /// </summary>
        /// <param name="debugLog"></param>
        public void LoadPlugins(ILogProvider debugLog)
        {
            _ = _stack ?? throw new InvalidOperationException("Plugin stack has not been set.");

            /*
             * Since we own the plugin stack, it is safe to build it here.
             * This method is not public and should not be called more than 
             * once. Otherwise it can cause issues with the plugin stack.
             */
            _stack.BuildStack();

            //Register for plugin events
            _stack.RegsiterListener(this, this);

            //Create plugin wrappers from loaded plugins
            ManagedPlugin[] wrapper = _stack.Plugins.Select(p => new ManagedPlugin(p)).ToArray();

            //Add all wrappers to the managed plugins table
            Array.ForEach(wrapper, w => _managedPlugins.Add(w.Plugin.Controller, w));

            //Init remaining controllers single-threaded because it may mutate the table
            _managedPlugins.Select(p => p.Value).TryForeach(w => InitializePlugin(w.Plugin, debugLog));

            //Load stage, load all multithreaded
            Parallel.ForEach(_managedPlugins.Values, wp => LoadPlugin(wp.Plugin, debugLog));

            debugLog.Information("Plugin loading completed");
        }

        /*
         * Plugins are manually loaded by this manager instead of the stack shortcut extensions
         * because I want to catch individual exceptions.
         * 
         * I do not prefer this method as I would prefer loading is handled by the stack
         * and the host not by this library. 
         * 
         * This will change in the future.
         */
      
        private void InitializePlugin(RuntimePluginLoader plugin, ILogProvider debugLog)
        {
            string fileName = Path.GetFileName(plugin.Config.AssemblyFile);

            try
            {
                //Initialzie plugin wrapper
                plugin.InitializeController();

                /*
                 * If the plugin assembly does not expose any plugin types or there is an issue loading the assembly, 
                 * its types my not unify, then we should give the user feedback insead of a silent fail.
                 */
                if (!plugin.Controller.Plugins.Any())
                {
                    debugLog.Warn("No plugin instances were exposed via {asm} assembly. This may be due to an assebmly mismatch", fileName);
                }
            }
            catch (Exception ex)
            {
                debugLog.Error("Exception raised during initialzation of {asm}. It has been removed from the collection\n{ex}", fileName, ex);

                //Remove the plugin from the table
                _managedPlugins.Remove(plugin.Controller);
            }
        }

        private static void LoadPlugin(RuntimePluginLoader plugin, ILogProvider debugLog)
        {
            string fileName = Path.GetFileName(plugin.Config.AssemblyFile);
           
            Stopwatch sw = new();
            try
            {
                sw.Start();

                //Load wrapper
                plugin.LoadPlugins();

                sw.Stop();

                debugLog.Verbose("Loaded {pl} in {tm} ms", fileName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex) 
            {
                debugLog.Error("Exception raised during loading {asf}. Failed to load plugin \n{ex}", fileName, ex);
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
            Check();

            //Reload all plugins, causing an event cascade
            _stack.ReloadAll();
        }

        /// <inheritdoc/>
        public void UnloadPlugins()
        {
            Check();

            //Unload all plugin controllers
            _stack.UnloadAll();

            /*
             * All plugin instances must be destroyed because the 
             * only way they will be loaded is from their files 
             * again, so they must be released
             */
            Free();
        }

        protected override void Free()
        {
            //Dispose all managed plugins and clear the table
            _managedPlugins.TryForeach(p => p.Value.Dispose());
            _managedPlugins.Clear();

            //Dispose the plugin stack
            _stack.Dispose();
        }

        /*
         * When using a service stack an loading manually, plugins that have errors 
         * will not be captured by this instance. However when using the shortcut
         * extensions, the events will be invoked regaldess if we loaded the plugin
         * here.
         */

        void IPluginEventListener.OnPluginLoaded(PluginController controller, object? state)
        {
            //Make sure the plugin is managed by this manager
            if(!_managedPlugins.TryGetValue(controller, out ManagedPlugin? mp))
            {
                return;
            }

            //Run onload method before invoking other handlers
            mp.OnPluginLoaded();

            //Get event listeners at event time because deps may be modified by the domain
            ServiceGroup[] deps = _dependents.ServiceGroups.Select(static d => d).ToArray();

            //run onload method
            deps.TryForeach(d => d.OnPluginLoaded(mp));
        }

        void IPluginEventListener.OnPluginUnloaded(PluginController controller, object? state)
        {
            //Make sure the plugin is managed by this manager
            if (!_managedPlugins.TryGetValue(controller, out ManagedPlugin? mp))
            {
                return;
            }

            //Run onload method before invoking other handlers
            mp.OnPluginUnloaded();

            //Get event listeners at event time because deps may be modified by the domain
            ServiceGroup[] deps = _dependents.ServiceGroups.Select(static d => d).ToArray();

            //Run unloaded method
            deps.TryForeach(d => d.OnPluginUnloaded(mp));
        }
    }
}
