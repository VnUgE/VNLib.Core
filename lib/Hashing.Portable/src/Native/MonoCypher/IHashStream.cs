/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: IHashStream.cs 
*
* IHashStream.cs is part of VNLib.Hashing.Portable which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Hashing.Portable is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Hashing.Portable is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Hashing.Portable. If not, see http://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Hashing.Native.MonoCypher
{
    /// <summary>
    /// A base interface for a streaming (incremental) hash
    /// function
    /// </summary>
    public interface IHashStream : IDisposable
    {
        /// <summary>
        /// The configured hash size of this stream
        /// </summary>
        byte HashSize { get; }

        /// <summary>
        /// Updates the hash of this stream with the specified message
        /// </summary>
        /// <param name="mRef">A reference to the first byte of the sequence</param>
        /// <param name="mSize">The size of the sequence</param>
        void Update(ref readonly byte mRef, uint mSize);

        /// <summary>
        /// Flushes the hash of this stream to the specified buffer
        /// </summary>
        /// <param name="hashOut">A reference to the first byte in the output sequence</param>
        /// <param name="hashSize">The size of the output sequence</param>
        void Flush(ref byte hashOut, byte hashSize);
    }
}