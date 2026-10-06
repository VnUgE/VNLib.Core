/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginSharedMemoryHostReservation.cs
*
* PluginSharedMemoryHostReservation.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger
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
    /// Reserves a shared memory region available for plugins to consume at startup. Equivalent to 
    /// using <see cref="SharedRegionAllocAttribute"/> from the plugin side.
    /// </summary>
    /// <param name="RegionName">The name of the shared region.</param>
    /// <param name="Size">The size of the memory region in bytes</param>
    public sealed record PluginSharedMemoryHostReservation(string RegionName, int Size);
}
