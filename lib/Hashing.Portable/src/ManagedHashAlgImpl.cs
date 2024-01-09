/*
* Copyright (c) 2024 Vaughn Nugent
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

            bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count);

            bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count);
        }

        private readonly struct Sha1 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA1;
          
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA1.TryHashData(data, output, out count);
        
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA1.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha256 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA256;

            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA256.TryHashData(data, output, out count);

            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA256.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha384 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA384;
          
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA384.TryHashData(data, output, out count);

            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA384.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha512 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA512;
           
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA512.TryHashData(data, output, out count);
           
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA512.TryHashData(key, data, output, out count);
        }

        private readonly struct Md5 : IHashAlgorithm
        {
            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.MD5;
          
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => MD5.TryHashData(data, output, out count);
           
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACMD5.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha3_256 : IHashAlgorithm
        {
            public static bool IsSupported => SHA3_256.IsSupported;

            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA256;
           
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA3_256.TryHashData(data, output, out count);
          
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA3_256.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha3_384 : IHashAlgorithm
        {
            public static bool IsSupported => SHA3_384.IsSupported;

            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA384;
           
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA3_384.TryHashData(data, output, out count);
          
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA3_384.TryHashData(key, data, output, out count);
        }

        private readonly struct Sha3_512 : IHashAlgorithm
        {
            public static bool IsSupported => SHA3_512.IsSupported;

            ///<inheritdoc/>
            public readonly int HashSize => (int)HashAlg.SHA512;
          
            ///<inheritdoc/>
            public readonly bool TryComputeHash(ReadOnlySpan<byte> data, Span<byte> output, out int count) => SHA3_512.TryHashData(data, output, out count);
           
            ///<inheritdoc/>
            public readonly bool TryComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output, out int count) => HMACSHA3_512.TryHashData(key, data, output, out count);
        }

        private readonly struct Blake2b : IHashAlgorithm
        {
            const byte DefaultBlake2HashSize = 64;

            internal static int MaxHashSize => MCBlake2Module.MaxHashSize;
            internal static int MaxKeySize => MCBlake2Module.MaxKeySize;

            ///<inheritdoc/>
            public readonly int HashSize => DefaultBlake2HashSize;

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
