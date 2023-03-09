/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: PasswordHashing.cs 
*
* PasswordHashing.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Security.Cryptography;

using VNLib.Hashing;
using VNLib.Utils;
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Essentials.Accounts
{

    /// <summary>
    /// Provides a structured password hashing system implementing the <seealso cref="VnArgon2"/> library
    /// with fixed time comparison
    /// </summary>
    public sealed class PasswordHashing : IPasswordHashingProvider
    {
        private const int STACK_MAX_BUFF_SIZE = 64;

        private readonly ISecretProvider _secret;

        private readonly uint TimeCost;
        private readonly uint MemoryCost;
        private readonly uint HashLen;
        private readonly int SaltLen;
        private readonly uint Parallelism;

        /// <summary>
        /// Initalizes the <see cref="PasswordHashing"/> class
        /// </summary>
        /// <param name="secret">The password secret provider</param>
        /// <param name="saltLen">A positive integer for the size of the random salt used during the hashing proccess</param>
        /// <param name="timeCost">The Argon2 time cost parameter</param>
        /// <param name="memoryCost">The Argon2 memory cost parameter</param>
        /// <param name="hashLen">The size of the hash to produce during hashing operations</param>
        /// <param name="parallism">
        /// The Argon2 parallelism parameter (the number of threads to use for hasing) 
        /// (default = 0 - defaults to the number of logical processors)
        /// </param>
        /// <exception cref="ArgumentNullException"></exception> 
        public PasswordHashing(ISecretProvider secret, int saltLen = 32, uint timeCost = 4, uint memoryCost = UInt16.MaxValue, uint parallism = 0, uint hashLen = 128)
        {
            //Store getter
            _secret = secret ?? throw new ArgumentNullException(nameof(secret));
            
            //Store parameters
            HashLen = hashLen;
            //Store maginitude as a unit
            MemoryCost = memoryCost;
            TimeCost = timeCost;
            SaltLen = saltLen;
            Parallelism = parallism < 1 ? (uint)Environment.ProcessorCount : parallism;
        }
        
        
        ///<inheritdoc/>
        ///<exception cref="VnArgon2Exception"></exception>
        ///<exception cref="VnArgon2PasswordFormatException"></exception>
        public bool Verify(ReadOnlySpan<char> passHash, ReadOnlySpan<char> password)
        {
            if(passHash.IsEmpty || password.IsEmpty)
            {
                return false;
            }

            if(_secret.BufferSize < STACK_MAX_BUFF_SIZE)
            {
                //Alloc stack buffer
                Span<byte> secretBuffer = stackalloc byte[STACK_MAX_BUFF_SIZE];

                return VerifyInternal(passHash, password, secretBuffer);
            }
            else
            {
                //Alloc heap buffer
                using UnsafeMemoryHandle<byte> secretBuffer = MemoryUtil.UnsafeAlloc<byte>(_secret.BufferSize, true);

                return VerifyInternal(passHash, password, secretBuffer);
            }
        }

        private bool VerifyInternal(ReadOnlySpan<char> passHash, ReadOnlySpan<char> password, Span<byte> secretBuffer)
        {
            try
            {
                //Get the secret from the callback
                ERRNO count = _secret.GetSecret(secretBuffer);
                //Verify
                return VnArgon2.Verify2id(password, passHash, secretBuffer[..(int)count]);
            }
            finally
            {
                //Erase secret buffer
                MemoryUtil.InitializeBlock(secretBuffer);
            }
        }

        /// <summary>
        /// Verifies a password against its hash. Partially exposes the Argon2 api.
        /// </summary>
        /// <param name="hash">Previously hashed password</param>
        /// <param name="salt">The salt used to hash the original password</param>
        /// <param name="password">The password to hash and compare against </param>
        /// <returns>true if bytes derrived from password match the hash, false otherwise</returns>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <remarks>Uses fixed time comparison from <see cref="CryptographicOperations"/> class</remarks>
        public bool Verify(ReadOnlySpan<byte> hash, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> password)
        {
            //Alloc a buffer with the same size as the hash
            using UnsafeMemoryHandle<byte> hashBuf = MemoryUtil.UnsafeAlloc<byte>(hash.Length, true);
            //Hash the password with the current config
            Hash(password, salt, hashBuf.Span);
            //Compare the hashed password to the specified hash and return results
            return CryptographicOperations.FixedTimeEquals(hash, hashBuf.Span);
        }
        
        /// <inheritdoc/>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <returns>A <see cref="PrivateString"/> of the hashed and encoded password</returns>
        public PrivateString Hash(ReadOnlySpan<char> password)
        {
            //Alloc shared buffer for the salt and secret buffer
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(SaltLen + _secret.BufferSize, true);
            try
            {
                //Split buffers
                Span<byte> saltBuf = buffer.Span[..SaltLen];
                Span<byte> secretBuf = buffer.Span[SaltLen..];
                
                //Fill the buffer with random bytes
                RandomHash.GetRandomBytes(saltBuf);
                
                //recover the secret
                ERRNO count = _secret.GetSecret(secretBuf);

                //Hashes a password, with the current parameters
                return (PrivateString)VnArgon2.Hash2id(password, saltBuf, secretBuf[..(int)count], TimeCost, MemoryCost, Parallelism, HashLen);
            }
            finally
            {
                MemoryUtil.InitializeBlock(buffer.Span);
            }
        }

        /// <inheritdoc/>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <returns>A <see cref="PrivateString"/> of the hashed and encoded password</returns>
        public PrivateString Hash(ReadOnlySpan<byte> password)
        {
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(SaltLen + _secret.BufferSize, true);
            try
            {
                //Split buffers
                Span<byte> saltBuf = buffer.Span[..SaltLen];
                Span<byte> secretBuf = buffer.Span[SaltLen..];

                //Fill the buffer with random bytes
                RandomHash.GetRandomBytes(saltBuf);

                //recover the secret
                ERRNO count = _secret.GetSecret(secretBuf);

                //Hashes a password, with the current parameters
                return (PrivateString)VnArgon2.Hash2id(password, saltBuf, secretBuf[..(int)count], TimeCost, MemoryCost, Parallelism, HashLen);
            }
            finally
            {
                MemoryUtil.InitializeBlock(buffer.Span);
            }
        }
        
        /// <summary>
        /// Partially exposes the Argon2 api. Hashes the specified password, with the initialized pepper.
        /// Writes the raw hash output to the specified buffer
        /// </summary>
        /// <param name="password">Password to be hashed</param>
        /// <param name="salt">Salt to hash the password with</param>
        /// <param name="hashOutput">The output buffer to store the hashed password to. The exact length of this buffer is the hash size</param>
        /// <exception cref="VnArgon2Exception"></exception>
        public void Hash(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, Span<byte> hashOutput)
        {
            //alloc secret buffer
            using UnsafeMemoryHandle<byte> secretBuffer = MemoryUtil.UnsafeAlloc<byte>(_secret.BufferSize, true);
            try
            {
                //Get the secret from the callback
                ERRNO count = _secret.GetSecret(secretBuffer.Span);
                //Hashes a password, with the current parameters
                VnArgon2.Hash2id(password, salt, secretBuffer.Span[..(int)count], hashOutput, TimeCost, MemoryCost, Parallelism);
            }
            finally
            {
                //Erase secret buffer
                MemoryUtil.InitializeBlock(secretBuffer.Span);
            }
        }

        /// <summary>
        /// NOT SUPPORTED! Use <see cref="Verify(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
        /// instead to specify the salt that was used to encypt the original password
        /// </summary>
        /// <param name="passHash"></param>
        /// <param name="password"></param>
        /// <exception cref="NotSupportedException"></exception>
        public bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password)
        {
            throw new NotSupportedException();
        }

        ///<inheritdoc/>
        ///<exception cref="VnArgon2Exception"></exception>
        public ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput)
        {
            //Calc the min buffer size
            int minBufferSize = SaltLen + _secret.BufferSize + (int)HashLen;

            //Alloc heap buffer 
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAllocNearestPage<byte>(minBufferSize, true);
            try
            {
                //Segment the buffer
                HashBufferSegments segments = new(buffer.Span, _secret.BufferSize, SaltLen, (int)HashLen);

                //Fill the buffer with random bytes
                RandomHash.GetRandomBytes(segments.SaltBuffer);

                //recover the secret
                ERRNO count = _secret.GetSecret(segments.SecretBuffer);

                //Hash the password in binary and write the secret to the binary buffer
                VnArgon2.Hash2id(password, segments.SaltBuffer, segments.SecretBuffer[..(int)count], segments.HashBuffer, TimeCost, MemoryCost, Parallelism);

                //Hash size is the desired hash size
                return new((int)HashLen);
            }
            finally
            {
                MemoryUtil.InitializeBlock(buffer.Span);
            }
        }

        private readonly ref struct HashBufferSegments
        {
            public readonly Span<byte> SaltBuffer;

            public readonly Span<byte> SecretBuffer;

            public readonly Span<byte> HashBuffer;

            public HashBufferSegments(Span<byte> buffer, int secretSize, int saltSize, int hashSize)
            {
                //Salt buffer is begining segment
                SaltBuffer = buffer[..saltSize];

                //Shift to end of salt buffer
                buffer = buffer[saltSize..];

                //Store secret buffer
                SecretBuffer = buffer[..secretSize];

                //Shift to end of secret buffer
                buffer = buffer[secretSize..];

                //Store remaining size as hash buffer
                HashBuffer = buffer[..hashSize]; 
            }
        }
    }
}