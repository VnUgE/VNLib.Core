/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: IPlugin.cs 
*
* IPlugin.cs is part of VNLib.Plugins which is part of the larger 
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

using System;
using System.Collections.Generic;

namespace VNLib.Plugins
{
    /// <summary>
    /// Allows for applications to define plugin capabilities
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// The name of the plugin to referrence (may be used by the host to interact)
        /// </summary>
        string PluginName { get; }
        /// <summary>
        /// Performs operations to prepare the plugin for use
        /// </summary>
        void Load();
        /// <summary>
        /// Invoked when the plugin is unloaded from the runtime
        /// </summary>
        void Unload();
        /// <summary>
        /// Returns all endpoints within the plugin to load into the current root
        /// </summary>
        /// <returns>An enumeration of endpoints to load</returns>
        /// <remarks>
        /// Lifecycle: Results returned from this method should be consistant (although its only
        /// likely to be called once) anytime after the <see cref="Load"/> method, and undefined
        /// after the <see cref="Unload"/> method is called.
        /// </remarks>
        IEnumerable<IEndpoint> GetEndpoints();
    }
}