/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.PluginBase
* File: ServiceExport.cs 
*
* ServiceExport.cs is part of VNLib.Plugins.PluginBase which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.PluginBase is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.PluginBase is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.PluginBase. If not, see http://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Plugins
{
    /// <summary>
    /// A service export that will be published to the 
    /// host application after the plugin has been loaded
    /// </summary>
    /// <param name="Service"> The exported service instance </param>
    /// <param name="ServiceType"> The exported service type </param>
    /// <param name="Flags"> The name of the service </param>
    public sealed record ServiceExport(Type ServiceType, object Service, ExportFlags Flags);
}