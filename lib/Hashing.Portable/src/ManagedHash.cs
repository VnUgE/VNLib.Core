/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: ManagedHash.cs 
*
* ManagedHash.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.Memory;

namespace VNLib.Hashing
{
    public enum HashAlg
    {
        SHA512 = 64,
        SHA384 = 48,
        SHA256 = 32,
        SHA1 = 20,
        MD5 = 16
    }

    /// <summary>
    /// The binary hash encoding type
    /// </summary>
    [Flags]
    public enum HashEncodingMode
    {
        /// <summary>
        /// Specifies the Base64 character encoding
        /// </summary>
        Base64 = 64,
        /// <summary>
        /// Specifies the hexadecimal character encoding
        /// </summary>
        Hexadecimal = 16,
        /// <summary>
        /// Specifies the Base32 character encoding
        /// </summary>
        Base32 = 32
    }

    /// <summary>
    /// Provides simple methods for common managed hashing functions
    /// </summary>
    public static partial class ManagedHash
    {
        private static readonly Encoding CharEncoding = Encoding.UTF8;

        /// <summary>
        /// Uses the UTF8 character encoding to encode the string, then 
        /// attempts to compute the hash and store the results into the output buffer
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="buffer">The hash output buffer</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <returns>The number of bytes written to the buffer, false if the hash could not be computed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static ERRNO ComputeHash(ReadOnlySpan<char> data, Span<byte> buffer, HashAlg type)
        {
            int byteCount = CharEncoding.GetByteCount(data);
            
            //Alloc buffer
            using UnsafeMemoryHandle<byte> binbuf = MemoryUtil.UnsafeAlloc<byte>(byteCount, true);

            //Encode data
            byteCount = CharEncoding.GetBytes(data, binbuf);
            
            //hash the buffer
            return ComputeHash(binbuf.Span[..byteCount], buffer, type);
        }

        /// <summary>
        /// Uses the UTF8 character encoding to encode the string, then 
        /// attempts to compute the hash and store the results into the output buffer
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <returns>The number of bytes written to the buffer, false if the hash could not be computed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] ComputeHash(ReadOnlySpan<char> data, HashAlg type)
        {
            int byteCount = CharEncoding.GetByteCount(data);
            //Alloc buffer
            using UnsafeMemoryHandle<byte> binbuf = MemoryUtil.UnsafeAlloc<byte>(byteCount, true);
            //Encode data
            byteCount = CharEncoding.GetBytes(data, binbuf);
            //hash the buffer
            return ComputeHash(binbuf.Span[..byteCount], type);
        }

        /// <summary>
        /// Hashes the data parameter to the output buffer using the specified algorithm type
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="output">The hash output buffer</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <returns>The number of bytes written to the buffer, <see cref="ERRNO.E_FAIL"/> if the hash could not be computed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static ERRNO ComputeHash(ReadOnlySpan<byte> data, Span<byte> output, HashAlg type)
        {
            //hash the buffer
            return type switch
            {
                HashAlg.SHA512 => SHA512.TryHashData(data, output, out int count) ? count : ERRNO.E_FAIL,
                HashAlg.SHA384 => SHA384.TryHashData(data, output, out int count) ? count : ERRNO.E_FAIL,
                HashAlg.SHA256 => SHA256.TryHashData(data, output, out int count) ? count : ERRNO.E_FAIL,
                HashAlg.SHA1 => SHA1.TryHashData(data, output, out int count) ? count : ERRNO.E_FAIL,
                HashAlg.MD5 => MD5.TryHashData(data, output, out int count) ? count : ERRNO.E_FAIL,
                _ => throw new ArgumentException("Hash algorithm is not supported"),
            };
        }

        /// <summary>
        /// Hashes the data parameter to the output buffer using the specified algorithm type
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <returns>A byte array that contains the hash of the data buffer</returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] ComputeHash(ReadOnlySpan<byte> data, HashAlg type)
        {
            //hash the buffer
            return type switch
            {
                HashAlg.SHA512 => SHA512.HashData(data),
                HashAlg.SHA384 => SHA384.HashData(data),
                HashAlg.SHA256 => SHA256.HashData(data),
                HashAlg.SHA1 => SHA1.HashData(data),
                HashAlg.MD5 => MD5.HashData(data),
                _ => throw new ArgumentException("Hash algorithm is not supported"),
            };
        }

        /// <summary>
        /// Hashes the data parameter to the output buffer using the specified algorithm type
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <param name="mode">The data encoding mode</param>
        /// <returns>The encoded hash of the input data</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="CryptographicException"></exception>
        public static string ComputeHash(ReadOnlySpan<byte> data, HashAlg type, HashEncodingMode mode)
        {
            //Alloc hash buffer
            Span<byte> hashBuffer = stackalloc byte[(int)type];
            //hash the buffer
            ERRNO count = ComputeHash(data, hashBuffer, type);
            if (!count)
            {
                throw new CryptographicException("Failed to compute the hash of the data");
            }
            //Convert to hex string
            return mode switch
            {
                HashEncodingMode.Hexadecimal => Convert.ToHexString(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base64 => Convert.ToBase64String(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base32 => VnEncoding.ToBase32String(hashBuffer.Slice(0, count)),
                _ => throw new ArgumentException("Encoding mode is not supported"),
            };
        }

        /// <summary>
        /// Uses the UTF8 character encoding to encode the string, then computes the hash and encodes 
        /// the hash to the specified encoding
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <param name="mode">The data encoding mode</param>
        /// <returns>The encoded hash of the input data</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="CryptographicException"></exception>
        public static string ComputeHash(ReadOnlySpan<char> data, HashAlg type, HashEncodingMode mode)
        {
            //Alloc hash buffer
            Span<byte> hashBuffer = stackalloc byte[(int)type];
            //hash the buffer
            ERRNO count = ComputeHash(data, hashBuffer, type);
            if (!count)
            {
                throw new CryptographicException("Failed to compute the hash of the data");
            }
            //Convert to hex string
            return mode switch
            {
                HashEncodingMode.Hexadecimal => Convert.ToHexString(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base64 => Convert.ToBase64String(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base32 => VnEncoding.ToBase32String(hashBuffer.Slice(0, count)),
                _ => throw new ArgumentException("Encoding mode is not supported"),
            };
        }


        public static string ComputeHexHash(ReadOnlySpan<byte> data, HashAlg type) => ComputeHash(data, type, HashEncodingMode.Hexadecimal);
        public static string ComputeBase64Hash(ReadOnlySpan<byte> data, HashAlg type) => ComputeHash(data, type, HashEncodingMode.Base64);
        public static string ComputeHexHash(ReadOnlySpan<char> data, HashAlg type) => ComputeHash(data, type, HashEncodingMode.Hexadecimal);
        public static string ComputeBase64Hash(ReadOnlySpan<char> data, HashAlg type) => ComputeHash(data, type, HashEncodingMode.Base64);

        /// <summary>
        /// Computes the HMAC of the specified character buffer using the specified key and 
        /// writes the resuts to the output buffer.
        /// </summary>
        /// <param name="key">The HMAC key</param>
        /// <param name="data">The character buffer to compute the encoded HMAC of</param>
        /// <param name="output">The buffer to write the hash to</param>
        /// <param name="type">The <see cref="HashAlg"/> type used to compute the HMAC</param>
        /// <returns>The number of bytes written to the ouput buffer or <see cref="ERRNO.E_FAIL"/> if the operation failed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static ERRNO ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<char> data, Span<byte> output, HashAlg type)
        {
            int byteCount = CharEncoding.GetByteCount(data);
            
            //Alloc buffer
            using UnsafeMemoryHandle<byte> binbuf = MemoryUtil.UnsafeAlloc<byte>(byteCount, true);
            
            //Encode data
            byteCount = CharEncoding.GetBytes(data, binbuf);

            //hash the buffer
            return ComputeHmac(key, binbuf.Span[..byteCount], output, type);
        }

        /// <summary>
        /// Computes the HMAC of the specified character buffer using the specified key and 
        /// writes the resuts to a new buffer to return
        /// </summary>
        /// <param name="key">The HMAC key</param>
        /// <param name="data">The data buffer to compute the HMAC of</param>
        /// <param name="type">The <see cref="HashAlg"/> type used to compute the HMAC</param>
        /// <returns>A buffer containg the computed HMAC</returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<char> data, HashAlg type)
        {
            int byteCount = CharEncoding.GetByteCount(data);
            
            //Alloc buffer
            using UnsafeMemoryHandle<byte> binbuf = MemoryUtil.UnsafeAlloc<byte>(byteCount, true);

            //Encode data
            byteCount = CharEncoding.GetBytes(data, binbuf);

            //hash the buffer
            return ComputeHmac(key, binbuf.Span[..byteCount], type);
        }
        /// <summary>
        /// Computes the HMAC of the specified data buffer using the specified key and 
        /// writes the resuts to the output buffer.
        /// </summary>
        /// <param name="key">The HMAC key</param>
        /// <param name="data">The data buffer to compute the HMAC of</param>
        /// <param name="output">The buffer to write the hash to</param>
        /// <param name="type">The <see cref="HashAlg"/> type used to compute the HMAC</param>
        /// <returns>The number of bytes written to the ouput buffer or <see cref="ERRNO.E_FAIL"/> if the operation failed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static ERRNO ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, HashAlg type)
        {
            //hash the buffer
            return type switch
            {
                HashAlg.SHA512 => HMACSHA512.TryHashData(key, data, output, out int count) ? count : ERRNO.E_FAIL,
                HashAlg.SHA384 => HMACSHA384.TryHashData(key, data, output, out int count) ? count : ERRNO.E_FAIL,
                HashAlg.SHA256 => HMACSHA256.TryHashData(key, data, output, out int count) ? count : ERRNO.E_FAIL,               
                HashAlg.SHA1 => HMACSHA1.TryHashData(key, data, output, out int count) ? count : ERRNO.E_FAIL,
                HashAlg.MD5 => HMACMD5.TryHashData(key, data, output, out int count) ? count : ERRNO.E_FAIL,
                _ => throw new ArgumentException("Hash algorithm is not supported"),
            };
        }

        /// <summary>
        /// Computes the HMAC of the specified data buffer using the specified key and 
        /// writes the resuts to a new buffer to return
        /// </summary>
        /// <param name="key">The HMAC key</param>
        /// <param name="data">The data buffer to compute the HMAC of</param>
        /// <param name="type">The <see cref="HashAlg"/> type used to compute the HMAC</param>
        /// <returns>A buffer containg the computed HMAC</returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, HashAlg type)
        {
            //hash the buffer
            return type switch
            {
                HashAlg.SHA512 => HMACSHA512.HashData(key, data),
                HashAlg.SHA384 => HMACSHA384.HashData(key, data),
                HashAlg.SHA256 => HMACSHA256.HashData(key, data),
                HashAlg.SHA1 => HMACSHA1.HashData(key, data),
                HashAlg.MD5 => HMACMD5.HashData(key, data),
                _ => throw new ArgumentException("Hash algorithm is not supported"),
            };
        }

        /// <summary>
        /// Computes the HMAC of the specified data buffer and encodes the result in
        /// the specified <see cref="HashEncodingMode"/>
        /// </summary>
        /// <param name="key">The HMAC key</param>
        /// <param name="data">The data buffer to compute the HMAC of</param>
        /// <param name="type">The <see cref="HashAlg"/> type used to compute the HMAC</param>
        /// <param name="mode">The encoding type for the output data</param>
        /// <returns>The encoded string of the result</returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, HashAlg type, HashEncodingMode mode)
        {
            //Alloc hash buffer
            Span<byte> hashBuffer = stackalloc byte[(int)type];
            
            //hash the buffer
            ERRNO count = ComputeHmac(key, data, hashBuffer, type);
            
            if (!count)
            {
                throw new InternalBufferTooSmallException("Failed to compute the hash of the data");
            }
            
            //Convert to hex string
            return mode switch
            {
                HashEncodingMode.Hexadecimal => Convert.ToHexString(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base64 => Convert.ToBase64String(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base32 => VnEncoding.ToBase32String(hashBuffer.Slice(0, count)),
                _ => throw new ArgumentException("Encoding mode is not supported"),
            };
        }

        /// <summary>
        /// Computes the HMAC of the specified data buffer and encodes the result in
        /// the specified <see cref="HashEncodingMode"/>
        /// </summary>
        /// <param name="key">The HMAC key</param>
        /// <param name="data">The character buffer to compute the HMAC of</param>
        /// <param name="type">The <see cref="HashAlg"/> type used to compute the HMAC</param>
        /// <param name="mode">The encoding type for the output data</param>
        /// <returns>The encoded string of the result</returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<char> data, HashAlg type, HashEncodingMode mode)
        {
            //Alloc hash buffer
            Span<byte> hashBuffer = stackalloc byte[(int)type];

            //hash the buffer
            ERRNO count = ComputeHmac(key, data, hashBuffer, type);
            
            if (!count)
            {
                throw new InternalBufferTooSmallException("Failed to compute the hash of the data");
            }
            
            //Convert to hex string
            return mode switch
            {
                HashEncodingMode.Hexadecimal => Convert.ToHexString(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base64 => Convert.ToBase64String(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base32 => VnEncoding.ToBase32String(hashBuffer.Slice(0, count)),
                _ => throw new ArgumentException("Encoding mode is not supported"),
            };
        }
    }
}
