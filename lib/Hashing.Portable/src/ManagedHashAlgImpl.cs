/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: ManagedHashAlgImpl.cs 
*
* ManagedHashAlgImpl.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Diagnostics;
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Hashing.Native.MonoCypher;

namespace VNLib.Hashing
{
    public static partial class ManagedHash
    {
        private interface IHashAlgorithm
        {
            int HashSize { get; }

            byte[] ComputeHash(ReadOnlySpan<byte> data);

            bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count);

            byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data);

            bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count);
        }

        private readonly struct Sha1 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA1;
            ///<inheritdoc/>
            public readonly byte[] ComputeHash(ReadOnlySpan<byte> data) => SHA1.HashData(data);
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA1.TryHashData(data, output, out count);

            ///<inheritdoc/>
            public readonly byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => HMACSHA1.HashData(key, data);
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA1.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha256 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA256;

            ///<inheritdoc/>
            public readonly byte[] ComputeHash(ReadOnlySpan<byte> data) => SHA256.HashData(data);

            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA256.TryHashData(data, output, out count);

            ///<inheritdoc/>
            public readonly byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => HMACSHA256.HashData(key, data);

            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA256.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha384 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA384;
            ///<inheritdoc/>
            public readonly byte[] ComputeHash(ReadOnlySpan<byte> data) => SHA384.HashData(data);
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA384.TryHashData(data, output, out count);

            ///<inheritdoc/>
            public readonly byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => HMACSHA384.HashData(key, data);

            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA384.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha512 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA512;
            ///<inheritdoc/>
            public readonly byte[] ComputeHash(ReadOnlySpan<byte> data) => SHA512.HashData(data);
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA512.TryHashData(data, output, out count);

            ///<inheritdoc/>
            public readonly byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => HMACSHA512.HashData(key, data);
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA512.TryHashData(key, data, output, out count);
        }

        private readonly struct Md5 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.MD5;
            ///<inheritdoc/>
            public readonly byte[] ComputeHash(ReadOnlySpan<byte> data) => MD5.HashData(data);
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => MD5.TryHashData(data, output, out count);

            ///<inheritdoc/>
            public readonly byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => HMACMD5.HashData(key, data);
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACMD5.TryHashData(key, data, output, out count);
        }

        private readonly struct Blake2b : IHashAlgorithm
        {
            const byte DefaultBlake2HashSize = 64;

            internal static int MaxHashSize => MCBlake2Module.MaxHashSize;
            internal static int MaxKeySize => MCBlake2Module.MaxKeySize;

            ///<inheritdoc/>
            public readonly int HashSize => DefaultBlake2HashSize;

            ///<inheritdoc/>
            public readonly byte[] ComputeHash(ReadOnlySpan<byte> data)
            {
                //Stack buffer for output hash
                byte[] output = new byte[DefaultBlake2HashSize];

                if (!TryComputeHash(data, output, out int count))
                {
                    throw new ArgumentException("Failed to compute Blake2 hash of desired data");
                }

                //Count must be exact same (sanity check)
                Debug.Assert(count == DefaultBlake2HashSize);

                //Return the hash as a new array
                return output;
            }

            ///<inheritdoc/>
            public readonly byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
            {
                //Alloc output buffer
                byte[] output = new byte[DefaultBlake2HashSize];

                if (!TryComputeHmac(key, data, output, out int count))
                {
                    throw new ArgumentException("Failed to compute Blake2 hash of desired data");
                }

                //Count must be exact same (sanity check)
                Debug.Assert(count == DefaultBlake2HashSize);

                //Return the hash as a new array
                return output;

            }

            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count)
            {
                if (output.Length > MCBlake2Module.MaxHashSize)
                {
                    count = 0;
                    return false;
                }

                //Compute one-shot hash
                ERRNO result = MonoCypherLibrary.Shared.Blake2ComputeHash(data, output);

                if(result < output.Length)
                {
                    count = 0;
                    return false;
                }

                count = output.Length;
                return true;
            }

            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count)
            {
                count = 0;

                if (output.Length > MCBlake2Module.MaxHashSize)
                {
                    return false;
                }

                //Test key size
                if (key.Length > MCBlake2Module.MaxKeySize)
                {
                    return false;
                }

                //Compute one-shot hash
                ERRNO result = MonoCypherLibrary.Shared.Blake2ComputeHmac(key, data, output);

                if (result < output.Length)
                {
                    count = 0;
                    return false;
                }

                count = output.Length;
                return true;
            }
        }
    }
}
