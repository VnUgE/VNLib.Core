/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IIpcRegionOwner.cs
*
* IIpcRegionOwner.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify
* it under the terms of the GNU Affero General Public License as
* published by the Free Software Foundation, either version 3 of the
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

using VNLib.Plugins.Ipc.SharedMemory;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc
{
    /// <summary>
    /// Represents the owner of a mapped <see cref="IPluginMemoryRegion"/>
    /// </summary>
    internal interface IIpcRegionOwner
    {
        /// <summary>
        /// The plugin shared memory region
        /// </summary>
        IPluginMemoryRegion Region { get; }
    }
}
