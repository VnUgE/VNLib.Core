/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: IJwtSignatureVerifier.cs 
*
* IJwtSignatureVerifier.cs is part of VNLib.Hashing.Portable which is 
* part of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Hashing.IdentityUtility
{
    /// <summary>
    /// Represents an object that can verify a message hash against its signature to 
    /// confirm the message is authentic
    /// </summary>
    public interface IJwtSignatureVerifier
    {
        /// <summary>
        /// Verifes that the message digest/hash matches the provided signature
        /// </summary>
        /// <param name="messageHash">The message digest to verify</param>
        /// <param name="signature">The signature to confrim matches</param>
        /// <returns>True if the signature matches the computed signature, false otherwise</returns>
        bool Verify(ReadOnlySpan<byte> messageHash, ReadOnlySpan<byte> signature);
    }
}
