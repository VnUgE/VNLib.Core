/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Ipc
* File: IPluginMemoryRegion.cs
*
* IPluginMemoryRegion.cs is part of VNLib.Plugins.Ipc which is part of the larger 
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
using System.Buffers;

namespace VNLib.Plugins.Ipc.SharedMemory
{
    /// <summary>
    /// Represents a view over a shared plugin memory region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations expose the user-data portion of a native shared-memory region as raw bytes.
    /// </para>
    /// <para>
    /// <see cref="IPinnable.Pin(int)"/> may be used to obtain a pointer to the underlying native memory
    /// without this interface exposing unsafe members directly. The pointer returned by
    /// <see cref="IPinnable.Pin(int)"/> remains valid for the lifetime of the region and
    /// <see cref="IPinnable.Unpin"/> is a no-op that may be safely called or ignored.
    /// </para>
    /// </remarks>
    public interface IPluginMemoryRegion : IPinnable
    {
        /// <summary>
        /// A shared object that can be used by the .NET runtime to synchronize threads using the Monitor 
        /// class or lock() keyword across the entire region. Both owners and accessors share the same 
        /// lock instance.
        /// </summary>
        object SyncRoot { get; }

        /// <summary>
        /// Gets the number of bytes in the user-data portion of the shared region.
        /// </summary>
        /// <value>
        /// The number of addressable user-data bytes, excluding any internal region header.
        /// </value>
        int Length { get; }

        /// <summary>
        /// Returns a writable view over the full user-data region.
        /// </summary>
        /// <returns>
        /// A writable span over the full user-data region
        /// </returns>
        Span<byte> AsSpan();

        /// <summary>
        /// Returns a reference to the byte of memory at the desired byte offset.
        /// </summary>
        /// <param name="offset">The zero-based byte offset from the start of the region.</param>
        /// <returns>
        /// A writable reference to the byte at the desired offset
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="offset"/> is negative or greater than or equal to <see cref="Length"/>.
        /// </exception>
        ref byte GetReference(int offset);
    }
}
