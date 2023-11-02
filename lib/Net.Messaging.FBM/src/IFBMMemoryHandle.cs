/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: IFBMMemoryHandle.cs 
*
* IFBMMemoryHandle.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;


namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// Represents a configurable handle to a memory block
    /// </summary>
    public interface IFBMMemoryHandle : IFBMSpanOnlyMemoryHandle
    {
        /// <summary>
        /// Gets the block as a <see cref="Memory{T}"/>
        /// structure
        /// </summary>
        /// <returns>The memory structure wrapping the underlying memory block</returns>
        Memory<byte> GetMemory();
    }

}
