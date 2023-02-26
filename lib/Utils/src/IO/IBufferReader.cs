/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IBufferReader.cs 
*
* IBufferReader.cs is part of VNLib.Utils which is part of the larger 
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


namespace VNLib.Utils.IO
{
    /// <summary>
    /// A simple interface that provides the opposite of a buffer writer,
    /// that is, allow reading segments from the internal buffer
    /// and advancing the read position
    /// </summary>
    /// <typeparam name="T">The buffer data type</typeparam>
    public interface IBufferReader<T>
    {
        /// <summary>
        /// Advances the reader by the number of elements read
        /// </summary>
        /// <param name="count">The number of elements read</param>
        void Advance(int count);

        /// <summary>
        /// Gets the current data segment to read
        /// </summary>
        /// <returns>The current data segment to read</returns>
        ReadOnlySpan<T> GetWindow();
    }
}
