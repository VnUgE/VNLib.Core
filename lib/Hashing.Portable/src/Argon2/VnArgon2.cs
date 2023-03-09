/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: VnArgon2.cs 
*
* VnArgon2.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Threading;
using System.Buffers.Text;
using System.Security.Cryptography;

using VNLib.Utils.Memory;
using VNLib.Utils.Native;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing
{

    /// <summary>
    /// Implements the Argon2 data hashing library in .NET for cross platform use.
    /// </summary>
    /// <remarks>Buffers are allocted on a private <see cref="IUnmangedHeap"/> instance.</remarks>
    public static unsafe partial class VnArgon2
    {
        public const uint ARGON2_DEFAULT_FLAGS = 0U;
        public const uint HASH_SIZE = 128;
        public const int MAX_SALT_SIZE = 100;
        public const string ID_MODE = "argon2id";
        public const string ARGON2_CTX_SAFE_METHOD_NAME = "argon2id_ctx";
        public const string ARGON2_LIB_ENVIRONMENT_VAR_NAME = "ARGON2_DLL_PATH";
        public const string ARGON2_DEFUALT_LIB_NAME = "Argon2";

        private static readonly Encoding LocEncoding = Encoding.Unicode;
        private static readonly Lazy<IUnmangedHeap> _heap = new (MemoryUtil.InitializeNewHeapForProcess, LazyThreadSafetyMode.PublicationOnly);
        private static readonly Lazy<Argon2NativeLibary> _nativeLibrary = new(LoadNativeLib, LazyThreadSafetyMode.PublicationOnly);
     

        //Private heap initialized to 10k size, and all allocated buffers will be zeroed when allocated
        private static IUnmangedHeap PwHeap => _heap.Value;

        /* Argon2 primitive type */
        private enum Argon2_type
        {
            Argon2_d, 
            Argon2_i, 
            Argon2_id
        }

        /* Version of the algorithm */
        private enum Argon2_version
        {
            VERSION_10 = 0x10,
            VERSION_13 = 0x13,
            ARGON2_VERSION_NUMBER = VERSION_13
        }
        
        /*
         * The native library delegate method
         */
        delegate int Argon2InvokeHash(Argon2_Context* context);
        
        /*
         * Wrapper class that manages lifetime of the native library
         * This should basically never get finalized, but if it does
         * it will free the native lib
         */
        private sealed class Argon2NativeLibary
        {
            private readonly SafeMethodHandle<Argon2InvokeHash> _argon2id_ctx;

            public Argon2NativeLibary(SafeMethodHandle<Argon2InvokeHash> method) => _argon2id_ctx = method;

            public int Argon2Hash(Argon2_Context* context) => _argon2id_ctx.Method!.Invoke(context);

            ~Argon2NativeLibary()
            {
                //Dispose method handle which will release the native library
                _argon2id_ctx.Dispose();
            }
        }

        /// <summary>
        /// Loads the native Argon2 libray into the process with env variable library path
        /// </summary>
        /// <returns></returns>
        private static Argon2NativeLibary LoadNativeLib()
        {
            //Get the path to the argon2 library
            string? argon2EnvPath = Environment.GetEnvironmentVariable(ARGON2_LIB_ENVIRONMENT_VAR_NAME);
            //Default to the default library name
            argon2EnvPath ??= ARGON2_DEFUALT_LIB_NAME;
            
            //Try to load the libary and always dispose it so the native method handle will unload the library
            using SafeLibraryHandle lib = SafeLibraryHandle.LoadLibrary(argon2EnvPath);

            //Get safe native method
            SafeMethodHandle<Argon2InvokeHash> method = lib.GetMethod<Argon2InvokeHash>(ARGON2_CTX_SAFE_METHOD_NAME);
            
            return new Argon2NativeLibary(method);
        }

        /// <summary>
        /// Hashes a password with a salt and specified arguments
        /// </summary>
        /// <param name="password">Span of characters containing the password to be hashed</param>
        /// <param name="salt">Span of characters contating the salt to include in the hashing</param>
        /// <param name="secret">Optional secret to include in hash</param>
        /// <param name="hashLen">Size of the hash in bytes</param>
        /// <param name="memCost">Memory cost</param>
        /// <param name="parallelism">Degree of parallelism</param>
        /// <param name="timeCost">Time cost of operation</param>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="InsufficientMemoryException"></exception>
        /// <returns>A <see cref="Encoding.Unicode"/> <see cref="string"/> containg the ready-to-store hash</returns>                
        public static string Hash2id(ReadOnlySpan<char> password, ReadOnlySpan<char> salt, ReadOnlySpan<byte> secret,
            uint timeCost = 2, uint memCost = 65535, uint parallelism = 4, uint hashLen = HASH_SIZE)
        {
            //Get bytes count
            int saltbytes = LocEncoding.GetByteCount(salt);
            
            //Get bytes count for password
            int passBytes = LocEncoding.GetByteCount(password);
            
            //Alloc memory for salt
            using IMemoryHandle<byte> buffer = PwHeap.Alloc<byte>(saltbytes + passBytes, true);
            
            Span<byte> saltBuffer = buffer.AsSpan(0, saltbytes);
            Span<byte> passBuffer = buffer.AsSpan(passBytes);
            
            //Encode salt with span the same size of the salt
            _ = LocEncoding.GetBytes(salt, saltBuffer);

            //Encode password, create a new span to make sure its proper size 
            _ = LocEncoding.GetBytes(password, passBuffer);
            
            //Hash
            return Hash2id(passBuffer, saltBuffer, secret, timeCost, memCost, parallelism, hashLen);
        }

        /// <summary>
        /// Hashes a password with a salt and specified arguments
        /// </summary>
        /// <param name="password">Span of characters containing the password to be hashed</param>
        /// <param name="salt">Span of characters contating the salt to include in the hashing</param>
        /// <param name="secret">Optional secret to include in hash</param>
        /// <param name="hashLen">Size of the hash in bytes</param>
        /// <param name="memCost">Memory cost</param>
        /// <param name="parallelism">Degree of parallelism</param>
        /// <param name="timeCost">Time cost of operation</param>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="InsufficientMemoryException"></exception>
        /// <returns>A <see cref="Encoding.Unicode"/> <see cref="string"/> containg the ready-to-store hash</returns>
        public static string Hash2id(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> secret,
            uint timeCost = 2, uint memCost = 65535, uint parallelism = 4, uint hashLen = HASH_SIZE)
        {
            //Get bytes count
            int passBytes = LocEncoding.GetByteCount(password);
            
            //Alloc memory for password
            using IMemoryHandle<byte> pwdHandle = PwHeap.Alloc<byte>(passBytes, true);
            
            //Encode password, create a new span to make sure its proper size 
            _ = LocEncoding.GetBytes(password, pwdHandle.Span);
            
            //Hash
            return Hash2id(pwdHandle.Span, salt, secret, timeCost, memCost, parallelism, hashLen);
        }
      
        /// <summary>
        /// Hashes a password with a salt and specified arguments
        /// </summary>
        /// <param name="password">Span of characters containing the password to be hashed</param>
        /// <param name="salt">Span of characters contating the salt to include in the hashing</param>
        /// <param name="secret">Optional secret to include in hash</param>
        /// <param name="hashLen">Size of the hash in bytes</param>
        /// <param name="memCost">Memory cost</param>
        /// <param name="parallelism">Degree of parallelism</param>
        /// <param name="timeCost">Time cost of operation</param>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <returns>A <see cref="Encoding.Unicode"/> <see cref="string"/>containg the ready-to-store hash</returns>                
        public static string Hash2id(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> secret,
            uint timeCost = 2, uint memCost = 65535, uint parallelism = 4, uint hashLen = HASH_SIZE)
        {
            string hash, salts;
            //Alloc data for hash output
            using IMemoryHandle<byte> hashHandle = PwHeap.Alloc<byte>(hashLen, true);
            
            //hash the password
            Hash2id(password, salt, secret, hashHandle.Span, timeCost, memCost, parallelism);

            //Encode hash
            hash = Convert.ToBase64String(hashHandle.Span);
            
            //encode salt
            salts = Convert.ToBase64String(salt);
            
            //Encode salt in base64
            return $"${ID_MODE}$v={(int)Argon2_version.VERSION_13},m={memCost},t={timeCost},p={parallelism},s={salts}${hash}";
        }
     

        /// <summary>
        /// Exposes the raw Argon2-ID hashing api to C#, using spans (pins memory references)
        /// </summary>
        /// <param name="password">Span of characters containing the password to be hashed</param>
        /// <param name="rawHashOutput">The output buffer to store the raw hash output</param>
        /// <param name="salt">Span of characters contating the salt to include in the hashing</param>
        /// <param name="secret">Optional secret to include in hash</param>
        /// <param name="memCost">Memory cost</param>
        /// <param name="parallelism">Degree of parallelism</param>
        /// <param name="timeCost">Time cost of operation</param>
        /// <exception cref="VnArgon2Exception"></exception>
        public static void Hash2id(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> secret, Span<byte> rawHashOutput,
            uint timeCost = 2, uint memCost = 65535, uint parallelism = 4)
        {
            fixed (byte* pwd = password, slptr = salt, secretptr = secret, outPtr = rawHashOutput)
            {
                //Setup context
                Argon2_Context ctx;
                //Pointer
                Argon2_Context* context = &ctx;
                context->version = Argon2_version.VERSION_13;
                context->t_cost = timeCost;
                context->m_cost = memCost;
                context->threads = parallelism;
                context->lanes = parallelism;
                //Default flags
                context->flags = ARGON2_DEFAULT_FLAGS;
                context->allocate_cbk = null;
                context->free_cbk = null;
                //Password
                context->pwd = pwd;
                context->pwdlen = (UInt32)password.Length;
                //Salt
                context->salt = slptr;
                context->saltlen = (UInt32)salt.Length;
                //Secret
                context->secret = secretptr;
                context->secretlen = (UInt32)secret.Length;
                //Output
                context->outptr = outPtr;
                context->outlen = (UInt32)rawHashOutput.Length;
                //Hash
                Argon2_ErrorCodes result = (Argon2_ErrorCodes)_nativeLibrary.Value.Argon2Hash(&ctx);
                //Throw exceptions if error
                ThrowOnArgonErr(result);
            }
        }


        /// <summary>
        /// Compares a raw password, with a salt to a raw hash
        /// </summary>
        /// <param name="rawPass">Password bytes</param>
        /// <param name="salt">Salt bytes</param>
        /// <param name="secret">Optional secret that was included in hash</param>
        /// <param name="hashBytes">Raw hash bytes</param>
        /// <param name="timeCost">Time cost</param>
        /// <param name="memCost">Memory cost</param>
        /// <param name="parallelism">Degree of parallelism</param>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="InsufficientMemoryException"></exception>
        /// <exception cref="VnArgon2PasswordFormatException"></exception>
        /// <returns>True if hashes match</returns>
        public static bool Verify2id(ReadOnlySpan<byte> rawPass, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> secret, ReadOnlySpan<byte> hashBytes,
            uint timeCost = 2, uint memCost = 65535, uint parallelism = 4)
        {
            //Alloc data for hash output
            using IMemoryHandle<byte> outputHandle = PwHeap.Alloc<byte>(hashBytes.Length, true);

            //Pin to get the base pointer
            using MemoryHandle outputPtr = outputHandle.Pin(0);
            
            //Get pointers
            fixed (byte* secretptr = secret, pwd = rawPass, slptr = salt)
            {
                //Setup context
                Argon2_Context ctx;
                //Pointer
                Argon2_Context* context = &ctx;
                context->version = Argon2_version.VERSION_13;
                context->m_cost = memCost;
                context->t_cost = timeCost;
                context->threads = parallelism;
                context->lanes = parallelism;
                //Default flags
                context->flags = ARGON2_DEFAULT_FLAGS;
                //Use default memory allocator
                context->allocate_cbk = null;
                context->free_cbk = null;
                //Password
                context->pwd = pwd;
                context->pwdlen = (uint)rawPass.Length;
                //Salt
                context->salt = slptr;
                context->saltlen = (uint)salt.Length;
                //Secret
                context->secret = secretptr;
                context->secretlen = (uint)secret.Length;
                //Output
                context->outptr = outputPtr.Pointer;
                context->outlen = (uint)outputHandle.Length;
                //Hash
                Argon2_ErrorCodes result = (Argon2_ErrorCodes)_nativeLibrary.Value.Argon2Hash(&ctx);
                //Throw an excpetion if an error ocurred
                ThrowOnArgonErr(result);
            }
            //Return the comparison
            return CryptographicOperations.FixedTimeEquals(outputHandle.Span, hashBytes);
        }

        /// <summary>
        /// Compares a password to a previously hashed password from this library
        /// </summary>
        /// <param name="rawPass">Password data</param>
        /// <param name="secret">Optional secret that was included in hash</param>
        /// <param name="hash">Full hash span</param>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="InsufficientMemoryException"></exception>
        /// <exception cref="VnArgon2PasswordFormatException"></exception>
        /// <returns>True if the password matches the hash</returns>
        public static bool Verify2id(ReadOnlySpan<char> rawPass, ReadOnlySpan<char> hash, ReadOnlySpan<byte> secret)
        {
            if (!hash.Contains(ID_MODE, StringComparison.Ordinal))
            {
                throw new VnArgon2PasswordFormatException("The hash argument supplied is not a valid format and cannot be decoded");
            }
            
            Argon2PasswordEntry entry;
            try
            {
                //Init password breakout struct
                entry = new(hash);
            }
            catch (Exception ex)
            {
                throw new VnArgon2PasswordFormatException("Password format was not recoverable", ex);
            }
            
            //Calculate base64 buffer sizes
            int passBase64BufSize = Base64.GetMaxDecodedFromUtf8Length(entry.Hash.Length);
            int saltBase64BufSize = Base64.GetMaxDecodedFromUtf8Length(entry.Salt.Length);
            int rawPassLen = LocEncoding.GetByteCount(rawPass);

            //Alloc buffer for decoded data
            using IMemoryHandle<byte> rawBufferHandle = MemoryUtil.Shared.Alloc<byte>(passBase64BufSize + saltBase64BufSize + rawPassLen, true);
            
            //Split buffers
            Span<byte> saltBuf = rawBufferHandle.Span[..saltBase64BufSize];
            Span<byte> passBuf = rawBufferHandle.AsSpan(saltBase64BufSize, passBase64BufSize);
            Span<byte> rawPassBuf = rawBufferHandle.AsSpan(saltBase64BufSize + passBase64BufSize, rawPassLen);
            {
                //Decode salt
                if (!Convert.TryFromBase64Chars(entry.Hash, passBuf, out int actualHashLen))
                {
                    throw new VnArgon2PasswordFormatException("Failed to recover hash bytes");
                }
                //Resize pass buff
                passBuf = passBuf[..actualHashLen];
            }
            
            //Decode salt
            {
                if (!Convert.TryFromBase64Chars(entry.Salt, saltBuf, out int actualSaltLen))
                {
                    throw new VnArgon2PasswordFormatException("Failed to recover salt bytes");
                }
                //Resize salt buff
                saltBuf = saltBuf[..actualSaltLen];
            }
            
            //encode password bytes
            rawPassLen = LocEncoding.GetBytes(rawPass, rawPassBuf);
            //Verify password
            return Verify2id(rawPassBuf[..rawPassLen], saltBuf, secret, passBuf, entry.TimeCost, entry.MemoryCost, entry.Parallelism);
        }

        private static void ThrowOnArgonErr(Argon2_ErrorCodes result)
        {
            switch (result)
            {
                //Success
                case Argon2_ErrorCodes.ARGON2_OK:
                    break;
                case Argon2_ErrorCodes.ARGON2_OUTPUT_PTR_NULL:
                    throw new VnArgon2Exception("Pointer to output data was null", result);
                case Argon2_ErrorCodes.ARGON2_OUTPUT_TOO_SHORT:
                    throw new VnArgon2Exception("Output array too short", result);
                case Argon2_ErrorCodes.ARGON2_OUTPUT_TOO_LONG:
                    throw new VnArgon2Exception("Pointer output data too long", result);
                case Argon2_ErrorCodes.ARGON2_PWD_TOO_SHORT:
                    throw new VnArgon2Exception("Password too short", result);
                case Argon2_ErrorCodes.ARGON2_PWD_TOO_LONG:
                    throw new VnArgon2Exception("Password too long", result);
                case Argon2_ErrorCodes.ARGON2_SECRET_TOO_SHORT:
                case Argon2_ErrorCodes.ARGON2_SALT_TOO_SHORT:
                    throw new VnArgon2Exception("Salt too short", result);
                case Argon2_ErrorCodes.ARGON2_SECRET_TOO_LONG:
                case Argon2_ErrorCodes.ARGON2_SALT_TOO_LONG:
                    throw new VnArgon2Exception("Salt too long", result);
                case Argon2_ErrorCodes.ARGON2_TIME_TOO_SMALL:
                    throw new VnArgon2Exception("Time cost too small", result);
                case Argon2_ErrorCodes.ARGON2_TIME_TOO_LARGE:
                    throw new VnArgon2Exception("Time cost too large", result);
                case Argon2_ErrorCodes.ARGON2_MEMORY_TOO_LITTLE:
                    throw new VnArgon2Exception("Memory cost too small", result);
                case Argon2_ErrorCodes.ARGON2_MEMORY_TOO_MUCH:
                    throw new VnArgon2Exception("Memory cost too large", result);
                case Argon2_ErrorCodes.ARGON2_LANES_TOO_FEW:
                    throw new VnArgon2Exception("Not enough parallelism lanes", result);
                case Argon2_ErrorCodes.ARGON2_LANES_TOO_MANY:
                    throw new VnArgon2Exception("Too many parallelism lanes", result);
                case Argon2_ErrorCodes.ARGON2_MEMORY_ALLOCATION_ERROR:
                    throw new VnArgon2Exception("Memory allocation error", result);
                case Argon2_ErrorCodes.ARGON2_PWD_PTR_MISMATCH:
                case Argon2_ErrorCodes.ARGON2_SALT_PTR_MISMATCH:
                case Argon2_ErrorCodes.ARGON2_SECRET_PTR_MISMATCH:
                case Argon2_ErrorCodes.ARGON2_AD_PTR_MISMATCH:
                case Argon2_ErrorCodes.ARGON2_FREE_MEMORY_CBK_NULL:
                case Argon2_ErrorCodes.ARGON2_ALLOCATE_MEMORY_CBK_NULL:
                case Argon2_ErrorCodes.ARGON2_INCORRECT_PARAMETER:
                case Argon2_ErrorCodes.ARGON2_INCORRECT_TYPE:
                case Argon2_ErrorCodes.ARGON2_OUT_PTR_MISMATCH:
                case Argon2_ErrorCodes.ARGON2_THREADS_TOO_FEW:
                case Argon2_ErrorCodes.ARGON2_THREADS_TOO_MANY:
                case Argon2_ErrorCodes.ARGON2_MISSING_ARGS:
                case Argon2_ErrorCodes.ARGON2_ENCODING_FAIL:
                case Argon2_ErrorCodes.ARGON2_DECODING_FAIL:
                case Argon2_ErrorCodes.ARGON2_THREAD_FAIL:
                case Argon2_ErrorCodes.ARGON2_DECODING_LENGTH_FAIL:
                case Argon2_ErrorCodes.ARGON2_VERIFY_MISMATCH:
                default:
                    throw new VnArgon2Exception($"Unhandled Argon2 operation {result}", result);
            }
        }
    }
}