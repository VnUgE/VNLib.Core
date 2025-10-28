/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: IFBMMemoryManager.cs 
*
* IFBMMemoryManager.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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

using System.Diagnostics.CodeAnalysis;

using VNLib.Utils.Memory;

namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// Manages memory blocks required by the FBM server messages
    /// </summary>
    public interface IFBMMemoryManager
    {
        /// <summary>
        /// Initializes a new <see cref="IFBMMemoryHandle"/>
        /// </summary>
        /// <returns>The initialized handle</returns>
        IFBMMemoryHandle InitHandle();

        /// <summary>
        /// Initializes a new <see cref="IFBMSpanOnlyMemoryHandle"/>
        /// </summary>
        /// <returns>The initialized handle</returns>
        IFBMSpanOnlyMemoryHandle InitSpanOnly();

        /// <summary>
        /// Allocates the <see cref="IFBMMemoryHandle"/> internal buffer
        /// for use
        /// </summary>
        /// <param name="state">The memory handle to allocate the buffer for</param>
        /// <param name="size">The size of the buffer required</param>
        void AllocBuffer(IFBMSpanOnlyMemoryHandle state, int size);

        /// <summary>
        /// Frees the <see cref="IFBMSpanOnlyMemoryHandle"/> internal buffer
        /// </summary>
        /// <param name="state">The buffer handle holding the memory to free</param>
        void FreeBuffer(IFBMSpanOnlyMemoryHandle state);

        /// <summary>
        /// Tries to get the internal <see cref="IUnmanagedHeap"/> to allocate internal 
        /// buffers from
        /// </summary>
        /// <param name="heap">The internal heap</param>
        /// <returns>A value that indicates if a backing heap is supported and can be recovered</returns>
        bool TryGetHeap([NotNullWhen(true)]out IUnmanagedHeap? heap);
    }

}
