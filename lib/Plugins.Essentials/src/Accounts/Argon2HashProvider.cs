/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: Argon2HashProvider.cs 
*
* Argon2HashProvider.cs is part of VNLib.Plugins.Essentials which is part 
* of the larger VNLib collection of libraries and utilities.
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
using System.Diagnostics;
using System.Security.Cryptography;

using VNLib.Hashing;
using VNLib.Utils;
using VNLib.Utils.Memory;

/*
 * Some stuff to note
 * 
 * Functions have explicit parameters to avoid accidental buffer mixup
 * when calling nested/overload functions. Please keep it that way for now
 * I really want to avoid a whoopsie in password hasing.
 */


namespace VNLib.Plugins.Essentials.Accounts
{

    /// <summary>
    /// Provides a structured password hashing system implementing the <seealso cref="VnArgon2"/> library
    /// with fixed time comparison
    /// </summary>
    public sealed class Argon2HashProvider : IPasswordHashingProvider
    {
        private readonly ISecretProvider? _secret;
        private readonly IArgon2Library _argon2;
        private readonly Argon2ConfigParams _config;
        
        internal Argon2HashProvider(IArgon2Library library, ISecretProvider? secret, in Argon2ConfigParams setup)
        {
            //Store getter
            _argon2 = library ?? throw new ArgumentNullException(nameof(library));
            _secret = secret;
            _config = setup;
        }

        /// <summary>
        /// Creates a new <see cref="Argon2HashProvider"/> instance using the specified library.
        /// </summary>
        /// <param name="library">The library instance to use</param>
        /// <param name="secret">The password secret provider</param>
        /// <param name="setup">The configuration setup arguments</param>
        /// <returns>The instance of the library to use</returns>
        public static Argon2HashProvider Create(IArgon2Library library, ISecretProvider? secret, in Argon2ConfigParams setup) 
            => new (library, secret, in setup);

        /// <summary>
        /// Creates a new <see cref="Argon2HashProvider"/> instance using the default 
        /// <see cref="VnArgon2"/> library.
        /// </summary>
        /// <param name="secret">The password secret provider</param>
        /// <param name="setup">The configuration setup arguments</param>
        /// <returns>The instance of the library to use</returns>
        /// <exception cref="DllNotFoundException"></exception>
        public static Argon2HashProvider Create(ISecretProvider? secret, in Argon2ConfigParams setup) 
            => Create(VnArgon2.GetOrLoadSharedLib(), secret, in setup);

        /// <summary>
        /// Creates a new <see cref="Argon2HashProvider"/> instance using the specified library.
        /// </summary>
        /// <param name="library">The library instance to use</param>
        /// <param name="setup">The configuration setup arguments</param>
        /// <returns>The instance of the library to use</returns>
        public static Argon2HashProvider Create(IArgon2Library library, in Argon2ConfigParams setup)
            => Create(library, secret: null, in setup);

        /// <summary>
        /// Creates a new <see cref="Argon2HashProvider"/> instance using the default 
        /// <see cref="VnArgon2"/> library.
        /// </summary>
        /// <param name="setup">The configuration setup arguments</param>
        /// <returns>The instance of the library to use</returns>
        /// <exception cref="DllNotFoundException"></exception>
        public static Argon2HashProvider Create(in Argon2ConfigParams setup)
            => Create(secret: null, in setup);

        private Argon2CostParams GetCostParams()
        {
            return new Argon2CostParams
            {
                MemoryCost      = _config.MemoryCost,
                TimeCost        = _config.TimeCost,
                Parallelism     = _config.Parallelism
            };
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

            if (_secret is null)
            {
                //Invoke without a secret buffer
                return _argon2.Verify2id(
                    rawPass: password,
                    hash: passHash,
                    secret: default
                );
            }
            else
            {
                //Alloc a heap buffer
                using UnsafeMemoryHandle<byte> secretBuffer = AllocSecretBuffer();

                ERRNO secretSize = _secret.GetSecret(secretBuffer.Span);

                return _argon2.Verify2id(
                    rawPass: password,
                    hash: passHash,
                    secret: secretBuffer.AsSpan(0, secretSize)
                );
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
            using UnsafeMemoryHandle<byte> hashBuf = MemoryUtil.UnsafeAlloc<byte>(_config.BufferHeap, hash.Length, true);

            Hash(password, salt, hashBuf.Span);

            //Compare the hashed password to the specified hash and return results
            return CryptographicOperations.FixedTimeEquals(hash, hashBuf.Span);
        }
        
        /// <inheritdoc/>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <returns>A <see cref="PrivateString"/> of the hashed and encoded password</returns>
        public PrivateString Hash(ReadOnlySpan<char> password)
        {
            int secBufferSize = _secret is null ? 0 : _secret.BufferSize;

            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(_config.SaltLen + secBufferSize, true);

            //Split buffers
            Span<byte> saltBuf = buffer.Span[.._config.SaltLen];

            /*
             * Salt is just crypographically secure random 
             * data.
             */
            RandomHash.GetRandomBytes(saltBuf);

            try
            {
                Argon2CostParams costParams = GetCostParams();

                Span<byte> secretBuf = buffer.Span[_config.SaltLen..];

                Debug.Assert(secretBuf.Length >= secBufferSize);

                if (_secret is not null)
                {
                    //recover the secret
                    ERRNO count = _secret.GetSecret(secretBuf);

                    secretBuf = secretBuf[..(int)count];
                }
                else
                {
                    //Set the secret buffer to empty when no seceret is provided
                    secretBuf = default;
                }

                return (PrivateString)_argon2.Hash2id(
                    password: password,
                    salt: saltBuf,
                    secret: secretBuf,
                    costParams: in costParams,
                    hashLen: _config.HashLen
                );
            }
            finally
            {
                MemoryUtil.InitializeBlock(ref buffer.GetReference(), buffer.IntLength);
            }
        }

        /// <inheritdoc/>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <returns>A <see cref="PrivateString"/> of the hashed and encoded password</returns>
        public PrivateString Hash(ReadOnlySpan<byte> password)
        {
            int secBufferSize = _secret is null ? 0 : _secret.BufferSize;

            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(_config.SaltLen + secBufferSize, true);

            //Split buffers
            Span<byte> saltBuf = buffer.AsSpan(0, _config.SaltLen);

            /*
             * Salt is just crypographically secure random 
             * data.
             */
            RandomHash.GetRandomBytes(saltBuf);

            try
            {
                Argon2CostParams costParams = GetCostParams();

                Span<byte> secretBuf = buffer.AsSpan(_config.SaltLen);

                Debug.Assert(secretBuf.Length >= secBufferSize);

                if (_secret is not null)
                {
                    //recover the secret
                    ERRNO count = _secret.GetSecret(secretBuf);

                    secretBuf = secretBuf[..(int)count];
                }
                else
                {
                    //Set the secret buffer to empty when no seceret is provided
                    secretBuf = default;
                }
                
                return (PrivateString)_argon2.Hash2id(
                    password: password,
                    salt: saltBuf,
                    secret: secretBuf,
                    costParams: in costParams,
                    hashLen: _config.HashLen
                );
            }
            finally
            {
                MemoryUtil.InitializeBlock(ref buffer.GetReference(), buffer.IntLength);
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
            Argon2CostParams costParams = GetCostParams();

            if (_secret is null)
            {
                _argon2.Hash2id(
                    password: password,
                    salt: salt,
                    secret: default,    //No secret buffer
                    rawHashOutput: hashOutput,
                    costParams: in costParams
                );
            }
            else
            {
                using UnsafeMemoryHandle<byte> secretBuffer = AllocSecretBuffer();

                try
                {
                    ERRNO secretSize = _secret.GetSecret(secretBuffer.Span);

                    _argon2.Hash2id(
                        password: password,
                        salt: salt,
                        secret: secretBuffer.AsSpan(0, secretSize),
                        rawHashOutput: hashOutput,
                        costParams: in costParams
                    );
                }
                finally
                {
                    //Erase secret buffer
                    MemoryUtil.InitializeBlock(
                        ref secretBuffer.GetReference(),
                        secretBuffer.IntLength
                    );
                }
            }
        }

        ///<inheritdoc/>
        ///<exception cref="VnArgon2Exception"></exception>
        public ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput)
        {
            //Calc the min buffer size
            int minBufferSize = _config.SaltLen + (int)_config.HashLen;

            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAllocNearestPage<byte>(_config.BufferHeap, minBufferSize, true);

            Span<byte> saltBuffer = buffer.AsSpan(0, _config.SaltLen);
            Span<byte> hashBuffer = buffer.AsSpan(_config.SaltLen, (int)_config.HashLen);

            /*
             * Salt is just secure random data.
             */
            RandomHash.GetRandomBytes(saltBuffer);

            /*
             * Unfortuantly calling the internal hash method will cost mutliple 
             * allocations, but I care more about safety and correctness here 
             * over performance.
             */
            Hash(
                password: password,
                salt: saltBuffer, 
                hashOutput: hashBuffer
            );

            //Hash size is the desired hash size
            return new((int)_config.HashLen);
        }

        /// <summary>
        /// NOT SUPPORTED! Use <see cref="Verify(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
        /// instead to specify the salt that was used to encypt the original password
        /// </summary>
        /// <param name="passHash"></param>
        /// <param name="password"></param>
        /// <exception cref="NotSupportedException"></exception>
        public bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password) => throw new NotSupportedException();

        /*
        * Always alloc page aligned to help keep block allocations 
        * a little less obvious. 
        */
        private UnsafeMemoryHandle<byte> AllocSecretBuffer() =>
            _secret is null ? new() : MemoryUtil.UnsafeAllocNearestPage<byte>(_config.BufferHeap, _secret.BufferSize, zero: true);
    }
}