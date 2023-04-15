/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IPluginAssemblyLoaderFactory.cs 
*
* IPluginAssemblyLoaderFactory.cs is part of VNLib.Plugins.Essentials.ServiceStack 
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

using VNLib.Plugins.Runtime;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Represents a provider that creates a unique <see cref="IPluginAssemblyLoader"/> for the the 
    /// loaded assembly file.
    /// </summary>
    public interface IPluginAssemblyLoaderFactory
    {
        /// <summary>
        /// Gets a new and unique <see cref="IPluginAssemblyLoader"/> instance for a given plugin assembly 
        /// file path
        /// </summary>
        /// <param name="pluginFile">The file path to the requested plugin assembly file</param>
        /// <returns>The new <see cref="IPluginAssemblyLoader"/> to recover the plugin assembly manifest</returns>
        IPluginAssemblyLoader GetLoaderForPluginFile(string pluginFile);
    }
}
