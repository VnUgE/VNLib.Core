/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: IPluginServicePool.cs 
*
* IPluginServicePool.cs is part of VNLib.Plugins which is part of the larger 
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

namespace VNLib.Plugins
{
    /// <summary>
    /// Represents a type that exposes services to the loading application
    /// </summary>
    public interface IPluginServicePool
    {
        /// <summary>
        /// Publishes a generic service to the service pool
        /// </summary>
        /// <param name="serviceType">The <see cref="Type"/> to expose to the pool for searching</param>
        /// <param name="service">The service instance to publish</param>
        /// <param name="flags">Optional flags to pass during export</param>
        void ExportService(Type serviceType, object service, ExportFlags flags = ExportFlags.None);
    }
}