/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: IJwtSignatureProvider.cs 
*
* IJwtSignatureProvider.cs is part of VNLib.Hashing.Portable which is part 
* of the larger VNLib collection of libraries and utilities.
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

using VNLib.Utils;

namespace VNLib.Hashing.IdentityUtility
{
    /// <summary>
    /// Represents an objec that can compute signatures from a messages digest
    /// and write the results to a buffer.
    /// </summary>
    public interface IJwtSignatureProvider
    {
        /// <summary>
        /// Gets the size (in bytes) of the buffer required to store the signature output
        /// </summary>
        int RequiredBufferSize { get; }

        /// <summary>
        /// Computes the signature from the message digest, and stores the results in the 
        /// output buffer
        /// </summary>
        /// <param name="hash">The message digest to compute the signature of</param>
        /// <param name="outputBuffer">The buffer to write sigature data to</param>
        /// <returns>The number of bytes written to the output buffer, or 0/fale if the operation failed</returns>
        ERRNO ComputeSignatureFromHash(ReadOnlySpan<byte> hash, Span<byte> outputBuffer);
    }
}
