/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: HeapCreation.cs 
*
* HeapCreation.cs is part of VNLib.Utils which is part of the larger 
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
    /// Internal heap creation flags passed to the heap creation method
    /// on initialization
    /// </summary>
    [Flags]
    public enum HeapCreation : int
    {
        /// <summary>
        /// Default/no flags
        /// </summary>
        None,
        /// <summary>
        /// Specifies that all allocations be zeroed before returning to caller
        /// </summary>
        GlobalZero = 0x01,
        /// <summary>
        /// Specifies that the heap should use internal locking, aka its not thread safe
        /// and needs to be made thread safe
        /// </summary>
        UseSynchronization = 0x02,
        /// <summary>
        /// Specifies that the requested heap will be a shared heap for the process/library
        /// </summary>
        IsSharedHeap = 0x04
    }
}