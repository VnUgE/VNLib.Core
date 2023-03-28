/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: HeapStatistics.cs 
*
* HeapStatistics.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory.Diagnostics
{
    /// <summary>
    /// A structure that represents the current/last captured 
    /// statistics of the monitored heap
    /// </summary>
    public readonly record struct HeapStatistics 
    {
        /// <summary>
        /// The current size (in bytes) of the heap
        /// </summary>
        public readonly ulong AllocatedBytes { get; init; }
        /// <summary>
        /// The largest block size seen
        /// </summary>
        public readonly ulong MaxBlockSize { get; init; }
        /// <summary>
        /// The largest size of the heap, in bytes.
        /// </summary>
        public readonly ulong MaxHeapSize { get; init; }
        /// <summary>
        /// The smallest block size seen allocated
        /// </summary>
        public readonly ulong MinBlockSize { get; init; }
        /// <summary>
        /// The number of allocated handles/blocks
        /// </summary>
        public readonly ulong AllocatedBlocks { get; init; }
    }
}
