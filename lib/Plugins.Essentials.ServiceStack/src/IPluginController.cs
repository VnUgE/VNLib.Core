/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IHttpPluginManager.cs 
*
* IHttpPluginManager.cs is part of VNLib.Plugins.Essentials.ServiceStack which 
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
* along with this program. If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Represents a live plugin controller that manages all
    /// plugins loaded in a <see cref="ServiceDomain"/>
    /// </summary>
    public interface IHttpPluginManager
    {
        /// <summary>
        /// The the plugins managed by this <see cref="IHttpPluginManager"/>
        /// </summary>
        public IEnumerable<IManagedPlugin> Plugins { get; }

        /// <summary>
        /// Sends a message to a plugin identified by it's name.
        /// </summary>
        /// <param name="pluginName">The name of the plugin to pass the message to</param>
        /// <param name="message">The message to pass to the plugin</param>
        /// <param name="nameComparison">The name string comparison type</param>
        /// <returns>True if the plugin was found and it has a message handler loaded</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        bool SendCommandToPlugin(string pluginName, string message, StringComparison nameComparison = StringComparison.Ordinal);

        /// <summary>
        /// Manually reloads all plugins loaded to the current service manager
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        void ForceReloadAllPlugins();

        /// <summary>
        /// Unloads all loaded plugins and calls thier event handlers
        /// </summary>
        void UnloadPlugins();
    }
}
