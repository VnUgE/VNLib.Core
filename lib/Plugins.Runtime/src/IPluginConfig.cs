/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IPluginConfig.cs 
*
* IPluginConfig.cs is part of VNLib.Plugins.Runtime which is part 
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
    /// Represents configuration information for a <see cref="IPluginAssemblyLoader"/>
    /// instance.
    /// </summary>
    public interface IPluginConfig
    {
        /// <summary>
        /// A value that indicates if the instance is unlodable.
        /// </summary>
        bool Unloadable { get; }

        /// <summary>
        /// The full file path to the assembly file to load
        /// </summary>
        string AssemblyFile { get; }

        /// <summary>
        /// A value that indicates if the plugin assembly should be watched for reload
        /// </summary>
        bool WatchForReload { get; }

        /// <summary>
        /// The delay which a watcher should wait to trigger a plugin reload after an assembly file changes
        /// </summary>
        TimeSpan ReloadDelay { get; }
    }
}
