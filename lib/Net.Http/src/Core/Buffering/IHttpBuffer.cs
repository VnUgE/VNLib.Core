/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpBuffer.cs 
*
* IHttpBuffer.cs is part of VNLib.Net.Http which is part of the larger 
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

using System;

namespace VNLib.Net.Http.Core.Buffering
{
    /// <summary>
    /// Represents a buffer segment use for an http operation, and defines a shared set of 
    /// methods used for capturing safe buffer segments.
    /// </summary>
    internal interface IHttpBuffer
    {
        /// <summary>
        /// Gets the internal buffer as a span of bytes as fast as possible
        /// </summary>
        /// <param name="offset">The number of bytes to offset the start of the segment</param>
        /// <returns>The memory block as a span</returns>
        Span<byte> GetBinSpan(int offset);

        /// <summary>
        /// Gets the internal buffer as a span of bytes as fast as possible 
        /// with a specified offset and size
        /// </summary>
        /// <param name="offset">The number of bytes to offset the start of the segment</param>
        /// <param name="size">The size of the desired segment</param>
        /// <returns></returns>
        Span<byte> GetBinSpan(int offset, int size);

        /// <summary>
        /// Gets the internal buffer as a reference to a byte as fast as possible.
        /// Dangerous because it's giving accessing a reference to the internal
        /// memory buffer directly
        /// </summary>
        /// <param name="offset">The number of bytes to offset the returned reference to</param>
        /// <returns>A reference to the first byte of the desired sequence</returns>
        ref byte DangerousGetBinRef(int offset);
 
        /// <summary>
        /// Gets the internal buffer as a memory block as fast as possible
        /// </summary>
        /// <returns>The memory block</returns>
        Memory<byte> GetMemory();
        
        /// <summary>
        /// The size of the internal buffer
        /// </summary>
        int Size { get; }
    }
}
