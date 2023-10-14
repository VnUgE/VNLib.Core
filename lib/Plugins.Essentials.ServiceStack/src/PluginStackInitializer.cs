/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginStackInitializer.cs 
*
* PluginStackInitializer.cs is part of VNLib.Plugins.Essentials.ServiceStack which 
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
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.Design;

using VNLib.Utils.Logging;
using VNLib.Plugins.Runtime;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Attributes;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    internal sealed record class PluginStackInitializer(IPluginStack Stack, IManualPlugin[] ManualPlugins) : IPluginInitializer
    {
        private readonly LinkedList<IManagedPlugin> _managedPlugins = new();
        private readonly LinkedList<ManualPluginWrapper> _manualPlugins = new();
 
        ///<inheritdoc/>
        public void PrepareStack(IPluginEventListener listener)
        {
            /*
            * Since we own the plugin stack, it is safe to build it here.
            * This method is not public and should not be called more than 
            * once. Otherwise it can cause issues with the plugin stack.
            */
            Stack.BuildStack();

            //Create plugin wrappers from loaded plugins
            ManagedPlugin[] wrapper = Stack.Plugins.Select(static p => new ManagedPlugin(p)).ToArray();

            //Add wrappers to list of managed plugins
            Array.ForEach(wrapper, p => _managedPlugins.AddLast(p));

            //Register for all plugins and pass the plugin instance as the state object
            Array.ForEach(wrapper, p => p.Plugin.Controller.Register(listener, p));

            //Add manual plugins to list of managed plugins
            Array.ForEach(ManualPlugins, p => _manualPlugins.AddLast(new ManualPluginWrapper(p)));
        }

        ///<inheritdoc/>
        public IManagedPlugin[] InitializePluginStack(ILogProvider debugLog)
        {
            //single thread initialziation
            LinkedList<IManagedPlugin> _loadedPlugins = new();

            //Combine all managed plugins and initialize them individually
            IEnumerable<IManagedPlugin> plugins = _managedPlugins.Union(_manualPlugins);

            foreach(IManagedPlugin p in plugins)
            {
                //Try init plugin and add it to the list of loaded plugins
                if (InitializePluginCore(p, debugLog))
                {
                    _loadedPlugins.AddLast(p);
                }
            }

            //Load stage, load only initialized plugins
            Parallel.ForEach(_loadedPlugins, wp => LoadPlugin(wp, debugLog));

            return _loadedPlugins.ToArray();
        }

        ///<inheritdoc/>
        public void UnloadPlugins()
        {
            Stack.UnloadAll();
            _manualPlugins.TryForeach(static mp => mp.Unload());
        }

        ///<inheritdoc/>
        public void ReloadPlugins()
        {
            Stack.ReloadAll();

            //Reload manual plugins
            _manualPlugins.TryForeach(static mp => mp.Unload());
            _manualPlugins.TryForeach(static mp => mp.Load());
        }

        ///<inheritdoc/>
        public void Dispose()
        {
            Stack.Dispose();
            _manualPlugins.TryForeach(static mp => mp.Dispose());
            _manualPlugins.Clear();
        }

        private static bool InitializePluginCore(IManagedPlugin plugin, ILogProvider debugLog)
        {
            try
            {
                if (plugin is ManagedPlugin mp)
                {
                    //Initialzie plugin wrapper
                    mp.Plugin.InitializeController();

                    /*
                     * If the plugin assembly does not expose any plugin types or there is an issue loading the assembly, 
                     * its types my not unify, then we should give the user feedback insead of a silent fail.
                     */
                    if (!mp.Plugin.Controller.Plugins.Any())
                    {
                        debugLog.Warn("No plugin instances were exposed via {asm} assembly. This may be due to an assebmly mismatch", plugin.ToString());
                    }
                }
                else if(plugin is ManualPluginWrapper mpw)
                {
                    //Initialzie plugin wrapper
                    mpw.Plugin.Initialize();
                }
                else
                {
                    Debug.Fail("Missed managed plugin wrapper type");
                }

                return true;
            }
            catch (Exception ex)
            {
                debugLog.Error("Exception raised during initialzation of {asm}. It has been removed from the collection\n{ex}", plugin.ToString(), ex);
            }

            return false;
        }

        private static void LoadPlugin(IManagedPlugin plugin, ILogProvider debugLog)
        {
            Stopwatch sw = new();
            try
            {
                sw.Start();

                //Recover the base class used to load instances 
                if (plugin is ManagedPlugin mp)
                {
                    mp.Plugin.LoadPlugins();
                }
                else if (plugin is ManualPluginWrapper mpw)
                {
                    mpw.Load();
                }
                else
                {
                    Debug.Fail("Missed managed plugin wrapper type");
                }

                sw.Stop();

                debugLog.Verbose("Loaded {pl} in {tm} ms", plugin.ToString(), sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                debugLog.Error("Exception raised during loading {asf}. Failed to load plugin \n{ex}", plugin.ToString(), ex);
            }
            finally
            {
                sw.Stop();
            }
        }


        private sealed record class ManagedPlugin(RuntimePluginLoader Plugin) : IManagedPlugin
        {
            private ServiceContainer? _services;

            ///<inheritdoc/>
            public IServiceContainer Services
            {
                get
                {
                    _ = _services ?? throw new InvalidOperationException("The service container is not currently loaded");
                    return _services!;
                }
            }

            ///<inheritdoc/>
            IEndpoint[] IManagedPlugin.GetEndpoints() => Plugin.Controller.GetOnlyWebPlugins().SelectMany(static pl => pl.Plugin!.GetEndpoints()).ToArray();

            /*
            * Automatically called after the plugin has successfully loaded
            * by event handlers below
            */

            ///<inheritdoc/>
            void IManagedPlugin.OnPluginLoaded()
            {
                //If the service container is defined, dispose
                _services?.Dispose();

                //Init new service container
                _services = new();

                //Get types from plugin
                foreach (LivePlugin plugin in Plugin.Controller.Plugins)
                {
                    /*
                     * Get the exposed configurator method if declared, 
                     * it may not be defined. 
                     */
                    ServiceConfigurator? callback = plugin.PluginType.GetMethods()
                                                .Where(static m => m.GetCustomAttribute<ServiceConfiguratorAttribute>() != null && !m.IsAbstract)
                                                .Select(m => m.CreateDelegate<ServiceConfigurator>(plugin.Plugin))
                                                .FirstOrDefault();

                    //Invoke if defined to expose services
                    callback?.Invoke(_services);
                }
            }

            ///<inheritdoc/>
            void IManagedPlugin.OnPluginUnloaded()
            {
                //Cleanup services no longer in use. Plugin is still valid until this method returns
                _services?.Dispose();

                //Remove ref to services
                _services = null;
            }

            ///<inheritdoc/>
            bool IManagedPlugin.SendCommandToPlugin(string pluginName, string command, StringComparison comp)
            {
                //Get plugin
                LivePlugin? plugin = Plugin.Controller.Plugins.FirstOrDefault(p => p.PluginName.Equals(pluginName, comp));

                //If plugin is null, return false
                if (plugin == null)
                {
                    return false;
                }

                return plugin.SendConsoleMessage(command);
            }

            public override string ToString() => Path.GetFileName(Plugin.Config.AssemblyFile);
        }

        private sealed record class ManualPluginWrapper(IManualPlugin Plugin) : IManagedPlugin, IDisposable
        {
            private ServiceContainer _container = new();

            ///<inheritdoc/>
            public IServiceContainer Services => _container;

            ///<inheritdoc/>
            IEndpoint[] IManagedPlugin.GetEndpoints() => Plugin.GetEndpoints();

            public void Load()
            {
                Plugin.Load();
                Plugin.GetAllExportedServices(Services);
            }

            public void Unload()
            {
                Plugin.Unload();

                //Unload and re-init container
                _container.Dispose();
                _container = new();
            }

            public void Dispose()
            {
                //Dispose container
                _container.Dispose();

                //Dispose plugin
                Plugin.Dispose();
            }

            ///<inheritdoc/>
            bool IManagedPlugin.SendCommandToPlugin(string pluginName, string command, StringComparison comp)
            {

                if (Plugin.Name.Equals(pluginName, comp))
                {
                    Plugin.OnConsoleCommand(command);
                    return true;
                }

                return false;
            }


            /*
             * SHOULD NEVER BE CALLED
             */
            void IManagedPlugin.OnPluginLoaded() => throw new NotImplementedException();
            void IManagedPlugin.OnPluginUnloaded() => throw new NotImplementedException();

            ///<inheritdoc/>
            public override string ToString() => Plugin.Name;
        }

    }
}
