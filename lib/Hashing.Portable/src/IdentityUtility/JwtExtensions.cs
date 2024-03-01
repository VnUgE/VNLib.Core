/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: JwtExtensions.cs 
*
* JwtExtensions.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Text.Json;
using System.Buffers.Text;
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing.IdentityUtility
{

    /// <summary>
    /// Provides extension methods for manipulating 
    /// and verifying <see cref="JsonWebToken"/>s
    /// </summary>
    public static class JwtExtensions
    {
        /// <summary>
        /// Writes the message header as the specified object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jwt"></param>
        /// <param name="header">The header object</param>
        /// <param name="jso">Optional serialize options</param>
        public static void WriteHeader<T>(this JsonWebToken jwt, T header, JsonSerializerOptions? jso = null) where T: class
        {
            ArgumentNullException.ThrowIfNull(jwt);

            byte[] data = JsonSerializer.SerializeToUtf8Bytes(header, jso);
            jwt.WriteHeader(data);
        }

        /// <summary>
        /// Writes the message payload as the specified object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jwt"></param>
        /// <param name="payload">The payload object</param>
        /// <param name="jso">Optional serialize options</param>
        public static void WritePayload<T>(this JsonWebToken jwt, T payload, JsonSerializerOptions? jso = null)
        {
            ArgumentNullException.ThrowIfNull(jwt);

            byte[] data = JsonSerializer.SerializeToUtf8Bytes(payload, jso);
            jwt.WritePayload(data);
        }

        /// <summary>
        /// Gets the payload data as a <see cref="JsonDocument"/> 
        /// </summary>
        /// <param name="jwt"></param>
        /// <returns>The <see cref="JsonDocument"/> of the jwt body</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static JsonDocument GetPayload(this JsonWebToken jwt)
        {
            ArgumentNullException.ThrowIfNull(jwt);

            ReadOnlySpan<byte> payload = jwt.PayloadData;
            if (payload.IsEmpty)
            {
                return JsonDocument.Parse("{}");
            }

            //calc padding bytes to add
            int paddingToAdd = CalcPadding(payload.Length);

            //Alloc buffer to copy jwt payload data to
            using UnsafeMemoryHandle<byte> buffer = jwt.Heap.UnsafeAlloc<byte>(payload.Length + paddingToAdd);

            //Decode from urlsafe base64
            int decoded = DecodeUnpadded(payload, buffer.Span);

            //Get json reader to read the first token (payload object) and return a document around it
            Utf8JsonReader reader = new(buffer.Span[..decoded]);

            return JsonDocument.ParseValue(ref reader);
        }

        /// <summary>
        /// Gets the header data as a <see cref="JsonDocument"/> 
        /// </summary>
        /// <param name="jwt"></param>
        /// <returns>The <see cref="JsonDocument"/> of the jwt body</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static JsonDocument GetHeader(this JsonWebToken jwt)
        {
            ArgumentNullException.ThrowIfNull(jwt);

            ReadOnlySpan<byte> header = jwt.HeaderData;
            if (header.IsEmpty)
            {
                return JsonDocument.Parse("{}");
            }
            //calc padding bytes to add
            int paddingToAdd = CalcPadding(header.Length);

            //Alloc buffer to copy jwt header data to
            using UnsafeMemoryHandle<byte> buffer = jwt.Heap.UnsafeAlloc<byte>(header.Length + paddingToAdd);

            //Decode from urlsafe base64
            int decoded = DecodeUnpadded(header, buffer.Span);

            //Get json reader to read the first token (payload object) and return a document around it
            Utf8JsonReader reader = new(buffer.Span[..decoded]);

            return JsonDocument.ParseValue(ref reader);
        }

        /*
         * Determines how many padding bytes to add at the end 
         * of the base64 unpadded buffer
         * 
         * Decodes the base64url to base64, then back to its binary 
         */
        private static int CalcPadding(int length) => (4 - (length % 4)) & 0x03;
        private static int DecodeUnpadded(ReadOnlySpan<byte> prePadding, Span<byte> output)
        {
            ERRNO count = VnEncoding.Base64UrlDecode(prePadding, output);            
            return count ? count : throw new FormatException($"Failed to decode the utf8 encoded data");
        }

        /// <summary>
        /// Deserialzes the jwt payload as the specified object
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="jso">Optional serialzie options</param>
        /// <returns>The <see cref="JsonDocument"/> of the jwt body</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static T? GetPayload<T>(this JsonWebToken jwt, JsonSerializerOptions? jso = null)
        {
            ArgumentNullException.ThrowIfNull(jwt);

            ReadOnlySpan<byte> payload = jwt.PayloadData;
            if (payload.IsEmpty)
            {
                return default;
            }
            //calc padding bytes to add
            int paddingToAdd = CalcPadding(payload.Length);

            //Alloc buffer to copy jwt payload data to
            using UnsafeMemoryHandle<byte> buffer = jwt.Heap.UnsafeAlloc<byte>(payload.Length + paddingToAdd);

            //Decode from urlsafe base64
            int decoded = DecodeUnpadded(payload, buffer.Span);

            //Deserialze as an object
            return JsonSerializer.Deserialize<T>(buffer.Span[..decoded], jso);
        }

        /// <summary>
        /// Initializes a new <see cref="JwtPayload"/> object for writing claims to the 
        /// current tokens payload segment
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="initCapacity">The inital cliam capacity</param>
        /// <returns>The fluent chainable stucture</returns>
        public static JwtPayload InitPayloadClaim(this JsonWebToken jwt, int initCapacity = 0) => new(jwt, initCapacity);

        /// <summary>
        /// Signs the current JWT (header + payload) data
        /// and writes the signature the end of the current buffer,
        /// using the specified <see cref="HashAlgorithm"/>.
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="signatureAlgorithm">An alternate <see cref="HashAlgorithm"/> instance to sign the JWT with</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static void Sign(this JsonWebToken jwt, HashAlgorithm signatureAlgorithm)
        {
            ArgumentNullException.ThrowIfNull(jwt);
            ArgumentNullException.ThrowIfNull(signatureAlgorithm);

            //Calculate the size of the buffer to use for the current algorithm
            int bufferSize = signatureAlgorithm.HashSize / 8;

            //Alloc buffer for signature output
            Span<byte> signatureBuffer = stackalloc byte[bufferSize];

            //Compute the hash of the current payload
            if (!signatureAlgorithm.TryComputeHash(jwt.HeaderAndPayload, signatureBuffer, out int bytesWritten))
            {
                throw new InternalBufferTooSmallException();
            }

            jwt.WriteSignature(signatureBuffer[..bytesWritten]);
        }

        /// <summary>
        /// Use an RSA algorithm to sign the JWT message
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="rsa">The algorithm used to sign the token</param>
        /// <param name="alg">The <see cref="HashAlg"/> use to compute the message digest</param>
        /// <param name="padding">The signature padding to use</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static void Sign(this JsonWebToken jwt, RSA rsa, HashAlg alg, RSASignaturePadding padding)
        {
            ArgumentNullException.ThrowIfNull(jwt);
            ArgumentNullException.ThrowIfNull(rsa);
            ArgumentNullException.ThrowIfNull(padding);

            //Init new rsa provider
            RSASignatureProvider sig = new(rsa, alg, padding);

            //Compute signature
            jwt.Sign(in sig, alg);
        }

        /// <summary>
        /// Use an RSA algorithm to sign the JWT message
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="alg">The algorithm used to sign the token</param>
        /// <param name="hashAlg">The hash algorithm to use</param>
        /// <param name="sigFormat">The DSA signature format</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static void Sign(this JsonWebToken jwt, ECDsa alg, HashAlg hashAlg, DSASignatureFormat sigFormat = DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
        {
            ArgumentNullException.ThrowIfNull(jwt);
            ArgumentNullException.ThrowIfNull(alg);

            //Init new ec provider
            ECDSASignatureProvider sig = new(alg, sigFormat);

            jwt.Sign(in sig, hashAlg);
        }

        /// <summary>
        /// Signs the JWT data using HMAC without allocating a <see cref="HashAlgorithm"/>
        /// instance.
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="alg">The algorithm used to sign</param>
        /// <param name="key">The key data</param>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static void Sign(this JsonWebToken jwt, ReadOnlySpan<byte> key, HashAlg alg)
        {
            //Stack hash output buffer, will be the size of the alg
            Span<byte> sigOut = stackalloc byte[alg.HashSize()];

            //Compute
            ERRNO count = ManagedHash.ComputeHmac(key, jwt.HeaderAndPayload, sigOut, alg);

            if (!count)
            {
                throw new InternalBufferTooSmallException("Failed to compute the hmac signature because the internal buffer was mis-sized");
            }

            //write
            jwt.WriteSignature(sigOut[..(int)count]);
        }
      
        /// <summary>
        /// Computes the signature of the current <see cref="JsonWebToken"/>
        /// using the generic <see cref="IJwtSignatureProvider"/> implementation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jwt"></param>
        /// <param name="provider">The <see cref="IJwtSignatureProvider"/> that will compute the signature of the message digest</param>
        /// <param name="hashAlg">The <see cref="HashAlg"/> algorithm used to compute the message digest</param>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        /// <exception cref="CryptographicException"></exception>
        public static void Sign<T>(this JsonWebToken jwt, in T provider, HashAlg hashAlg) where T : IJwtSignatureProvider
        {
            //Alloc heap buffer to store hash data (helps with memory locality)
            nint nearestPage = MemoryUtil.NearestPage(provider.RequiredBufferSize + (int)hashAlg);

            //Alloc buffer
            using UnsafeMemoryHandle<byte> handle = jwt.Heap.UnsafeAlloc<byte>((int)nearestPage, true);

            //Split buffers
            Span<byte> hashBuffer = handle.Span[..(int)hashAlg];
            Span<byte> output = handle.Span[(int)hashAlg..];
          
            //Compute hash
            ERRNO hashLen = ManagedHash.ComputeHash(jwt.HeaderAndPayload, hashBuffer, hashAlg);

            if (!hashLen)
            {
                throw new InternalBufferTooSmallException("Hash buffer was not properly computed");
            }

            //Compute signature
            ERRNO sigLen = provider.ComputeSignatureFromHash(hashBuffer[..(int)hashLen], output);

            if (!sigLen)
            {
                throw new CryptographicException("Failed to compute the JWT hash signature");
            }

            //Write signature to the jwt
            jwt.WriteSignature(output[..(int)sigLen]);
        }

        /// <summary>
        /// Verifies the current JWT body-segements against the parsed signature segment.
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="verificationAlg">
        /// The <see cref="HashAlgorithm"/> to use when calculating the hash of the JWT
        /// </param>
        /// <returns>
        /// True if the signature field of the current JWT matches the re-computed signature of the header and data-fields
        /// signature
        /// </returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static bool Verify(this JsonWebToken jwt, HashAlgorithm verificationAlg)
        {
            ArgumentNullException.ThrowIfNull(jwt);
            ArgumentNullException.ThrowIfNull(verificationAlg);
            
            //Calculate the size of the buffer to use for the current algorithm and make sure it will include the utf8 encoding
            int hashBufferSize = Base64.GetMaxEncodedToUtf8Length(verificationAlg.HashSize / 8);
            
            //Alloc buffer for signature output
            Span<byte> signatureBuffer = stackalloc byte[hashBufferSize];
            
            //Compute the hash of the current payload
            if (!verificationAlg.TryComputeHash(jwt.HeaderAndPayload, signatureBuffer, out int bytesWritten))
            {
                throw new InternalBufferTooSmallException("Failed to compute the hash of the JWT data");
            }

            //Do an in-place base64 conversion of the signature to base64url
            ERRNO encoded = VnEncoding.Base64UrlEncodeInPlace(signatureBuffer, bytesWritten, false);
          
            if (!encoded)
            {
                throw new InternalBufferTooSmallException("Failed to convert the signature buffer to its base64 because the buffer was too small");
            }        
            
            //Verify the signatures and return results
            return CryptographicOperations.FixedTimeEquals(jwt.SignatureData, signatureBuffer[..(int)encoded]);
        }

        /// <summary>
        /// Verifies the signature of the current <see cref="JsonWebToken"/> against the 
        /// generic <see cref="IJwtSignatureVerifier"/> verification method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jwt"></param>
        /// <param name="provider">The <see cref="IJwtSignatureVerifier"/> used to verify the message digest</param>
        /// <param name="alg">The <see cref="HashAlg"/> used to compute the message digest</param>
        /// <returns>True if the siganture matches the computed on, false otherwise</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static bool Verify<T>(this JsonWebToken jwt, ref readonly T provider, HashAlg alg) where T : IJwtSignatureVerifier
        {
            ArgumentNullException.ThrowIfNull(jwt);

            ReadOnlySpan<byte> signature = jwt.SignatureData;

            int sigBufSize = CalcPadding(signature.Length) + signature.Length;

            //Calc full buffer size
            nint bufferSize = MemoryUtil.NearestPage(sigBufSize + alg.HashSize());

            //Alloc buffer to decode data, as a full page, all buffers will be used from the block for better cache
            using UnsafeMemoryHandle<byte> buffer = jwt.Heap.UnsafeAlloc<byte>((int)bufferSize);

            //Split buffers for locality
            Span<byte> sigBuffer = buffer.Span[..sigBufSize];
            Span<byte> hashBuffer = buffer.Span[sigBufSize..];

            //Decode from urlsafe base64
            int decoded = DecodeUnpadded(signature, sigBuffer);

            //Shift sig buffer to end of signature bytes
            sigBuffer = sigBuffer[..decoded];

            //Compute digest of message
            ERRNO hashLen = ManagedHash.ComputeHash(jwt.HeaderAndPayload, hashBuffer, alg);

            if (!hashLen)
            {
                throw new InternalBufferTooSmallException("Hash output buffer was not properly sized");
            }

            //Shift hash buffer
            hashBuffer = hashBuffer[..(int)hashLen];

            //Verify signature
            return provider.Verify(hashBuffer, sigBuffer);
        }

        /// <summary>
        /// Verifies the current JWT body-segements against the parsed signature segment.
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="alg">
        /// The HMAC algorithm to use when calculating the hash of the JWT
        /// </param>
        /// <param name="key">The HMAC shared symetric key</param>
        /// <returns>
        /// True if the signature field of the current JWT matches the re-computed signature of the header and data-fields
        /// signature
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static bool Verify(this JsonWebToken jwt, ReadOnlySpan<byte> key, HashAlg alg)
        {
            ArgumentNullException.ThrowIfNull(jwt);

            //Get base64 buffer size for in-place conversion
            int bufferSize = Base64.GetMaxEncodedToUtf8Length(alg.HashSize());

            //Alloc buffer for signature output
            Span<byte> signatureBuffer = stackalloc byte[bufferSize];

            //Compute the hash of the current payload
            ERRNO count = ManagedHash.ComputeHmac(key, jwt.HeaderAndPayload, signatureBuffer, alg);
            if (!count)
            {
                throw new InternalBufferTooSmallException("Failed to compute the hash of the JWT data");
            }

            //Do an in-place base64 conversion of the signature to base64url
            ERRNO encoded = VnEncoding.Base64UrlEncodeInPlace(signatureBuffer, alg.HashSize(), false);
          
            if (!encoded)
            {
                throw new InternalBufferTooSmallException("Failed to convert the signature buffer to its base64 because the buffer was too small");
            }        
            
            //Verify the signatures and return results
            return CryptographicOperations.FixedTimeEquals(jwt.SignatureData, signatureBuffer[..(int)encoded]);
        }

        /// <summary>
        /// Verifies the signature of the data using the specified <see cref="RSA"/> and hash parameters
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="alg">The RSA algorithim to use while verifying the signature of the payload</param>
        /// <param name="hashAlg">The <see cref="HashAlg"/> used to compute the digest of the message data</param>
        /// <param name="padding">The RSA signature padding method</param>
        /// <returns>True if the singature has been verified, false otherwise</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static bool Verify(this JsonWebToken jwt, RSA alg, HashAlg hashAlg, RSASignaturePadding padding)
        {
            ArgumentNullException.ThrowIfNull(jwt);
            ArgumentNullException.ThrowIfNull(alg);
            ArgumentNullException.ThrowIfNull(padding);

            //Inint verifier
            RSASignatureVerifier verifier = new(alg, hashAlg, padding);

            return jwt.Verify(in verifier, hashAlg);
        }

        /// <summary>
        /// Verifies the signature of the data using the specified <see cref="RSA"/> and hash parameters
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="alg">The RSA algorithim to use while verifying the signature of the payload</param>
        /// <param name="hashAlg">The <see cref="HashAlgorithmName"/> used to hash the signature</param>
        /// <param name="signatureFormat">The signatured format used to verify the token, defaults to field concatination</param>
        /// <returns>True if the singature has been verified, false otherwise</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static bool Verify(this JsonWebToken jwt, ECDsa alg, HashAlg hashAlg, DSASignatureFormat signatureFormat = DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
        {
            ArgumentNullException.ThrowIfNull(jwt);
            ArgumentNullException.ThrowIfNull(alg);

            //Inint verifier
            ECDSASignatureVerifier verifier = new(alg, signatureFormat);

            return jwt.Verify(in verifier, hashAlg);
        }

        /*
         * Simple ecdsa and rsa providers
         */
        private readonly struct ECDSASignatureProvider(ECDsa SigAlg, DSASignatureFormat Format) : IJwtSignatureProvider
        {
            ///<inheritdoc/>
            public readonly int RequiredBufferSize { get; } = 512;

            ///<inheritdoc/>
            public readonly ERRNO ComputeSignatureFromHash(ReadOnlySpan<byte> hash, Span<byte> outputBuffer) 
                => SigAlg.TrySignHash(hash, outputBuffer, Format, out int written) ? written : ERRNO.E_FAIL;
        }

        internal readonly struct RSASignatureProvider(RSA SigAlg, HashAlg Slg, RSASignaturePadding Padding) : IJwtSignatureProvider
        {
            ///<inheritdoc/>
            public readonly int RequiredBufferSize { get; } = 1024;

            ///<inheritdoc/>
            public readonly ERRNO ComputeSignatureFromHash(ReadOnlySpan<byte> hash, Span<byte> outputBuffer) 
                => !SigAlg.TrySignHash(hash, outputBuffer, Slg.GetAlgName(), Padding, out int written) ? written : ERRNO.E_FAIL;
        }

        /*
         * ECDSA and rsa verifiers
         */
        internal readonly struct ECDSASignatureVerifier(ECDsa Alg, DSASignatureFormat Format) : IJwtSignatureVerifier
        {
            ///<inheritdoc/>
            public readonly bool Verify(ReadOnlySpan<byte> messageHash, ReadOnlySpan<byte> signature) 
                => Alg.VerifyHash(messageHash, signature, Format);
        }

        internal readonly struct RSASignatureVerifier(RSA Alg, HashAlg HashAlg, RSASignaturePadding Padding) : IJwtSignatureVerifier
        {
            ///<inheritdoc/>
            public readonly bool Verify(ReadOnlySpan<byte> messageHash, ReadOnlySpan<byte> signature) 
                => Alg.VerifyHash(messageHash, signature, HashAlg.GetAlgName(), Padding);
        }
    }
}
