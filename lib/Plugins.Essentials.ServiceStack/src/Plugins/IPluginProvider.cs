/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IPluginProvider.cs
*
* IPluginProvider.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

using System.Collections.Generic;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins
{
    /// <summary>
    /// Abstraction over plugin stack implementations that can provide plugins
    /// to the <see cref="PluginManager"/> for lifecycle management
    /// </summary>
    public interface IPluginProvider
    {
        /// <summary>
        /// Builds the plugin stack, discovering and preparing all plugins
        /// for initialization
        /// </summary>
        void BuildStack();

        /// <summary>
        /// Gets the collection of plugins that were discovered during stack building
        /// </summary>
        /// <returns>An enumeration of plugins available in this stack</returns>
        IEnumerable<IManualPlugin> GetPlugins();
    }
}
