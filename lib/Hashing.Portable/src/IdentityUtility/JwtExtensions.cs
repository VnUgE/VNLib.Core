/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Buffers;
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
            _ = jwt ?? throw new ArgumentNullException(nameof(jwt));
            _ = verificationAlg ?? throw new ArgumentNullException(nameof(verificationAlg));
            
            //Calculate the size of the buffer to use for the current algorithm and make sure it will include the utf8 encoding
            int hashBufferSize = Base64.GetMaxEncodedToUtf8Length(verificationAlg.HashSize / 8);
            
            //Alloc buffer for signature output
            Span<byte> signatureBuffer = stackalloc byte[hashBufferSize];
            
            //Compute the hash of the current payload
            if (!verificationAlg.TryComputeHash(jwt.HeaderAndPayload, signatureBuffer, out int bytesWritten))
            {
                throw new InternalBufferTooSmallException("Failed to compute the hash of the JWT data");
            }
            
            //Do an in-place base64 conversion of the signature to base64
            if (Base64.EncodeToUtf8InPlace(signatureBuffer, bytesWritten, out int base64BytesWritten) != OperationStatus.Done)
            {
                throw new InternalBufferTooSmallException("Failed to convert the signature buffer to its base64 because the buffer was too small");
            }
            
            //Trim padding
            Span<byte> base64 = signatureBuffer[..base64BytesWritten].Trim(JsonWebToken.PADDING_BYTES);

            //Urlencode
            VnEncoding.Base64ToUrlSafeInPlace(base64);
            
            //Verify the signatures and return results
            return CryptographicOperations.FixedTimeEquals(jwt.SignatureData, base64);
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
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static bool Verify(this JsonWebToken jwt, ReadOnlySpan<byte> key, HashAlg alg)
        {
            _ = jwt ?? throw new ArgumentNullException(nameof(jwt));

            //Get base64 buffer size for in-place conversion
            int bufferSize = Base64.GetMaxEncodedToUtf8Length((int)alg);

            //Alloc buffer for signature output
            Span<byte> signatureBuffer = stackalloc byte[bufferSize];

            //Compute the hash of the current payload
            ERRNO count = ManagedHash.ComputeHmac(key, jwt.HeaderAndPayload, signatureBuffer, alg);
            if (!count)
            {
                throw new InternalBufferTooSmallException("Failed to compute the hash of the JWT data");
            }

            //Do an in-place base64 conversion of the signature to base64
            if (Base64.EncodeToUtf8InPlace(signatureBuffer, count, out int base64BytesWritten) != OperationStatus.Done)
            {
                throw new InternalBufferTooSmallException("Failed to convert the signature buffer to its base64 because the buffer was too small");
            }

            //Trim padding
            Span<byte> base64 = signatureBuffer[..base64BytesWritten].Trim(JsonWebToken.PADDING_BYTES);

            //Urlencode
            VnEncoding.Base64ToUrlSafeInPlace(base64);

            //Verify the signatures and return results
            return CryptographicOperations.FixedTimeEquals(jwt.SignatureData, base64);
        }

        /// <summary>
        /// Verifies the signature of the data using the specified <see cref="RSA"/> and hash parameters
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="alg">The RSA algorithim to use while verifying the signature of the payload</param>
        /// <param name="hashAlg">The <see cref="HashAlgorithmName"/> used to hash the signature</param>
        /// <param name="padding">The RSA signature padding method</param>
        /// <returns>True if the singature has been verified, false otherwise</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static bool Verify(this JsonWebToken jwt, RSA alg, HashAlgorithmName hashAlg, RSASignaturePadding padding)
        {
            _ = jwt ?? throw new ArgumentNullException(nameof(jwt));
            _ = alg ?? throw new ArgumentNullException(nameof(alg));
            //Decode the signature
            ReadOnlySpan<byte> signature = jwt.SignatureData;
            int paddBytes = CalcPadding(signature.Length);
            //Alloc buffer to decode data
            using UnsafeMemoryHandle<byte> buffer = jwt.Heap.UnsafeAlloc<byte>(signature.Length + paddBytes);
            //Decode from urlsafe base64
            int decoded = DecodeUnpadded(signature, buffer.Span);
            //Verify signature
            return alg.VerifyData(jwt.HeaderAndPayload, buffer.Span[..decoded], hashAlg, padding);
        }
        /// <summary>
        /// Verifies the signature of the data using the specified <see cref="RSA"/> and hash parameters
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="alg">The RSA algorithim to use while verifying the signature of the payload</param>
        /// <param name="hashAlg">The <see cref="HashAlgorithmName"/> used to hash the signature</param>
        /// <returns>True if the singature has been verified, false otherwise</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static bool Verify(this JsonWebToken jwt, ECDsa alg, HashAlgorithmName hashAlg)
        {
            _ = alg ?? throw new ArgumentNullException(nameof(alg));
            //Decode the signature
            ReadOnlySpan<byte> signature = jwt.SignatureData;
            int paddBytes = CalcPadding(signature.Length);
            //Alloc buffer to decode data
            using UnsafeMemoryHandle<byte> buffer = jwt.Heap.UnsafeAlloc<byte>(signature.Length + paddBytes);
            //Decode from urlsafe base64
            int decoded = DecodeUnpadded(signature, buffer.Span);
            //Verify signature
            return alg.VerifyData(jwt.HeaderAndPayload, buffer.Span[..decoded], hashAlg);
        }

        /// <summary>
        /// Initializes a new <see cref="JwtPayload"/> object for writing claims to the 
        /// current tokens payload segment
        /// </summary>
        /// <param name="jwt"></param>
        /// <param name="initCapacity">The inital cliam capacity</param>
        /// <returns>The fluent chainable stucture</returns>
        public static JwtPayload InitPayloadClaim(this JsonWebToken jwt, int initCapacity = 0) => new (jwt, initCapacity);
    }
}
