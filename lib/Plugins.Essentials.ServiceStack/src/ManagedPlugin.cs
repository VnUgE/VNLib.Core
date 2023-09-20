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

using System.Linq;
using System.Threading;
using System.Reflection;
using System.ComponentModel.Design;

using VNLib.Utils;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Attributes;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    internal sealed class ManagedPlugin : VnDisposeable, IManagedPlugin
    {
        internal RuntimePluginLoader Plugin { get; }

        ///<inheritdoc/>
        public string PluginPath => Plugin.Config.AssemblyFile;

        private UnloadableServiceContainer? _services;

        public ManagedPlugin(RuntimePluginLoader loader) => Plugin = loader;

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
                return Plugin.Controller;
            }
        }

        /*
        * Automatically called after the plugin has successfully loaded
        * by event handlers below
        */

        internal void OnPluginLoaded()
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

        internal void OnPluginUnloaded()
        {
            //Cleanup services no longer in use. Plugin is still valid until this method returns
            using (_services)
            {
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

            //Dispose loader
            Plugin.Dispose();
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
