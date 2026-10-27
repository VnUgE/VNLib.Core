/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginSharedMemoryConfig.cs
*
* PluginSharedMemoryConfig.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

using System.Collections.Generic;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc
{
    /// <summary>
    /// User properties to configure the <see cref="PluginSharedMemoryProvider"/>
    /// </summary>
    public sealed record PluginSharedMemoryConfig
    {
        /// <summary>
        ///  The memory manager used to allocate and free IPC memory regions.
        /// </summary>
        public required IPluginSharedMemoryManager Allocator { get; init; }

        /// <summary>
        /// The maximum size in bytes allowed for a shared IPC region. Must be 
        /// greater than or equal to <see cref="MinRegionSize"/>.
        /// </summary>
        public required int MaxRegionSize { get; init; }

        /// <summary>
        /// The minimum size in bytes allowed for a new region. Must be greater
        /// than zero and less than or equal to <see cref="MaxRegionSize"/>.
        /// </summary>
        public required int MinRegionSize { get; init; }

        /// <summary>
        /// Defines a list of host reservations for shared regions. Pre-allocates 
        /// these blocks at startup, and frees them when the system exits.
        /// </summary>
        public IEnumerable<PluginSharedMemoryHostReservation>? HostReservations { get; init; }
    }
}
