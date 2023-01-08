/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: JsonWebToken.cs 
*
* JsonWebToken.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing.IdentityUtility
{
    /// <summary>
    /// Provides a dynamic JSON Web Token class that will store and 
    /// compute Base64Url encoded WebTokens
    /// </summary>
    public class JsonWebToken : VnDisposeable, IStringSerializeable, IDisposable
    {
        internal const byte SAEF_PERIOD = 0x2e;
        internal const byte PADDING_BYTES = 0x3d;

        /// <summary>
        /// Parses a JWT from a Base64URL encoded character buffer
        /// </summary>
        /// <param name="urlEncJwtString"></param>
        /// <param name="heap">An optional <see cref="IUnmangedHeap"/> instance to alloc buffers from</param>
        /// <returns>The parses <see cref="JsonWebToken"/></returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static JsonWebToken Parse(ReadOnlySpan<char> urlEncJwtString, IUnmangedHeap heap = null)
        {
            heap ??= Memory.Shared;
            //Calculate the decoded size of the characters to alloc a buffer
            int utf8Size = Encoding.UTF8.GetByteCount(urlEncJwtString);
            //Alloc bin buffer to store decode data
            using MemoryHandle<byte> binBuffer = heap.Alloc<byte>(utf8Size, true);
            //Decode to utf8
            utf8Size = Encoding.UTF8.GetBytes(urlEncJwtString, binBuffer);
            //Parse and return the jwt
            return ParseRaw(binBuffer.Span[..utf8Size], heap);
        }
        
        /// <summary>
        /// Parses a buffer of UTF8 bytes of url encoded base64 characters
        /// </summary>
        /// <param name="utf8JWTData">The JWT data buffer</param>
        /// <param name="heap">An optional <see cref="IUnmangedHeap"/> instance to alloc buffers from</param>
        /// <returns>The parsed <see cref="JsonWebToken"/></returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static JsonWebToken ParseRaw(ReadOnlySpan<byte> utf8JWTData, IUnmangedHeap heap = null)
        {
            if (utf8JWTData.IsEmpty)
            {
                throw new ArgumentException("JWT data may not be empty", nameof(utf8JWTData));
            }
            //Set default heap of non was specified
            heap ??= Memory.Shared;
            //Alloc the token and copy the supplied data to a new mem stream
            JsonWebToken jwt = new(heap, new (heap, utf8JWTData));
            try
            {
                ReadOnlySpan<byte> buffer = jwt.DataBuffer;
                //Search for the first period to indicate the end of the header section
                jwt.HeaderEnd = buffer.IndexOf(SAEF_PERIOD);
                //Make sure a '.' was found
                if (jwt.HeaderEnd < 0)
                {
                    throw new FormatException("The supplied data is not a valid Json Web Token, header end symbol could not be found");
                }
                //Shift buffer window
                buffer = buffer[jwt.PayloadStart..];
                //Search for next period to end the payload
                jwt.PayloadEnd = jwt.PayloadStart + buffer.LastIndexOf(SAEF_PERIOD);
                //Make sure a '.' was found
                if (jwt.PayloadEnd < 0)
                {
                    throw new FormatException("The supplied data is not a valid Json Web Token, payload end symbol could not be found");
                }
                //signature is set automatically
                //return the new token
                return jwt;
            }
            catch
            {
                jwt.Dispose();
                throw;
            }
        }


        /// <summary>
        /// The heap used to allocate buffers from
        /// </summary>
        public IUnmangedHeap Heap { get; }
        /// <summary>
        /// The size (in bytes) of the encoded data that makes 
        /// up the current JWT.
        /// </summary>
        public int ByteSize => (int)DataStream.Position;
        /// <summary>
        /// A buffer that represents the current state of the JWT buffer.
        /// Utf8Base64Url encoded data.
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public ReadOnlySpan<byte> DataBuffer => DataStream.AsSpan()[..ByteSize];

        
        private readonly VnMemoryStream DataStream;

        /// <summary>
        /// Creates a new <see cref="JsonWebToken"/> with the specified initial state
        /// </summary>
        /// <param name="heap">The heap used to alloc buffers</param>
        /// <param name="initialData">The initial data of the jwt</param>
        protected JsonWebToken(IUnmangedHeap heap, VnMemoryStream initialData)
        {
            Heap = heap;
            DataStream = initialData;
            initialData.Position = initialData.Length;
        }

        /// <summary>
        /// Creates a new empty JWT instance, with an optional heap to alloc
        /// buffers from. (<see cref="Memory.Shared"/> is used as default)
        /// </summary>
        /// <param name="heap">The <see cref="IUnmangedHeap"/> to alloc buffers from</param>
        public JsonWebToken(IUnmangedHeap heap = null)
        {
            Heap = heap ?? Memory.Shared;
            DataStream = new(Heap, 100, true);         
        }
        
        #region Header
        private int HeaderEnd;
        /// <summary>
        /// The Base64URL encoded UTF8 bytes of the header portion of the current JWT
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ReadOnlySpan<byte> HeaderData => DataBuffer[..HeaderEnd];

        /// <summary>
        /// Encodes and stores the specified header value to the begining of the 
        /// JWT. This method may only be called once, if the header has not already been supplied.
        /// </summary>
        /// <param name="header">The value of the JWT header parameter</param>
        /// <exception cref="OutOfMemoryException"></exception>
        public void WriteHeader(ReadOnlySpan<byte> header)
        {
            //reset the buffer
            DataStream.Position = 0;
            //Write the header data
            WriteValue(header);
            //The header end is the position of the stream since it was empty
            HeaderEnd = ByteSize;
        }
        
        #endregion

        #region Payload
        private int PayloadStart => HeaderEnd + 1;
        private int PayloadEnd;
        /// <summary>
        /// The Base64URL encoded UTF8 bytes of the payload portion of the current JWT
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ReadOnlySpan<byte> PayloadData => DataBuffer[PayloadStart..PayloadEnd];

        /// <summary>
        /// The Base64URL encoded UTF8 bytes of the header + '.' + payload portion of the current jwt
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ReadOnlySpan<byte> HeaderAndPayload => DataBuffer[..PayloadEnd];

        /// <summary>
        /// Encodes and stores the specified payload data and appends it to the current 
        /// JWT buffer. This method may only be called once, if the header has not already been supplied.
        /// </summary>
        /// <param name="payload">The value of the JWT payload section</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void WritePayload(ReadOnlySpan<byte> payload)
        {
            //Write leading period
            DataStream.WriteByte(SAEF_PERIOD);
            //Write payload
            WriteValue(payload);
            //Store final position
            PayloadEnd = ByteSize;
        }
        /// <summary>
        /// Encodes the specified value and writes it to the 
        /// internal buffer
        /// </summary>
        /// <param name="value">The data value to encode and buffer</param>
        /// <exception cref="OutOfMemoryException"></exception>
        protected void WriteValue(ReadOnlySpan<byte> value)
        {
            //Calculate the proper base64 buffer size
            int base64BufSize = Base64.GetMaxEncodedToUtf8Length(value.Length);
            //Alloc buffer
            using UnsafeMemoryHandle<byte> binBuffer = Heap.UnsafeAlloc<byte>(base64BufSize);
            //Slice off the begiing of the buffer for the base64 encoding
            if(Base64.EncodeToUtf8(value, binBuffer.Span, out _, out int written) != OperationStatus.Done)
            {
                throw new OutOfMemoryException();
            }
            //Base64 encoded
            Span<byte> base64Data = binBuffer.Span[..written].Trim(PADDING_BYTES);
            //Convert to rfc4648 urlsafe version
            VnEncoding.Base64ToUrlSafeInPlace(base64Data);
            //Write the endoded buffer to the stream
            DataStream.Write(base64Data);
        }
        #endregion

        #region Signature
        private int SignatureStart => PayloadEnd + 1;
        private int SignatureEnd => ByteSize;
        /// <summary>
        /// The Base64URL encoded UTF8 bytes of the signature portion of the current JWT
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ReadOnlySpan<byte> SignatureData => DataBuffer[SignatureStart..SignatureEnd];
      
        /// <summary>
        /// Signs the current JWT (header + payload) data
        /// and writes the signature the end of the current buffer,
        /// using the specified <see cref="HashAlgorithm"/>.
        /// </summary>
        /// <param name="signatureAlgorithm">An alternate <see cref="HashAlgorithm"/> instance to sign the JWT with</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual void Sign(HashAlgorithm signatureAlgorithm)
        {
            Check();
            _ = signatureAlgorithm ?? throw new ArgumentNullException(nameof(signatureAlgorithm));
            //Calculate the size of the buffer to use for the current algorithm
            int bufferSize = signatureAlgorithm.HashSize / 8;
            //Alloc buffer for signature output
            Span<byte> signatureBuffer = stackalloc byte[bufferSize];
            //Compute the hash of the current payload
            if(!signatureAlgorithm.TryComputeHash(DataBuffer, signatureBuffer, out int bytesWritten))
            {
                throw new OutOfMemoryException();
            }
            //Reset the stream position to the end of the payload
            DataStream.SetLength(PayloadEnd);
            //Write leading period
            DataStream.WriteByte(SAEF_PERIOD);
            //Write the signature data to the buffer
            WriteValue(signatureBuffer[..bytesWritten]);
        }
        /// <summary>
        /// Use an RSA algorithm to sign the JWT message
        /// </summary>
        /// <param name="rsa">The algorithm used to sign the token</param>
        /// <param name="hashAlg">The hash algorithm to use</param>
        /// <param name="padding">The signature padding to use</param>
        /// <param name="hashSize">The size (in bytes) of the hash output</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual void Sign(RSA rsa, in HashAlgorithmName hashAlg, RSASignaturePadding padding, int hashSize)
        {
            Check();
            _ = rsa ?? throw new ArgumentNullException(nameof(rsa));
            //Calculate the size of the buffer to use for the current algorithm
            using UnsafeMemoryHandle<byte> sigBuffer = Heap.UnsafeAlloc<byte>(hashSize);
            if(!rsa.TrySignData(HeaderAndPayload, sigBuffer.Span, hashAlg, padding, out int hashBytesWritten))
            {
                throw new OutOfMemoryException("Signature buffer is not large enough to store the hash");
            }
            //Reset the stream position to the end of the payload
            DataStream.SetLength(PayloadEnd);
            //Write leading period
            DataStream.WriteByte(SAEF_PERIOD);
            //Write the signature data to the buffer
            WriteValue(sigBuffer.Span[..hashBytesWritten]);
        }
        /// <summary>
        /// Use an RSA algorithm to sign the JWT message
        /// </summary>
        /// <param name="alg">The algorithm used to sign the token</param>
        /// <param name="hashAlg">The hash algorithm to use</param>
        /// <param name="hashSize">The size (in bytes) of the hash output</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual void Sign(ECDsa alg, in HashAlgorithmName hashAlg, int hashSize)
        {
            Check();
            _ = alg ?? throw new ArgumentNullException(nameof(alg));
            //Calculate the size of the buffer to use for the current algorithm
            using UnsafeMemoryHandle<byte> sigBuffer = Heap.UnsafeAlloc<byte>(hashSize);
            if (!alg.TrySignData(HeaderAndPayload, sigBuffer.Span, hashAlg, out int hashBytesWritten))
            {
                throw new OutOfMemoryException("Signature buffer is not large enough to store the hash");
            }
            //Reset the stream position to the end of the payload
            DataStream.SetLength(PayloadEnd);
            //Write leading period
            DataStream.WriteByte(SAEF_PERIOD);
            //Write the signature data to the buffer
            WriteValue(sigBuffer.Span[..hashBytesWritten]);
        }

        #endregion

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public virtual string Compile() => Encoding.UTF8.GetString(DataBuffer);

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public virtual void Compile(ref ForwardOnlyWriter<char> writer) => _ = Encoding.UTF8.GetChars(DataBuffer, ref writer);
        
        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public virtual ERRNO Compile(in Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }      

        /// <summary>
        /// Reset's the internal JWT buffer
        /// </summary>
        public virtual void Reset()
        {
            DataStream.Position = 0;
            //Reset segment indexes
            HeaderEnd = 0;
            PayloadEnd = 0;
        }
        
        /// <summary>
        /// Compiles the current JWT instance and converts it to a string
        /// </summary>
        /// <returns>A Base64Url enocded string of the JWT format</returns>
        public override string ToString() => Compile();

        ///<inheritdoc/>
        protected override void Free()
        {
            //Clear pointers, so buffer get operations just return empty instead of throwing
            Reset();
            DataStream.Dispose();
        }
        
    }
}
