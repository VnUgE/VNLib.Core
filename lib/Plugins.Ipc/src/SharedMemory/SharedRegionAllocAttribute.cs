/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Ipc
* File: SharedRegionAllocAttribute.cs
*
* SharedRegionAllocAttribute.cs is part of VNLib.Plugins.Ipc which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Ipc is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Ipc is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Ipc. If not, see https://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Plugins.Ipc.SharedMemory
{
    /// <summary>
    /// Declares an <see cref="IPluginMemoryRegion" /> property that the runtime allocates and assigns
    /// before IPlugin.Load() is called.
    /// <para>
    /// Apply this attribute to a plugin property whose type is assignable from
    /// <see cref="IPluginMemoryRegion" /> to request that the host allocate a new named shared
    /// memory region and inject it during pre-load initialization.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Shared region handles are guaranteed to be valid between Load() and Unload() lifecycle hooks of a 
    /// plugin instance and are automatically released when all plugins referencing the same region are unloaded. 
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class SharedRegionAllocAttribute : Attribute
    {
        /// <summary>
        /// Gets the unique shared memory region name to allocate.
        /// </summary>
        /// <value>
        /// The unique shared memory region name.
        /// </value>
        public string Name { get; }

        /// <summary>
        /// Gets the size of the shared memory region to allocate, in bytes.
        /// </summary>
        /// <value>
        /// The shared memory region size, in bytes.
        /// </value>
        public int Size { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedRegionAllocAttribute" /> class
        /// with the specified region name and size.
        /// </summary>
        /// <param name="name">The unique region name to allocate.</param>
        /// <param name="size">The region size, in bytes.</param>
        public SharedRegionAllocAttribute(string name, int size)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            Name = name;
            Size = size;
        }
    }
}
