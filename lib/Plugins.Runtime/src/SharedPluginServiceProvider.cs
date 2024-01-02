/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: SharedPluginServiceProvider.cs 
*
* SharedPluginServiceProvider.cs is part of VNLib.Plugins.Runtime which 
* is part of the larger VNLib collection of libraries and utilities.
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
using System.ComponentModel.Design;

using VNLib.Utils;
using VNLib.Plugins.Runtime.Services;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Represents a single shared pool for a collection of plugins to 
    /// export services to.
    /// </summary>
    public sealed class SharedPluginServiceProvider : 
        VnDisposeable, 
        IServiceProvider,
        IPluginEventListener
    {
        private readonly ServiceContainer _serviceContainer = new();
        private readonly object _syncRoot = new();

        ///<inheritdoc/>
        public object? GetService(Type serviceType) => _serviceContainer.GetService(serviceType);

        /// <summary>
        ///  Gets the service object of the specified type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>
        /// A service object of type serviceType. -or- null if 
        /// there is no service object of type serviceType.
        /// </returns>
        public T? GetService<T>() where T : class => GetService(typeof(T)) as T;

        void IPluginEventListener.OnPluginLoaded(PluginController controller, object? state)
        {
            //Add services
            AddOrRemoveServices(controller, true);
        }

        void IPluginEventListener.OnPluginUnloaded(PluginController controller, object? state)
        {
            //Remove services
            AddOrRemoveServices(controller, false);
        }

        private void AddOrRemoveServices(PluginController controller, bool add)
        {
            /*
             * Depending on when services are loaded/unloaded, this instances
             * may be disposeed so avoid raising an exception for a condition
             * that doenst matter. If disposed, we dont need to clean anything up
             */
            if (Disposed)
            {
                return;
            }

            //Get all exported services
            PluginServiceExport[] exports = controller.GetExportedServices();

            //We need to hold a lock to synchronize access to the service container
            lock (_syncRoot)
            {
                //if add flag is set, add the serivces, otherwise remove them
                if (add)
                {
                    Array.ForEach(exports, e => _serviceContainer.AddService(e.ServiceType, e.Service));
                }
                else
                {
                    Array.ForEach(exports, e => _serviceContainer.RemoveService(e.ServiceType));
                }
            }

            //cleanup any disposable services when removing
            if (!add)
            {
                foreach(PluginServiceExport export in exports)
                {
                    if(export.Service is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        ///<inheritdoc/>
        protected override void Free() => _serviceContainer.Dispose();
    }
}
