/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IStringSerializeable.cs 
*
* IStringSerializeable.cs is part of VNLib.Utils which is part of the larger 
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
    /// A interface that provides indempodent abstractions for compiling an instance
    /// to its representitive string.
    /// </summary>
    public interface IStringSerializeable
    {
        /// <summary>
        /// Compiles the current instance into its safe string representation
        /// </summary>
        /// <returns>A string of the desired representation of the current instance</returns>
        string Compile();
        /// <summary>
        /// Compiles the current instance into its safe string representation, and writes its 
        /// contents to the specified buffer writer
        /// </summary>
        /// <param name="writer">The ouput writer to write the serialized representation to</param>
        /// <exception cref="OutOfMemoryException"></exception>
        void Compile(ref ForwardOnlyWriter<char> writer);
        /// <summary>
        /// Compiles the current instance into its safe string representation, and writes its 
        /// contents to the specified buffer writer
        /// </summary>
        /// <param name="buffer">The buffer to write the serialized representation to</param>
        /// <returns>The number of characters written to the buffer</returns>
        ERRNO Compile(in Span<char> buffer);
    }
}
