/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: MCHashingStreamExtensions.cs 
*
* MCHashingStreamExtensions.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;


namespace VNLib.Hashing.Native.MonoCypher
{
    /// <summary>
    /// Provides extension methods for <see cref="IHashStream"/> instances
    /// </summary>
    public static unsafe class MCHashingStreamExtensions
    {
        /// <summary>
        /// Updates the hash of this stream with the specified message
        /// </summary>
        /// <param name="hashStream"></param>
        /// <param name="message">A pointer to the message memory sequence</param>
        /// <param name="mSize">The size of the sequence</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Update(this IHashStream hashStream, IntPtr message, uint mSize) => Update(hashStream, message.ToPointer(), mSize);

        /// <summary>
        /// Updates the hash of this stream with the specified message
        /// </summary>
        /// <param name="hashStream"></param>
        /// <param name="message">A pointer to the message memory sequence</param>
        /// <param name="mSize">The size of the sequence</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Update(this IHashStream hashStream, void* message, uint mSize) 
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            _ = hashStream ?? throw new ArgumentNullException(nameof(hashStream));

            //get ref from pointer
            ref byte mRef = ref Unsafe.AsRef<byte>(message);
            hashStream.Update(ref mRef, mSize);
        }

        /// <summary>
        /// Updates the hash of this stream with the specified message
        /// </summary>
        /// <param name="hashStream"></param>
        /// <param name="message">The message memory sequence</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Update(this IHashStream hashStream, ReadOnlySpan<byte> message)
        {
            _ = hashStream ?? throw new ArgumentNullException(nameof(hashStream));

            if(message.Length == 0)
            {
                return;
            }

            //Marshal reference to span
            ref byte mRef = ref MemoryMarshal.GetReference(message);
            hashStream.Update(ref mRef, (uint)message.Length);
        }

        /// <summary>
        /// Updates the hash of this stream with the specified message
        /// </summary>
        /// <param name="hashStream"></param>
        /// <param name="message">The message memory sequence</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Update(this IHashStream hashStream, ReadOnlyMemory<byte> message)
        {
            _ = hashStream ?? throw new ArgumentNullException(nameof(hashStream));
            if (message.Length == 0)
            {
                return;
            }

            //Pin memory block instead of span marshalling
            using MemoryHandle mHandle = message.Pin();

            hashStream.Update(mHandle.Pointer, (uint)message.Length);
        }

        /// <summary>
        /// Writes the internal hash state to the specified memory location. The hash size 
        /// must be at least the size of <see cref="IHashStream.HashSize"/>
        /// </summary>
        /// <param name="hashStream"></param>
        /// <param name="hashOut">A pointer to the memory sequence to write the hash to</param>
        /// <param name="hashSize">The size of the hash memory sequence, must be exactly <see cref="IHashStream.HashSize"/></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Flush(this IHashStream hashStream, IntPtr hashOut, byte hashSize) => Flush(hashStream, hashOut.ToPointer(), hashSize);

        /// <summary>
        /// Writes the internal hash state to the specified memory location. The hash size 
        /// must be at least the size of <see cref="IHashStream.HashSize"/>
        /// </summary>
        /// <param name="hashStream"></param>
        /// <param name="hashOut">A pointer to the memory sequence to write the hash to</param>
        /// <param name="hashSize">The size of the hash memory sequence, must be exactly <see cref="IHashStream.HashSize"/></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Flush(this IHashStream hashStream, void* hashOut, byte hashSize)
        {
            if (hashOut == null)
            {
                throw new ArgumentNullException(nameof(hashOut));
            }
            _ = hashStream ?? throw new ArgumentNullException(nameof(hashStream));

            //get ref from pointer
            ref byte hashOutRef = ref Unsafe.AsRef<byte>(hashOut);
            hashStream.Flush(ref hashOutRef, hashSize);
        }

        /// <summary>
        /// Writes the internal hash state to the specified memory location. The hash size 
        /// must be at least the size of <see cref="IHashStream.HashSize"/>
        /// </summary>
        /// <param name="hashStream"></param>
        /// <param name="hashOut">The memory sequence to write the hash to, must be exactly <see cref="IHashStream.HashSize"/></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Flush(this IHashStream hashStream, Span<byte> hashOut)
        {
            _ = hashStream ?? throw new ArgumentNullException(nameof(hashStream));

            if (hashOut.Length != hashStream.HashSize)
            {
                throw new ArgumentException("The hash output must be the configured hash size", nameof(hashOut));
            }

            //Marshal reference to span and flush
            ref byte hashOutRef = ref MemoryMarshal.GetReference(hashOut);
            hashStream.Flush(ref hashOutRef, (byte)hashOut.Length);
        }

        /// <summary>
        /// Writes the internal hash state to the specified memory location. The hash size 
        /// must be at least the size of <see cref="IHashStream.HashSize"/>
        /// </summary>
        /// <param name="hashStream"></param>
        /// <param name="hashOut">The memory sequence to write the hash to, must be exactly <see cref="IHashStream.HashSize"/></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Flush(this IHashStream hashStream, Memory<byte> hashOut)
        {
            _ = hashStream ?? throw new ArgumentNullException(nameof(hashStream));

            if (hashOut.Length != hashStream.HashSize)
            {
                throw new ArgumentException("The hash output must be the configured hash size", nameof(hashOut));
            }

            //Pin memory block instead of span marshalling
            using MemoryHandle hashOutHandle = hashOut.Pin();
            hashStream.Flush(hashOutHandle.Pointer, (byte)hashOut.Length);
        }

        /// <summary>
        /// Creates a new <see cref="IHmacStream"/> keyed MAC instance with the specified hash size.
        /// </summary>
        /// <remarks>
        /// A value greater than 32 is recommended for KDFs and a value greater than 16 is recommended for MACs.
        /// <para>
        /// <seealso href="https://monocypher.org/manual/blake2b"/>
        /// </para>
        /// </remarks>
        /// <param name="stream"></param>
        /// <param name="key">A pointer to the HMAC key buffer</param>
        /// <param name="keySize">The HMAC key must be between 1 and <see cref="IHmacStream.MaxKeySize"/> inclusive</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Initialize(this IHmacStream stream, IntPtr key, byte keySize) => Initialize(stream, (byte*)key.ToPointer(), keySize);

        /// <summary>
        /// Creates a new <see cref="IHmacStream"/> keyed MAC instance with the specified hash size.
        /// </summary>
        /// <remarks>
        /// A value greater than 32 is recommended for KDFs and a value greater than 16 is recommended for MACs.
        /// <para>
        /// <seealso href="https://monocypher.org/manual/blake2b"/>
        /// </para>
        /// </remarks>
        /// <param name="stream"></param>
        /// <param name="key">A pointer to the HMAC key buffer</param>
        /// <param name="keySize">The HMAC key must be between 1 and <see cref="IHmacStream.MaxKeySize"/> inclusive</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Initialize(this IHmacStream stream, byte* key, byte keySize)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (keySize == 0 || keySize > stream.MaxKeySize)
            {
                throw new ArgumentOutOfRangeException(nameof(keySize), $"The key size must be between 1 and {stream.MaxKeySize} inclusive");
            }

            //Get reference to key
            ref byte asRef = ref Unsafe.AsRef<byte>(key);
            stream.Initialize(ref asRef, keySize);
        }

        /// <summary>
        /// Creates a new <see cref="IHmacStream"/> keyed MAC instance with the specified hash size.
        /// </summary>
        /// <remarks>
        /// A value greater than 32 is recommended for KDFs and a value greater than 16 is recommended for MACs.
        /// <para>
        /// <seealso href="https://monocypher.org/manual/blake2b"/>
        /// </para>
        /// </remarks>
        /// <param name="stream"></param>
        /// <param name="key">The HMAC key buffer</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Initialize(this IHmacStream stream, ReadOnlySpan<byte> key)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));

            if (key.Length > stream.MaxKeySize)
            {
                throw new ArgumentOutOfRangeException(nameof(key), $"The hash size must be less than or equal to {stream.MaxKeySize}");
            }

            //Get span ref and call interface method
            ref byte asRef = ref MemoryMarshal.GetReference(key);
            stream.Initialize(ref asRef, (byte)key.Length);
        }

        /// <summary>
        /// Creates a new <see cref="IHmacStream"/> keyed MAC instance with the specified hash size.
        /// </summary>
        /// <remarks>
        /// A value greater than 32 is recommended for KDFs and a value greater than 16 is recommended for MACs.
        /// <para>
        /// <seealso href="https://monocypher.org/manual/blake2b"/>
        /// </para>
        /// </remarks>
        /// <param name="stream"></param>
        /// <param name="key">The HMAC key buffer</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Initialize(this IHmacStream stream, ReadOnlyMemory<byte> key)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));

            if (key.Length > stream.MaxKeySize)
            {
                throw new ArgumentOutOfRangeException(nameof(key), $"The hash size must be less than or equal to {stream.MaxKeySize}");
            }

            //If key is default, then h.Pointer will be null, which is handled
            using MemoryHandle h = key.Pin();

            //Get address of ref
            Initialize(stream, (byte*)h.Pointer, (byte)key.Length);
        }
    }
}