/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: NonceExtensions.cs 
*
* NonceExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

using VNLib.Utils;
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// Provides INonce extensions for computing/verifying nonce values
    /// </summary>
    public static class NonceExtensions
    {
        /// <summary>
        /// Computes a base32 nonce of the specified size and returns a string
        /// representation
        /// </summary>
        /// <param name="nonce"></param>
        /// <param name="size">The size (in bytes) of the nonce</param>
        /// <returns>The base32 string of the computed nonce</returns>
        public static string ComputeNonce<T>(this T nonce, int size) where T: INonce
        {
            //Alloc bin buffer
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(size);

            //Compute nonce
            nonce.ComputeNonce(buffer.Span);
            
            //Return base32 string
            return VnEncoding.ToBase32String(buffer.Span, false);
        }
        
        /// <summary>
        /// Compares the base32 encoded nonce value against the previously
        /// generated nonce
        /// </summary>
        /// <param name="nonce"></param>
        /// <param name="base32Nonce">The base32 encoded nonce string</param>
        /// <returns>True if the nonce values are equal, flase otherwise</returns>
        public static bool VerifyNonce<T>(this T nonce, ReadOnlySpan<char> base32Nonce) where T : INonce
        {
            //Alloc bin buffer
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(base32Nonce.Length);
            
            //Decode base32 nonce
            ERRNO count = VnEncoding.TryFromBase32Chars(base32Nonce, buffer.Span);
            
            //Verify nonce
            return nonce.VerifyNonce(buffer.Span[..(int)count]);
        }
    }
}
