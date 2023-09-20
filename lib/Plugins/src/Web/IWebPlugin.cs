/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: IWebPlugin.cs 
*
* IWebPlugin.cs is part of VNLib.Plugins which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins. If not, see http://www.gnu.org/licenses/.
*/

using System.Collections.Generic;

namespace VNLib.Plugins
{
    /// <summary>
    /// Represents a plugin that is expected to perform web application based operations
    /// </summary>
    public interface IWebPlugin : IPlugin
    {
        /// <summary>
        /// Returns all endpoints within the plugin to load into the current root
        /// </summary>
        /// <returns>An enumeration of endpoints to load</returns>
        /// <remarks>
        /// Lifecycle: Results returned from this method should be consistant (although its only
        /// likely to be called once) anytime after the <see cref="IPlugin.Load"/> method, and undefined
        /// after the <see cref="IPlugin.Unload"/> method is called.
        /// </remarks>
        IEnumerable<IEndpoint> GetEndpoints();
    }
}