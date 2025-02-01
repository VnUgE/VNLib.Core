/*
* Copyright (c) 2025 Vaughn Nugent
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
    public sealed class PasswordHashing : IPasswordHashingProvider
    {
        private const int STACK_MAX_BUFF_SIZE = 128;

        private readonly ISecretProvider? _secret;
        private readonly IArgon2Library _argon2;
        private readonly Argon2ConfigParams _config;
        
        private PasswordHashing(IArgon2Library library, ISecretProvider? secret, in Argon2ConfigParams setup)
        {
            //Store getter
            _argon2 = library ?? throw new ArgumentNullException(nameof(library));
            _secret = secret;
            _config = setup;
        }

        /// <summary>
        /// Creates a new <see cref="PasswordHashing"/> instance using the specified library.
        /// </summary>
        /// <param name="library">The library instance to use</param>
        /// <param name="secret">The password secret provider</param>
        /// <param name="setup">The configuration setup arguments</param>
        /// <returns>The instance of the library to use</returns>
        public static PasswordHashing Create(IArgon2Library library, ISecretProvider? secret, in Argon2ConfigParams setup) 
            => new (library, secret, in setup);

        /// <summary>
        /// Creates a new <see cref="PasswordHashing"/> instance using the default 
        /// <see cref="VnArgon2"/> library.
        /// </summary>
        /// <param name="secret">The password secret provider</param>
        /// <param name="setup">The configuration setup arguments</param>
        /// <returns>The instance of the library to use</returns>
        /// <exception cref="DllNotFoundException"></exception>
        public static PasswordHashing Create(ISecretProvider? secret, in Argon2ConfigParams setup) 
            => Create(VnArgon2.GetOrLoadSharedLib(), secret, in setup);

        /// <summary>
        /// Creates a new <see cref="PasswordHashing"/> instance using the specified library.
        /// </summary>
        /// <param name="library">The library instance to use</param>
        /// <param name="setup">The configuration setup arguments</param>
        /// <returns>The instance of the library to use</returns>
        public static PasswordHashing Create(IArgon2Library library, in Argon2ConfigParams setup)
            => Create(library, secret: null, in setup);

        /// <summary>
        /// Creates a new <see cref="PasswordHashing"/> instance using the default 
        /// <see cref="VnArgon2"/> library.
        /// </summary>
        /// <param name="setup">The configuration setup arguments</param>
        /// <returns>The instance of the library to use</returns>
        /// <exception cref="DllNotFoundException"></exception>
        public static PasswordHashing Create(in Argon2ConfigParams setup)
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
            else if (_secret.BufferSize < STACK_MAX_BUFF_SIZE)
            {
                /*
                 * Also always alloc fixed buffer size again to help 
                 * be less obvious during process allocations
                 */
                Span<byte> secretBuffer = stackalloc byte[STACK_MAX_BUFF_SIZE];

                ERRNO secretSize = _secret.GetSecret(secretBuffer);

                return _argon2.Verify2id(
                    rawPass: password,
                    hash: passHash,
                    secret: secretBuffer.Slice(0, secretSize)
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
            if (hash.Length < STACK_MAX_BUFF_SIZE)
            {               
                Span<byte> hashBuf = stackalloc byte[hash.Length];
              
                Hash(password, salt, hashBuf);

                //Compare the hashed password to the specified hash and return results
                return CryptographicOperations.FixedTimeEquals(hash, hashBuf);
            }
            else
            {
                using UnsafeMemoryHandle<byte> hashBuf = MemoryUtil.UnsafeAlloc(hash.Length, true);
                
                Hash(password, salt, hashBuf.Span);

                //Compare the hashed password to the specified hash and return results
                return CryptographicOperations.FixedTimeEquals(hash, hashBuf.Span);
            }
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

        /// <summary>
        /// NOT SUPPORTED! Use <see cref="Verify(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
        /// instead to specify the salt that was used to encypt the original password
        /// </summary>
        /// <param name="passHash"></param>
        /// <param name="password"></param>
        /// <exception cref="NotSupportedException"></exception>
        public bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password) => throw new NotSupportedException();

        ///<inheritdoc/>
        ///<exception cref="VnArgon2Exception"></exception>
        public ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput)
        {
            int secretSize = _secret is null ? 0 : _secret.BufferSize;

            //Calc the min buffer size
            int minBufferSize = _config.SaltLen + secretSize + (int)_config.HashLen;

            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAllocNearestPage(minBufferSize, true);

            try
            {
                HashBufferSegments segments;

                if (_secret is null)
                {                    
                    segments = new(
                        buffer.Span,
                        saltSize: _config.SaltLen,
                        hashSize: (int)_config.HashLen
                    );

                    Debug.Assert(segments.SecretBuffer.Length == 0);
                }
                else
                {                    
                    segments = new(
                        buffer.Span,
                        secretSize: secretSize,
                        saltSize: _config.SaltLen,
                        hashSize: (int)_config.HashLen
                    );

                    Debug.Assert(segments.SecretBuffer.Length > 0);

                    //recover the secret
                    ERRNO count = _secret.GetSecret(segments.SecretBuffer);

                    /*
                     * If the actual secret size is less than the buffer size
                     * then we need to adjust the segment size to match the actual
                     * secret size
                     */
                    if (count < secretSize)
                    {
                        segments = new(
                            buffer.Span,
                            secretSize: (int)count,
                            saltSize: _config.SaltLen,
                            hashSize: (int)_config.HashLen
                        );
                    }

                    Debug.Assert(segments.SecretBuffer.Length == count);
                }

                Argon2CostParams costParams = GetCostParams();

                /*
                 * Salt is just secure random data.
                 */
                RandomHash.GetRandomBytes(segments.SaltBuffer);

                //Hash the password in binary
                _argon2.Hash2id(
                    password,
                    salt: segments.SaltBuffer,
                    secret: segments.SecretBuffer,
                    rawHashOutput: segments.HashBuffer,
                    costParams: in costParams
                );

                //Hash size is the desired hash size
                return new((int)_config.HashLen);
            }
            finally
            {
                MemoryUtil.InitializeBlock(ref buffer.GetReference(), buffer.IntLength);
            }

        }

        /*
        * Always alloc page aligned to help keep block allocations 
        * a little less obvious. 
        */
        private UnsafeMemoryHandle<byte> AllocSecretBuffer() =>
            _secret is null ? new() : MemoryUtil.UnsafeAllocNearestPage(_secret.BufferSize, true);

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

            public HashBufferSegments(Span<byte> buffer, int saltSize, int hashSize)
            {
                //No secret to store
                SecretBuffer = default;
               
                SaltBuffer = buffer[..saltSize];
              
                buffer = buffer[saltSize..];
              
                HashBuffer = buffer[..hashSize];
            }
        }
    }
}