/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: PluginServiceExport.cs 
*
* PluginServiceExport.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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

using System;

namespace VNLib.Plugins.Runtime.Services
{
    /// <summary>
    /// An immutable wrapper for an exported service by an <see cref="IPlugin"/>
    /// </summary>
    /// <param name="Flags">The export flags</param>
    /// <param name="Service">The exported service instance</param>
    /// <param name="ServiceType">The exported service type</param>
    public readonly record struct PluginServiceExport(Type ServiceType, object Service, ExportFlags Flags);

}
