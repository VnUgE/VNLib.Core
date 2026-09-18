/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Ipc
* File: IPluginMemoryRegionAccessor.cs
*
* IPluginMemoryRegionAccessor.cs is part of VNLib.Plugins.Ipc which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Ipc is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Ipc is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Threading.Tasks;

namespace VNLib.Plugins.Ipc.SharedMemory
{
    /// <summary>
    /// Provides gated access to <see cref="IPluginMemoryRegion"/> when a request for access
    /// to an existing region is made.
    /// <para>
    /// Once a call to IsValid() returns true, the region can be safely acquired via GetRegion() 
    /// and should remain valid until the Unload() function on the plugin is called by the host. 
    /// </para>
    /// </summary>
    public interface IPluginMemoryRegionAccessor
    {
        /// <summary>
        /// The name of the shared memory region
        /// </summary>
        string RegionName { get; }

        /// <summary>
        /// Gets a value that determines if the region is ready for use. 
        /// </summary>
        /// <returns>A value that indicates if the shared memory region is ready</returns>
        bool IsValid();

        /// <summary>
        /// Acquires the <see cref="IPluginMemoryRegion"/> for the shared region
        /// if valid. Use <see cref="IsValid"/> to determine if it's safe to call
        /// </summary>
        /// <returns>The <see cref="IPluginMemoryRegion"/></returns>
        /// <exception cref="InvalidOperationException">Thrown when the region is not valid (see <see cref="IsValid"/>).</exception>
        IPluginMemoryRegion GetRegion();

        /// <summary>
        /// Gets a task that waits for the region to become available, allowing async coordination
        /// during loading events.
        /// </summary>
        /// <returns>The <see cref="IPluginMemoryRegion"/> shared memory region</returns>
        /// <remarks>
        /// If the owner plugin never loads, the task will remain pending until the registry is
        /// disposed, at which point the task is cancelled and awaiting callers will observe a
        /// <see cref="OperationCanceledException"/>.
        /// </remarks>
        Task<IPluginMemoryRegion> WaitAsync();
    }
}
