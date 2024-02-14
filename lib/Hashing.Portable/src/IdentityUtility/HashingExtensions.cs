/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: HashingExtensions.cs 
*
* HashingExtensions.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.Memory;

namespace VNLib.Hashing.IdentityUtility
{
    /// <summary>
    /// Contains .NET cryptography hasing library extensions
    /// </summary>
    public static class HashingExtensions
    {
        /// <summary>
        /// Computes the Base64 hash of the specified data using the 
        /// specified character encoding, or <see cref="Encoding.UTF8"/> 
        /// by default.
        /// </summary>
        /// <param name="hmac"></param>
        /// <param name="data">The data to compute the hash of</param>
        /// <param name="encoding">The <see cref="Encoding"/> used to encode the character buffer</param>
        /// <returns>The base64 UTF8 string of the computed hash of the specified data</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static string ComputeBase64Hash(this HMAC hmac, ReadOnlySpan<char> data, Encoding? encoding = null)
        {
            ArgumentNullException.ThrowIfNull(hmac);
            
            encoding ??= Encoding.UTF8;
            
            //Calc hashsize to alloc buffer
            int hashBufSize = (hmac.HashSize / 8);
            
            //Calc buffer size
            int encBufSize = encoding.GetByteCount(data);

            //Alloc buffer for encoding data
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(encBufSize + hashBufSize);
            
            Span<byte> encBuffer = buffer.Span[0..encBufSize];
            Span<byte> hashBuffer = buffer.Span[encBufSize..];

            //Encode data
            int encoed = encoding.GetBytes(data, encBuffer);
            Debug.Assert(encoed == encBufSize, "Encoder actual encoded bytes not equal to computed encoding size");

            //compute hash
            if (!hmac.TryComputeHash(encBuffer, hashBuffer, out int hashBytesWritten))
            {
                throw new InternalBufferTooSmallException("Hash buffer size was too small");
            }
            
            //Convert to base64 string
            return Convert.ToBase64String(hashBuffer[..hashBytesWritten]);
        }

        /// <summary>
        /// Computes the hash of the raw data and compares the computed hash against 
        /// the specified base64hash
        /// </summary>
        /// <param name="hmac"></param>
        /// <param name="raw">The raw data buffer (encoded characters) to decode and compute the hash of</param>
        /// <param name="base64Hmac">The base64 hash to verify against</param>
        /// <param name="encoding">The encoding used to encode the raw data balue</param>
        /// <returns>A value indicating if the hash values match</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static bool VerifyBase64Hash(this HMAC hmac, ReadOnlySpan<char> base64Hmac, ReadOnlySpan<char> raw, Encoding? encoding = null)
        {
            ArgumentNullException.ThrowIfNull(hmac);

            ArgumentOutOfRangeException.ThrowIfZero(raw.Length, nameof(raw));
            ArgumentOutOfRangeException.ThrowIfZero(base64Hmac.Length, nameof(base64Hmac));
            
            encoding ??= Encoding.UTF8;
            
            //Calc buffer size
            int rawDataBufSize = encoding.GetByteCount(raw);
            
            //Calc base64 buffer size
            int base64BufSize = base64Hmac.Length;
            
            //Alloc buffer for encoding and raw data
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(rawDataBufSize + base64BufSize, true);
            
            Span<byte> rawDataBuf =  buffer.Span[0..rawDataBufSize];
            Span<byte> base64Buf = buffer.Span[rawDataBufSize..];
            
            //encode
            int encoed = encoding.GetBytes(raw, rawDataBuf);
            Debug.Assert(encoed == rawDataBufSize, "Encoder actual encoded bytes not equal to computed encoding size");

            //Convert to binary
            if(!Convert.TryFromBase64Chars(base64Hmac, base64Buf, out int base64Converted))
            {
                throw new InternalBufferTooSmallException("Base64 buffer too small");
            }

            //Compare hash buffers
            return hmac.VerifyHash(base64Buf[0..base64Converted], rawDataBuf);
        }

        /// <summary>
        /// Computes the hash of the raw data and compares the computed hash against 
        /// the specified hash
        /// </summary>
        /// <param name="hmac"></param>
        /// <param name="raw">The raw data to verify the hash of</param>
        /// <param name="hash">The hash to compare against the computed data</param>
        /// <returns>A value indicating if the hash values match</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static bool VerifyHash(this HMAC hmac, ReadOnlySpan<byte> hash, ReadOnlySpan<byte> raw)
        {
            ArgumentNullException.ThrowIfNull(hmac);
            ArgumentOutOfRangeException.ThrowIfZero(raw.Length, nameof(raw));
            ArgumentOutOfRangeException.ThrowIfZero(hash.Length, nameof(hash));

            //Calc hashsize to alloc buffer
            int hashBufSize = hmac.HashSize / 8;
            
            //Alloc buffer for hash
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(hashBufSize);
            
            //compute hash
            if (!hmac.TryComputeHash(raw, buffer.Span, out int hashBytesWritten))
            {
                throw new InternalBufferTooSmallException("Hash buffer size was too small");
            }
            
            //Compare hash buffers
            return CryptographicOperations.FixedTimeEquals(buffer.Span[0..hashBytesWritten], hash);
        }

        /// <summary>
        /// Attempts to encrypt the specified character buffer using the specified encoding
        /// </summary>
        /// <param name="alg"></param>
        /// <param name="data">The data to encrypt</param>
        /// <param name="output">The output buffer</param>
        /// <param name="padding">The encryption padding to use</param>
        /// <param name="enc">Character encoding used to encode the character buffer</param>
        /// <returns>The number of bytes encrypted, or 0/false otherwise</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="CryptographicException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static ERRNO TryEncrypt(this RSA alg, ReadOnlySpan<char> data, Span<byte> output, RSAEncryptionPadding padding, Encoding? enc = null)
        {
            ArgumentNullException.ThrowIfNull(alg);
            
            //Default to UTF8 encoding
            enc ??= Encoding.UTF8;
            
            //Alloc decode buffer
            int buffSize = enc.GetByteCount(data);

            //Alloc buffer
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(buffSize, true);
            
            //Encode data
            int converted = enc.GetBytes(data, buffer.Span);
            
            //Try encrypt
            return !alg.TryEncrypt(buffer.Span, output, padding, out int bytesWritten) ? ERRNO.E_FAIL : (ERRNO)bytesWritten;
        }

        /// <summary>
        /// Gets the <see cref="HashAlgorithmName"/> for the current <see cref="HashAlg"/>
        /// value.
        /// </summary>
        /// <param name="alg"></param>
        /// <returns>The <see cref="HashAlgorithmName"/> of the current <see cref="HashAlg"/></returns>
        public static HashAlgorithmName GetAlgName(this HashAlg alg)
        {
            return alg switch
            {
                HashAlg.SHA3_512 => HashAlgorithmName.SHA3_512,
                HashAlg.SHA3_384 => HashAlgorithmName.SHA3_384,
                HashAlg.SHA3_256 => HashAlgorithmName.SHA3_256,
                HashAlg.SHA512 => HashAlgorithmName.SHA512,
                HashAlg.SHA384 => HashAlgorithmName.SHA384,
                HashAlg.SHA256 => HashAlgorithmName.SHA256,
                HashAlg.SHA1 => HashAlgorithmName.SHA1,
                HashAlg.MD5 => HashAlgorithmName.MD5,
                _ => new(alg.ToString()),
            };
        }
    }
}
