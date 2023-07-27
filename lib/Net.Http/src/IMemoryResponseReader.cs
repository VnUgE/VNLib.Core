/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IMemoryResponseReader.cs 
*
* IMemoryResponseReader.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// <para>
    /// A forward only memory backed response entity body reader. This interface exists
    /// to provide a memory-backed response body that will be written "directly" to the
    /// response stream. This avoids a buffer allocation and a copy.
    /// </para>
    /// <para>
    /// The entity is only read foward, one time, so it is not seekable.
    /// </para>
    /// <para>
    /// The <see cref="Close"/> method is always called by internal lifecycle hooks
    /// when the entity is no longer needed. <see cref="Close"/> should avoid raising 
    /// excptions.
    /// </para>
    /// </summary>
    public interface IMemoryResponseReader
    {
        /// <summary>
        /// Gets a readonly buffer containing the remaining 
        /// data to be written
        /// </summary>
        /// <returns>A memory segment to send to the client</returns>
        ReadOnlyMemory<byte> GetMemory();
        
        /// <summary>
        /// Advances the buffer by the number of bytes written
        /// </summary>
        /// <param name="written">The number of bytes written</param>
        void Advance(int written);

        /// <summary>
        /// The number of bytes remaining to send
        /// </summary>
        int Remaining { get; }

        /// <summary>
        /// Raised when reading has completed
        /// </summary>
        void Close();
    }
}