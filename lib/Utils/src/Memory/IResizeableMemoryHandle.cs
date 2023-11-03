/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IResizeableMemoryHandle.cs 
*
* IResizeableMemoryHandle.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory
{

    /// <summary>
    /// Represents a memory handle that can be resized in place.
    /// </summary>
    /// <typeparam name="T">The data type</typeparam>
    public interface IResizeableMemoryHandle<T> : IMemoryHandle<T>
    {
        /// <summary>
        /// Gets a value indicating whether the handle supports resizing in place
        /// </summary>
        bool CanRealloc { get; }

        /// <summary>
        /// Resizes a memory handle to a new number of elements. 
        /// </summary>
        /// <remarks>
        /// Even if a handle is resizable resizing may not be supported for all types of handles. <br/>
        /// Be careful not to resize handles that are pinned or have raw pointers/references floating.
        /// </remarks>
        /// <param name="elements">The new number of elements to resize the handle to</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        void Resize(nuint elements);
    }
}