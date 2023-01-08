/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IMemoryHandle.cs 
*
* IMemoryHandle.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Represents a handle for safe access to memory managed/unamanged memory
    /// </summary>
    /// <typeparam name="T">The type this handle represents</typeparam>
    public interface IMemoryHandle<T> : IDisposable, IPinnable
    {
        /// <summary>
        /// The size of the block as an integer
        /// </summary>
        /// <exception cref="OverflowException"></exception>
        int IntLength { get; }

        /// <summary>
        /// The number of elements in the block
        /// </summary>
        ulong Length { get; }

        /// <summary>
        /// Gets the internal block as a span
        /// </summary>
        Span<T> Span { get; }
    }

}
