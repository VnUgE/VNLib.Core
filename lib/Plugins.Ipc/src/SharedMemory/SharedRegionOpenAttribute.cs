/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Ipc
* File: SharedRegionOpenAttribute.cs
*
* SharedRegionOpenAttribute.cs is part of VNLib.Plugins.Ipc which is part of the larger 
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
    /// Declares an <see cref="IPluginMemoryRegionAccessor" /> property that the runtime assigns a
    /// deferred accessor handle before IPlugin.Load() is called.
    /// <para>
    /// Apply this attribute to a plugin property whose type is assignable from
    /// <see cref="IPluginMemoryRegionAccessor" /> to request a deferred accessor that connects to
    /// a shared memory region once the owning plugin has mapped it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The accessor handle is guaranteed to be valid between Load() and Unload() lifecycle hooks of a plugin 
    /// instance and is automatically released when all plugins referencing the same region are unloaded.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class SharedRegionOpenAttribute : Attribute
    {
        /// <summary>
        /// Gets the unique shared memory region name to open.
        /// </summary>
        /// <value>
        /// The unique shared memory region name.
        /// </value>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedRegionOpenAttribute" /> class
        /// with the specified region name.
        /// </summary>
        /// <param name="name">The unique region name to open.</param>
        public SharedRegionOpenAttribute(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            Name = name;
        }
    }
}
