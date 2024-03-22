/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: FNV1a.cs 
*
* FNV1a.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VNLib.Hashing.Checksums
{
    /// <summary>
    /// A managed software implementation of the FNV-1a 64-bit non cryptographic hash algorithm
    /// </summary>
    public static class FNV1a
    {
        /* 
        * Constants taken from the spec 
        * https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
        */
        const ulong FNV_PRIME = 0x100000001b3UL;
        const ulong FNV_OFFSET_BASIS = 0xcbf29ce484222325UL;

        /// <summary>
        /// Computes the next 64-bit FNV-1a hash value using the current hash 
        /// value and the next byte of data. 
        /// </summary>
        /// <param name="initalizer">The inital hash to begin the computation with</param>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns>The next value of the checksum representing current and previously computed segments</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ulong Update64(ulong initalizer, ref byte data, nuint length)
        {
            if (Unsafe.IsNullRef(ref data))
            {
                throw new ArgumentNullException(nameof(data));
            }

            ulong digest = initalizer;

            for (nuint i = 0; i < length; i++)
            {
                digest ^= Unsafe.AddByteOffset(ref data, i);
                digest *= FNV_PRIME;
            }

            return digest;
        }

        /// <summary>
        /// Computes the next 64-bit FNV-1a hash value using the current hash
        /// value and the next byte of data.
        /// </summary>
        /// <param name="initalizer">The initial hash to begin the computation with</param>
        /// <param name="data">A span structure pointing to the memory block to compute the digest of</param>
        /// <returns>The next value of the checksum representing current and previously computed segments</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static ulong Update64(ulong initalizer, ReadOnlySpan<byte> data)
        {
            ref byte r0 = ref MemoryMarshal.GetReference(data);
            return Update64(initalizer, ref r0, (nuint)data.Length);
        }

        /// <summary>
        /// Begins computing the FNV-1a 64-bit hash of the input data and returns the 
        /// initial hash value which may be updated if more data is available
        /// </summary>
        /// <param name="data">A managed pointer to the first byte of the sequence to compute</param>
        /// <param name="length">A platform specific integer representing the length of the input data</param>
        /// <returns>The 64bit unsigned integer representing the message sum or digest</returns>
        /// <remarks>
        /// WARNING: This function produces a non-cryptographic hash and should not be used for
        /// security or cryptographic purposes. It is intended for fast data integrity checks
        /// </remarks>
        public static ulong Compute64(ref byte data, nuint length) => Update64(FNV_OFFSET_BASIS, ref data, length);

        /// <summary>
        /// Computes the next 64-bit FNV-1a hash value using the current hash 
        /// value and the next byte of data. 
        /// </summary>
        /// <param name="data">A span structure pointing to the memory block to compute the digest of</param>
        /// <returns>The 64bit unsigned integer representng the message sum or digest</returns>
        /// <remarks>
        /// WARNING: This function produces a non-cryptographic hash and should not be used for
        /// security or cryptographic purposes. It is intended for fast data integrity checks
        /// </remarks>
        public static ulong Compute64(ReadOnlySpan<byte> data)
        {
            ref byte r0 = ref MemoryMarshal.GetReference(data);
            return Compute64(ref r0, (nuint)data.Length);
        }
    }
}
