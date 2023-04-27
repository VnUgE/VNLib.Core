/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IPluginLoadConfiguration.cs 
*
* IPluginLoadConfiguration.cs is part of VNLib.Plugins.Essentials.ServiceStack 
* which is part of the larger VNLib collection of libraries and utilities.
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
using System.Text.Json;

using VNLib.Utils.Logging;
using VNLib.Plugins.Runtime;

namespace VNLib.Plugins.Essentials.ServiceStack
{

    /// <summary>
    /// Plugin loading configuration variables
    /// </summary>
    public interface IPluginLoadConfiguration
    {
        /// <summary>
        /// The directory containing the dynamic plugin assemblies to load
        /// </summary>
        string PluginDir { get; }

        /// <summary>
        /// The optional host configuration file to merge with plugin config 
        /// to pass to the loading plugin.
        /// </summary>
        JsonElement? HostConfig { get; }

        /// <summary>
        /// Passed to the underlying <see cref="RuntimePluginLoader"/>
        /// holding plugins
        /// </summary>
        ILogProvider? PluginErrorLog { get; }

        /// <summary>
        /// A factory instance the provides new <see cref="IPluginAssemblyLoader"/> instances
        /// on demand from its plugin assembly path
        /// </summary>
        public IPluginAssemblyLoaderFactory AssemblyLoaderFactory { get; }
    }
}
