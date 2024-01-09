/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: HashAlg.cs 
*
* HashAlg.cs is part of VNLib.Hashing.Portable which is part of the larger 
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

using VNLib.Hashing.Native.MonoCypher;

namespace VNLib.Hashing
{
    /// <summary>
    /// Defines a hashing algorithm to use when computing a hash.
    /// </summary>
    public enum HashAlg
    {
        /// <summary>
        /// Unused type, will cause a computation method to raise an argument exception when used.
        /// </summary>
        None,
        /// <summary>
        /// Defines the SHA-512 hashing algorithm
        /// </summary>
        SHA512 = 64,
        /// <summary>
        /// Defines the SHA-384 hashing algorithm
        /// </summary>
        SHA384 = 48,
        /// <summary>
        /// Defines the SHA-256 hashing algorithm
        /// </summary>
        SHA256 = 32,
        /// <summary>
        /// Defines the SHA-1 hashing algorithm
        /// WARNING: This hashing method is considered insecure and cannot be corrected.
        /// </summary>
        SHA1 = 20,
        /// <summary>
        /// Defines the MD5 hashing algorithm
        /// WARNING: This hashing method is considered insecure and cannot be corrected.
        /// </summary>
        MD5 = 16,

#pragma warning disable CA1707 // Identifiers should not contain underscores

        /// <summary>
        /// Defines the SHA3-512 hashing algorithm. NOTE: May not be supported on all platforms.
        /// Inspect the value of <see cref="ManagedHash.SupportsSha3"/>
        /// </summary>
        SHA3_512 = 364,
        /// <summary>
        /// Defines the SHA3-384 hashing algorithm. NOTE: May not be supported on all platforms.
        /// Inspect the value of <see cref="ManagedHash.SupportsSha3"/>
        /// </summary>
        SHA3_384 = 348,
        /// <summary>
        /// Defines the SHA3-256 hashing algorithm. NOTE: May not be supported on all platforms.
        /// Inspect the value of <see cref="ManagedHash.SupportsSha3"/>
        /// </summary>
        SHA3_256 = 332,

#pragma warning restore CA1707 // Identifiers should not contain underscores

        /*
         * The blake2 value is negative because the hash size is variable and the enum value 
         * and cannot be used to determine the hash buffer size.
         */
        /// <summary>
        /// Defines the BLAKE2B hashing algorithm
        /// NOTE: This hashing method may not be supported on all platforms, you should check for support before using it.
        /// Inspect the value of <see cref="ManagedHash.SupportsBlake2b"/>
        /// </summary>
        BlAKE2B = -MCBlake2Module.MaxHashSize,
    }
}
