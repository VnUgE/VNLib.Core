/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IManagedPlugin.cs 
*
* IManagedPlugin.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.ComponentModel.Design;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Represents a plugin managed by a <see cref="IHttpPluginManager"/> that includes dynamically loaded plugins
    /// </summary>
    public interface IManagedPlugin
    {
        /// <summary>
        /// The exposed services the inernal plugin provides
        /// </summary>
        /// <remarks>
        /// WARNING: Services exposed by the plugin will abide by the plugin lifecycle, so consumers 
        /// must listen for plugin load/unload events to respect lifecycles properly.
        /// </remarks>
        IServiceContainer Services { get; }

        /// <summary>
        /// Internal notification that the plugin is loaded
        /// </summary>
        internal void OnPluginLoaded();

        /// <summary>
        /// Internal notification that the plugin is unloaded
        /// </summary>
        internal void OnPluginUnloaded();

        /// <summary>
        /// Sends the specified command to the desired plugin by it's name
        /// </summary>
        /// <param name="pluginName">The name of the plugin to find</param>
        /// <param name="command">The command text to send to the plugin</param>
        /// <param name="comp">The string name comparison type</param>
        /// <returns>True if the command was sent successfully</returns>
        internal bool SendCommandToPlugin(string pluginName, string command, StringComparison comp);
    }
}
