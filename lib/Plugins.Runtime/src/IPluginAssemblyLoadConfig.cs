/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IPluginAssemblyLoadConfig.cs 
*
* IPluginAssemblyLoadConfig.cs is part of VNLib.Plugins.Runtime which is part 
* of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Represents runtime plugin load configuration
    /// instance.
    /// </summary>
    public interface IPluginAssemblyLoadConfig
    {
        /// <summary>
        /// Gets a value that indicates whether the plugin assembly can be unloaded from its load context.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the assembly supports unloading; otherwise, <see langword="false"/>.
        /// </value>
        bool Unloadable { get; }

        /// <summary>
        /// Gets the full file-system path of the assembly file to load.
        /// </summary>
        /// <value>The absolute path to the plugin assembly file.</value>
        string AssemblyFile { get; }

        /// <summary>
        /// Gets a value that indicates whether the plugin assembly should be monitored for hot-reload.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the assembly file should be watched for changes; otherwise, <see langword="false"/>.
        /// </value>
        bool WatchForReload { get; }

        /// <summary>
        /// Gets the delay between a detected assembly file change and the triggered plugin reload.
        /// </summary>
        /// <value>The reload delay as a <see cref="TimeSpan"/>.</value>
        TimeSpan ReloadDelay { get; }

        /// <summary>
        /// Gets an <see cref="IAssemblyLoader"/> appropriate for this configuration instance.
        /// Implementations may return a new instance on each call, so callers are advised
        /// to store and reuse the returned loader for the lifetime of the plugin.
        /// </summary>
        /// <returns>An <see cref="IAssemblyLoader"/> configured for this plugin instance</returns>
        IAssemblyLoader GetLoader();
    }
}
