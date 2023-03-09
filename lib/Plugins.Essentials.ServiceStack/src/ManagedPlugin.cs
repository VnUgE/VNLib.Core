/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: ManagedPlugin.cs 
*
* ManagedPlugin.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using System.ComponentModel.Design;

using VNLib.Utils;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Attributes;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    internal sealed class ManagedPlugin : VnDisposeable, IPluginEventListener, IManagedPlugin
    {
        private readonly IPluginEventListener _serviceDomainListener;
        private readonly RuntimePluginLoader _plugin;

        private UnloadableServiceContainer? _services;

        public ManagedPlugin(string pluginPath, PluginLoadConfiguration config, IPluginEventListener listener)
        {
            PluginPath = pluginPath;

            //configure the loader
            _plugin = new(pluginPath, config.HostConfig, config.PluginErrorLog, config.HotReload, config.HotReload);

            //Register listener before loading occurs
            _plugin.Controller.Register(this, this);

            //Store listener to raise events
            _serviceDomainListener = listener;
        }

        ///<inheritdoc/>
        public string PluginPath { get; }

        ///<inheritdoc/>
        public IUnloadableServiceProvider Services
        {
            get
            {
                Check();
                return _services!;
            }
        }

        ///<inheritdoc/>
        public PluginController Controller
        {
            get
            {
                Check();
                return _plugin.Controller;
            }
        }

        internal string PluginFileName => Path.GetFileName(PluginPath);

        internal Task InitializePluginsAsync()
        {
            Check();
            return _plugin.InitializeController();
        }

        internal void LoadPlugins()
        {
            Check();
            _plugin.LoadPlugins();
        }

        /*
         * Automatically called after the plugin has successfully loaded
         * by event handlers below
         */       
        private void ConfigureServices()
        {
            //If the service container is defined, dispose
            _services?.Dispose();

            //Init new service container
            _services = new();

            //Get types from plugin
            foreach (LivePlugin plugin in _plugin.Controller.Plugins)
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

        internal void ReloadPlugins()
        {
            Check();
            _plugin.ReloadPlugins();
        }

        internal void UnloadPlugins()
        {
            Check();

            //unload plugins
            _plugin.UnloadAll();

            //Services will be cleaned up by the unload event
        }

        void IPluginEventListener.OnPluginLoaded(PluginController controller, object? state)
        {
            //Initialize services after load, before passing event
            ConfigureServices();

            //Propagate event
            _serviceDomainListener.OnPluginLoaded(controller, state);
        }

        void IPluginEventListener.OnPluginUnloaded(PluginController controller, object? state)
        {
            //Cleanup services no longer in use. Plugin is still valid until this method returns
            using (_services)
            {
                //Propagate event
                _serviceDomainListener.OnPluginUnloaded(controller, state);

                //signal service cancel before disposing
                _services?.SignalUnload();
            }
            //Remove ref to services
            _services = null;
        }

        protected override void Free()
        {
            //Dispose services
            _services?.Dispose();
            //Unregister the listener to cleanup resources
            _plugin.Controller.Unregister(this);
            //Dispose loader
            _plugin.Dispose();
        }


        private sealed class UnloadableServiceContainer : ServiceContainer, IUnloadableServiceProvider
        {
            private readonly CancellationTokenSource _cts;

            public UnloadableServiceContainer() : base()
            {
                _cts = new();
            }

            ///<inheritdoc/>
            CancellationToken IUnloadableServiceProvider.UnloadToken => _cts.Token;

            /// <summary>
            /// Signals to listensers that the service container will be unloading
            /// </summary>
            internal void SignalUnload() => _cts.Cancel();

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                _cts.Dispose();
            }
        }
    }
}
