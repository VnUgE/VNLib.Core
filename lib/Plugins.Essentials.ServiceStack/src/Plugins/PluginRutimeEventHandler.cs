/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginRutimeEventHandler.cs 
*
* PluginRutimeEventHandler.cs is part of VNLib.Plugins.Essentials.ServiceStack which 
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


using System.Linq;

using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins
{
    internal sealed class PluginRutimeEventHandler(ServiceDomain Domain) : IPluginEventListener
    {
        ///<inheritdoc/>
        void IPluginEventListener.OnPluginLoaded(PluginController controller, object? state) => OnPluginLoaded((state as IManagedPlugin)!);

        ///<inheritdoc/>
        void IPluginEventListener.OnPluginUnloaded(PluginController controller, object? state) => OnPluginUnloaded((state as IManagedPlugin)!);

        /// <summary>
        /// Called when a plugin has been successfully loaded and 
        /// should be put into service
        /// </summary>
        /// <param name="plugin">The plugin that was loaded</param>
        internal void OnPluginLoaded(IManagedPlugin plugin)
        {
            //Run onload method before invoking other handlers
            plugin.OnPluginLoaded();

            //Get event listeners at event time because deps may be modified by the domain
            ServiceGroup[] deps = Domain.ServiceGroups.Select(static d => d).ToArray();

            //run onload method
            deps.TryForeach(d => d.OnPluginLoaded(plugin));
        }

        /// <summary>
        /// Called when a plugin is about to be unloaded and should 
        /// be removed from service.
        /// </summary>
        /// <param name="plugin">The plugin instance to unload</param>
        internal void OnPluginUnloaded(IManagedPlugin plugin)
        {
            try
            {
                //Get event listeners at event time because deps may be modified by the domain
                ServiceGroup[] deps = Domain.ServiceGroups.Select(static d => d).ToArray();

                //Run unloaded method
                deps.TryForeach(d => d.OnPluginUnloaded(plugin));
            }
            finally
            {
                //always unload the plugin wrapper
                plugin.OnPluginUnloaded();
            }
        }
    }
}
