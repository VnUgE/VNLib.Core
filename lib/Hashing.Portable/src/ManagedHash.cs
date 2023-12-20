/*
* Copyright (c) 2023 Vaughn Nugent
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
using VNLib.Hashing.Native.MonoCypher;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing
{

    /// <summary>
    /// Provides simple methods for common managed hashing functions
    /// </summary>
    public static partial class ManagedHash
    {
        private static readonly Encoding CharEncoding = Encoding.UTF8;

        /// <summary>
        /// A .NET explicit ECCurve for the secp256k1 EC curve algorithm
        /// </summary>
        public static ECCurve CurveSecp256k1 { get; } = GetStaticCurve();

        private static ECCurve GetStaticCurve()
        {
            //Curve parameters from https://www.secg.org/sec2-v2.pdf
            return new()
            {
                CurveType = ECCurve.ECCurveType.PrimeShortWeierstrass,

                Prime = Convert.FromHexString("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F"),

                A = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"),
                B = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000007"),

                G = new ECPoint()
                {
                    X = Convert.FromHexString("79BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798"),
                    Y = Convert.FromHexString("483ADA7726A3C4655DA4FBFC0E1108A8FD17B448A68554199C47D08FFB10D4B8")
                },

                Order = Convert.FromHexString("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141"),
                Cofactor = Convert.FromHexString("01")
            };
        }

        private static readonly Sha1 _sha1Alg;
        private static readonly Sha256 _sha256Alg;
        private static readonly Sha384 _sha384Alg;
        private static readonly Sha512 _sha512Alg;
        private static readonly Md5 _md5Alg;
        private static readonly Blake2b _blake2bAlg;

        /// <summary>
        /// Gets a value that indicates whether the current runtime has the required libraries 
        /// available to support the Blake2b hashing algorithm
        /// </summary>
        public static bool SupportsBlake2b => MonoCypherLibrary.CanLoadDefaultLibrary(); 

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
            return type switch
            {
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, buffer),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, buffer),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, buffer),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, buffer),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, buffer),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, buffer),                
                _ => throw new ArgumentException("Invalid hash algorithm", nameof(type))
            };
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
            return type switch
            {
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data),               
                _ => throw new ArgumentException("Invalid hash algorithm", nameof(type))
            };
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
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, output),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, output),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, output),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, output),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, output),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, output),                
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
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data),               
                _ => throw new ArgumentException("Invalid hash algorithm", nameof(type))
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
            return type switch
            {
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, mode),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, mode),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, mode),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, mode),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, mode),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, mode),                
                _ => throw new ArgumentException("Invalid hash algorithm", nameof(type))
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
            return type switch
            {
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, mode),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, mode),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, mode),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, mode),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, mode),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, mode),              
                _ => throw new ArgumentException("Invalid hash algorithm", nameof(type))
            };
        }

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
            return type switch
            {
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, output, key),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, output, key),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, output, key),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, output, key),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, output, key),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, output, key),                
                _ => ERRNO.E_FAIL
            };
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
            return type switch
            {
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, key),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, key),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, key),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, key),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, key),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, key),               
                _ => throw new ArgumentException("Hash algorithm is not supported"),
            };
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
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, output, key),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, output, key),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, output, key),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, output, key),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, output, key),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, output, key),               
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
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, key),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, key),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, key),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, key),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, key),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, key),              
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
            return type switch
            {
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, mode, key),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, mode, key),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, mode, key),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, mode, key),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, mode, key),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, mode, key),
                _ => throw new ArgumentException("Invalid hash algorithm", nameof(type))
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
            return type switch
            {
                HashAlg.BlAKE2B => ComputeHashInternal(in _blake2bAlg, data, mode, key),
                HashAlg.SHA512 => ComputeHashInternal(in _sha512Alg, data, mode, key),
                HashAlg.SHA384 => ComputeHashInternal(in _sha384Alg, data, mode, key),
                HashAlg.SHA256 => ComputeHashInternal(in _sha256Alg, data, mode, key),
                HashAlg.SHA1 => ComputeHashInternal(in _sha1Alg, data, mode, key),
                HashAlg.MD5 => ComputeHashInternal(in _md5Alg, data, mode, key),
                _ => throw new ArgumentException("Invalid hash algorithm", nameof(type))
            };
        }

        #region internal

        private static byte[] ComputeHashInternal<T>(in T algorithm, ReadOnlySpan<char> data, ReadOnlySpan<byte> key = default) where T : IHashAlgorithm
        {
            int byteCount = CharEncoding.GetByteCount(data);
            //Alloc buffer
            using UnsafeMemoryHandle<byte> binbuf = MemoryUtil.UnsafeAlloc(byteCount, true);
            //Encode data
            byteCount = CharEncoding.GetBytes(data, binbuf.Span);
            //hash the buffer
            return ComputeHashInternal(in algorithm, binbuf.AsSpan(0, byteCount), key);
        }

        private static string ComputeHashInternal<T>(in T algorithm, ReadOnlySpan<char> data, HashEncodingMode mode, ReadOnlySpan<byte> key = default) where T : IHashAlgorithm
        {
            //Alloc stack buffer to store hash output
            Span<byte> hashBuffer = stackalloc byte[algorithm.HashSize];
            //hash the buffer
            ERRNO count = ComputeHashInternal(in algorithm, data, hashBuffer, key);
            if (!count)
            {
                throw new CryptographicException("Failed to compute the hash of the data");
            }

            //Convert to encoded string 
            return mode switch
            {
                HashEncodingMode.Hexadecimal => Convert.ToHexString(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base64 => Convert.ToBase64String(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base32 => VnEncoding.ToBase32String(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base64Url => VnEncoding.ToBase64UrlSafeString(hashBuffer.Slice(0, count), true),
                _ => throw new ArgumentException("Encoding mode is not supported"),
            };
        }

        private static string ComputeHashInternal<T>(in T algorithm, ReadOnlySpan<byte> data, HashEncodingMode mode, ReadOnlySpan<byte> key = default) where T : IHashAlgorithm
        {
            //Alloc stack buffer to store hash output
            Span<byte> hashBuffer = stackalloc byte[algorithm.HashSize];

            //hash the buffer
            ERRNO count = ComputeHashInternal(in algorithm, data, hashBuffer, key);
            if (!count)
            {
                throw new CryptographicException("Failed to compute the hash of the data");
            }

            //Convert to encoded string 
            return mode switch
            {
                HashEncodingMode.Hexadecimal => Convert.ToHexString(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base64 => Convert.ToBase64String(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base32 => VnEncoding.ToBase32String(hashBuffer.Slice(0, count)),
                HashEncodingMode.Base64Url => VnEncoding.ToBase64UrlSafeString(hashBuffer.Slice(0, count), true),
                _ => throw new ArgumentException("Encoding mode is not supported"),
            };
        }

        private static ERRNO ComputeHashInternal<T>(in T algorithm, ReadOnlySpan<char> data, Span<byte> output, ReadOnlySpan<byte> key = default) where T : IHashAlgorithm
        {
            int byteCount = CharEncoding.GetByteCount(data);
            //Alloc buffer
            using UnsafeMemoryHandle<byte> binbuf = MemoryUtil.UnsafeAlloc(byteCount, true);
            //Encode data
            byteCount = CharEncoding.GetBytes(data, binbuf.Span);
            //hash the buffer or hmac if key is not empty
            return ComputeHashInternal(in algorithm, binbuf.Span[..byteCount], output, key);
        }


        private static ERRNO ComputeHashInternal<T>(in T algorithm, ReadOnlySpan<byte> data, Span<byte> buffer, ReadOnlySpan<byte> key = default) where T : IHashAlgorithm
        {
            //hash the buffer or hmac if key is not empty
            if (key.IsEmpty)
            {
                return algorithm.TryComputeHash(data, buffer, out int written) ? written : ERRNO.E_FAIL;
            }
            else
            {
                return algorithm.TryComputeHmac(key, data, buffer, out int written) ? written : ERRNO.E_FAIL;
            }
        }

        private static byte[] ComputeHashInternal<T>(in T algorithm, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key = default) where T : IHashAlgorithm
        {
            //hash the buffer or hmac if key is not empty
            if (key.IsEmpty)
            {
                return algorithm.ComputeHash(data);
            }
            else
            {
                return algorithm.ComputeHmac(key, data);
            }
        }     

        #endregion
    }
}
