/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpMemoryPool.cs 
*
* IHttpMemoryPool.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Buffers;

using VNLib.Utils.Memory;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents a single memory pool for the server that allocates buffers per http context.
    /// on new connections and frees them when the connection is closed.
    /// </summary>
    public interface IHttpMemoryPool
    {
        /// <summary>
        /// Allocates a buffer for a new http context connection attachment.
        /// </summary>
        /// <param name="bufferSize">The minium size of the buffer required</param>
        /// <returns>A handle to the allocated buffer</returns>
        IMemoryOwner<byte> AllocateBufferForContext(int bufferSize);

        /// <summary>
        /// Allocates arbitrary form data related memory handles that are not tied to a specific http context.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="initialSize">The initial size of the buffer to allocate, which may be expanded as needed</param>
        /// <returns>The allocated block of memory</returns>
        MemoryHandle<T> AllocFormDataBuffer<T>(int initialSize) where T: unmanaged;
    }
}
