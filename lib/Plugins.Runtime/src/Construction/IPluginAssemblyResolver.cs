/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IPluginAssemblyResolver.cs 
*
* IPluginAssemblyResolver.cs is part of VNLib.Plugins.Runtime which is part of the larger 
* VNLib collection of libraries and utilities.
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

using System.Collections.Generic;

namespace VNLib.Plugins.Runtime.Construction
{
    /// <summary>
    /// Defines a contract for discovering plugin assemblies and providing assembly loaders for them. This allows
    /// for a flexible and extensible plugin system where different loading strategies can be applied based on the
    /// configuration of each plugin assembly.
    /// </summary>
    public interface IPluginAssemblyResolver
    {
        /// <summary>
        /// Discovers plugin assemblies and returns a configuration for each assembly. The configuration
        /// is used to determine how the assembly should be loaded and managed by the plugin stack.
        /// </summary>
        /// <returns>An enumerable of <see cref="IPluginAssemblyLoadConfig"/> instances describing each discovered plugin assembly.</returns>
        IEnumerable<IPluginAssemblyLoadConfig> DiscoverAssemblies();
    }
}
