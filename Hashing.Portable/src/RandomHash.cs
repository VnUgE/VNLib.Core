/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: RandomHash.cs 
*
* RandomHash.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.Memory;

namespace VNLib.Hashing
{
    /// <summary>
    /// Produces random cryptographic data in common formats 
    /// </summary>
    public static class RandomHash
    {

        /// <summary>
        /// Generates a cryptographic random number, computes the hash, and encodes the hash as a string.
        /// </summary>
        /// <param name="alg">The hash algorithm to use when computing the hash</param>
        /// <param name="size">Number of random bytes</param>
        /// <param name="encoding"></param>
        /// <returns>String containing hash of the random number</returns>
        public static string GetRandomHash(HashAlg alg, int size = 64, HashEncodingMode encoding = HashEncodingMode.Base64)
        {
            //Get temporary buffer for storing random keys
            using UnsafeMemoryHandle<byte> buffer = Memory.UnsafeAlloc<byte>(size);
            //Fill with random non-zero bytes
            GetRandomBytes(buffer.Span);
            //Compute hash
            return ManagedHash.ComputeHash(buffer.Span, alg, encoding); 
        }

        /// <summary>
        /// Gets the sha512 hash of a new GUID
        /// </summary>
        /// <returns>String containing hash of the GUID</returns>
        /// <exception cref="FormatException"></exception>
        public static string GetGuidHash(HashAlg alg, HashEncodingMode encoding = HashEncodingMode.Base64)
        {
            //Get temp buffer
            Span<byte> buffer = stackalloc byte[16];
            //Get a new GUID and write bytes to 
            if (!Guid.NewGuid().TryWriteBytes(buffer))
            {
                throw new FormatException("Failed to get a guid hash");
            }
            return ManagedHash.ComputeHash(buffer, alg, encoding);
        }
        /// <summary>
        /// Generates a secure random number and seeds a GUID object, then returns the string GUID
        /// </summary>
        /// <returns>Guid string</returns>
        public static Guid GetSecureGuid()
        {
            //Get temp buffer
            Span<byte> buffer = stackalloc byte[16];
            //Generate non zero bytes
            GetRandomBytes(buffer);
            //Get a GUID initialized with the key data and return the string represendation
            return new Guid(buffer);
        }

        /// <summary>
        /// Generates a cryptographic random number and returns the base64 string of that number
        /// </summary>
        /// <param name="size">Number of random bytes</param>
        /// <returns>Base64 string of the random number</returns>
        public static string GetRandomBase64(int size = 64)
        {
            //Get temp buffer
            using UnsafeMemoryHandle<byte> buffer = Memory.UnsafeAlloc<byte>(size);
            //Generate non zero bytes
            GetRandomBytes(buffer.Span);
            //Convert to base 64
            return Convert.ToBase64String(buffer.Span, Base64FormattingOptions.None);
        }
        /// <summary>
        /// Generates a cryptographic random number and returns the hex string of that number
        /// </summary>
        /// <param name="size">Number of random bytes</param>
        /// <returns>Hex string of the random number</returns>
        public static string GetRandomHex(int size = 64)
        {
            //Get temp buffer
            using UnsafeMemoryHandle<byte> buffer = Memory.UnsafeAlloc<byte>(size);
            //Generate non zero bytes
            GetRandomBytes(buffer.Span);
            //Convert to hex
            return Convert.ToHexString(buffer.Span);
        }
        /// <summary>
        /// Generates a cryptographic random number and returns the Base32 encoded string of that number
        /// </summary>
        /// <param name="size">Number of random bytes</param>
        /// <returns>Base32 string of the random number</returns>
        public static string GetRandomBase32(int size = 64)
        {
            //Get temporary buffer for storing random keys
            using UnsafeMemoryHandle<byte> buffer =  Memory.UnsafeAlloc<byte>(size);
            //Fill with random non-zero bytes
            GetRandomBytes(buffer.Span);
            //Return string of encoded data
            return VnEncoding.ToBase32String(buffer.Span);
        }

        /// <summary>
        /// Allocates a new byte[] of the specified size and fills it with non-zero random values
        /// </summary>
        /// <param name="size">Number of random bytes</param>
        /// <returns>byte[] containing the random data</returns>
        public static byte[] GetRandomBytes(int size = 64)
        {
            byte[] rand = new byte[size];
            GetRandomBytes(rand);
            return rand;
        }
        /// <summary>
        /// Fill the buffer with non-zero bytes 
        /// </summary>
        /// <param name="data">Buffer to fill</param>
        public static void GetRandomBytes(Span<byte> data)
        {
            RandomNumberGenerator.Fill(data);
        }
    }
}