/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Diagnostics;
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.Memory;

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

        private static readonly Sha3_256 _3_sha256;
        private static readonly Sha3_384 _3_sha384;
        private static readonly Sha3_512 _3_sha512;

        /// <summary>
        /// Gets a value that indicates whether the current runtime has the required libraries 
        /// available to support the Blake2b hashing algorithm
        /// </summary>
        public static bool SupportsBlake2b => IsAlgSupported(HashAlg.BlAKE2B);

        /// <summary>
        /// Gets a value that indicates whether the current platform supports the SHA3 
        /// hashing algorithm.
        /// </summary>
        public static bool SupportsSha3 { get; } = IsAlgSupported(HashAlg.SHA3_512) && IsAlgSupported(HashAlg.SHA3_384) && IsAlgSupported(HashAlg.SHA3_256);       

        /// <summary>
        /// Determines if the specified hash algorithm is supported by 
        /// the current runtime.
        /// </summary>
        /// <param name="type">The algorithm to verify</param>
        /// <returns>A value that indicates if the algorithm is supported</returns>
        public static bool IsAlgSupported(HashAlg type) => type switch
        {
            HashAlg.SHA3_512 => Sha3_512.IsSupported,
            HashAlg.SHA3_384 => Sha3_384.IsSupported,
            HashAlg.SHA3_256 => Sha3_256.IsSupported,
            HashAlg.BlAKE2B => Blake2b.IsSupported,
            HashAlg.SHA512 => true, //Built-in functions are always supported
            HashAlg.SHA384 => true,
            HashAlg.SHA256 => true,
            HashAlg.SHA1 => true,
            HashAlg.MD5 => true,
            _ => false
        };

        /// <summary>
        /// Gets the size of the hash (in bytes) for the specified algorithm
        /// </summary>
        /// <param name="type">The hash algorithm to get the size of</param>
        /// <returns>A positive 32-bit integer size (in bytes) of the algorithm hash size</returns>
        /// <exception cref="ArgumentException"></exception>
        public static int GetHashSize(HashAlg type) => type switch
        {
            HashAlg.SHA3_512 => _3_sha512.HashSize,
            HashAlg.SHA3_384 => _3_sha384.HashSize,
            HashAlg.SHA3_256 => _3_sha256.HashSize,
            HashAlg.BlAKE2B => _blake2bAlg.HashSize,
            HashAlg.SHA512 => _sha512Alg.HashSize,
            HashAlg.SHA384 => _sha384Alg.HashSize,
            HashAlg.SHA256 => _sha256Alg.HashSize,
            HashAlg.SHA1 => _sha1Alg.HashSize,
            HashAlg.MD5 => _md5Alg.HashSize,
            _ => throw new ArgumentException("Invalid hash algorithm", nameof(type))
        };

        /// <summary>
        /// Uses the UTF8 character encoding to encode the string, then 
        /// attempts to compute the hash and store the results into the output buffer
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="output">The hash output buffer</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <returns>The number of bytes written to the buffer, false if the hash could not be computed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static ERRNO ComputeHash(ReadOnlySpan<char> data, Span<byte> output, HashAlg type) 
            => ComputeHashInternal(type, data, output, default);

        /// <summary>
        /// Uses the UTF8 character encoding to encode the string, then 
        /// attempts to compute the hash and store the results into the output buffer
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <returns>The number of bytes written to the buffer, false if the hash could not be computed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] ComputeHash(ReadOnlySpan<char> data, HashAlg type) 
            => ComputeHashInternal(type, data, default);

        /// <summary>
        /// Hashes the data parameter to the output buffer using the specified algorithm type
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="output">The hash output buffer</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <returns>The number of bytes written to the buffer, <see cref="ERRNO.E_FAIL"/> if the hash could not be computed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static ERRNO ComputeHash(ReadOnlySpan<byte> data, Span<byte> output, HashAlg type) 
            => ComputeHashInternal(type, data, output, default);

        /// <summary>
        /// Hashes the data parameter to the output buffer using the specified algorithm type
        /// </summary>
        /// <param name="data">String to hash</param>
        /// <param name="type">The hash algorithm to use</param>
        /// <returns>A byte array that contains the hash of the data buffer</returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] ComputeHash(ReadOnlySpan<byte> data, HashAlg type) 
            => ComputeHashInternal(type, data, default);

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
            => ComputeHashInternal(type, data, mode, default);

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
            => ComputeHashInternal(type, data, mode, default);

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
            => ComputeHashInternal(type, data, output, key);      

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
            => ComputeHashInternal(type, data, key);

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
            => ComputeHashInternal(type, data, output, key);

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
            => ComputeHashInternal(type, data, key);

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
            => ComputeHashInternal(type, data, mode, key);

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
            => ComputeHashInternal(type, data, mode, key);

        #region internal


        private static byte[] ComputeHashInternal(HashAlg alg, ReadOnlySpan<char> data, ReadOnlySpan<byte> key)
        {
            //Alloc output buffer
            byte[] output = new byte[GetHashSize(alg)];

            //Hash data
            ERRNO result = ComputeHashInternal(alg, data, output, key);
            Debug.Assert(result == GetHashSize(alg), $"Failed to compute hash using {alg} of size {output.Length}");

            return output;
        }

        private static ERRNO ComputeHashInternal(HashAlg alg, ReadOnlySpan<char> data, Span<byte> output, ReadOnlySpan<byte> key)
        {
            int byteCount = CharEncoding.GetByteCount(data);
            //Alloc buffer
            using UnsafeMemoryHandle<byte> binbuf = MemoryUtil.UnsafeAlloc(byteCount, true);
            //Encode data
            byteCount = CharEncoding.GetBytes(data, binbuf.Span);
            //hash the buffer or hmac if key is not empty
            return ComputeHashInternal(alg, binbuf.Span[..byteCount], output, key);
        }

        private static string ComputeHashInternal(HashAlg alg, ReadOnlySpan<char> data, HashEncodingMode mode, ReadOnlySpan<byte> key)
        {
            //Alloc stack buffer to store hash output
            Span<byte> hashBuffer = stackalloc byte[GetHashSize(alg)];

            //hash the buffer
            ERRNO count = ComputeHashInternal(alg, data, hashBuffer, key);

            //Count should always be the same as the hash size, this should never fail
            Debug.Assert(count == GetHashSize(alg), $"Failed to compute hash using {alg} of size {hashBuffer.Length}");

            //Convert to encoded string 
            return mode switch
            {
                HashEncodingMode.Hexadecimal => Convert.ToHexString(hashBuffer),
                HashEncodingMode.Base64 => Convert.ToBase64String(hashBuffer),
                HashEncodingMode.Base32 => VnEncoding.ToBase32String(hashBuffer),
                HashEncodingMode.Base64Url => VnEncoding.Base64UrlEncode(hashBuffer, true),
                _ => throw new ArgumentException("Encoding mode is not supported"),
            };
        }

        private static string ComputeHashInternal(HashAlg alg, ReadOnlySpan<byte> data, HashEncodingMode mode, ReadOnlySpan<byte> key) 
        {
            //Alloc stack buffer to store hash output
            Span<byte> hashBuffer = stackalloc byte[GetHashSize(alg)];

            //hash the buffer
            ERRNO count = ComputeHashInternal(alg, data, hashBuffer, key);

            //Count should always be the same as the hash size, this should never fail
            Debug.Assert(count == GetHashSize(alg), $"Failed to compute hash using {alg} of size {hashBuffer.Length}");

            //Convert to encoded string 
            return mode switch
            {
                HashEncodingMode.Hexadecimal => Convert.ToHexString(hashBuffer),
                HashEncodingMode.Base64 => Convert.ToBase64String(hashBuffer),
                HashEncodingMode.Base32 => VnEncoding.ToBase32String(hashBuffer),
                HashEncodingMode.Base64Url => VnEncoding.Base64UrlEncode(hashBuffer, true),
                _ => throw new ArgumentException("Encoding mode is not supported"),
            };
        }

        
        private static byte[] ComputeHashInternal(HashAlg alg, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
        {
            //Alloc output buffer
            byte[] output = new byte[GetHashSize(alg)];
            
            //Hash data
            ERRNO result = ComputeHashInternal(alg, data, output, key);
            Debug.Assert(result == GetHashSize(alg), $"Failed to compute hash using {alg} of size {output.Length}");

            return output;
        }


        private static ERRNO ComputeHashInternal(HashAlg alg, ReadOnlySpan<byte> data, Span<byte> buffer, ReadOnlySpan<byte> key)
        {
            return alg switch
            {
                HashAlg.SHA3_512 => computeHashInternal(in _3_sha512,   data, buffer, key),
                HashAlg.SHA3_384 => computeHashInternal(in _3_sha384,   data, buffer, key),
                HashAlg.SHA3_256 => computeHashInternal(in _3_sha256,   data, buffer, key),
                HashAlg.BlAKE2B  => computeHashInternal(in _blake2bAlg, data, buffer, key),
                HashAlg.SHA512   => computeHashInternal(in _sha512Alg,  data, buffer, key),
                HashAlg.SHA384   => computeHashInternal(in _sha384Alg,  data, buffer, key),
                HashAlg.SHA256   => computeHashInternal(in _sha256Alg,  data, buffer, key),
                HashAlg.SHA1     => computeHashInternal(in _sha1Alg,    data, buffer, key),
                HashAlg.MD5      => computeHashInternal(in _md5Alg,     data, buffer, key),
                _ => throw new ArgumentException("Invalid hash algorithm", nameof(alg))
            };

            static ERRNO computeHashInternal<T>(ref readonly T algorithm, ReadOnlySpan<byte> data, Span<byte> buffer, ReadOnlySpan<byte> key)
               where T : IHashAlgorithm
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
        }

        #endregion
    }
}
