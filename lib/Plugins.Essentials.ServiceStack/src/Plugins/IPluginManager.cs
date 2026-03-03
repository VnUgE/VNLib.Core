/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IPluginManager.cs 
*
* IPluginManager.cs is part of VNLib.Plugins.Essentials.ServiceStack which 
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

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins
{
    /// <summary>
    /// Represents a live plugin controller that manages the application
    /// lifecycle of plugins.
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>
        /// Loads plugins into the current service manager. The log provider
        /// passed to the constructor will be used for plugin diagnostics.
        /// </summary>
        /// <param name="concurrent">A value that indicates if plugins should be loaded in parallel or serially</param>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        void LoadPlugins(bool concurrent);

        /// <summary>
        /// Manually reloads all plugins loaded to the current service manager
        /// </summary>
        /// <param name="concurrent">A value that indicates if plugins should be loaded in parallel or serially</param>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        void ReloadPlugins(bool concurrent);

        /// <summary>
        /// Unloads all loaded plugins and calls their event handlers
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        void UnloadPlugins();
    }
}
