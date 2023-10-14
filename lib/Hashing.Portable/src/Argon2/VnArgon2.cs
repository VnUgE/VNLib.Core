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
using System.Diagnostics;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

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
        public const string ARGON2_DEFUALT_LIB_NAME = "argon2";
        public const string ARGON2_LIB_ENVIRONMENT_VAR_NAME = "ARGON2_DLL_PATH";

        private static readonly Encoding LocEncoding = Encoding.Unicode;
        private static readonly Lazy<IUnmangedHeap> _heap = new (static () => MemoryUtil.InitializeNewHeapForProcess(true), LazyThreadSafetyMode.PublicationOnly);
        private static readonly Lazy<SafeArgon2Library> _nativeLibrary = new(LoadSharedLibInternal, LazyThreadSafetyMode.PublicationOnly);


        //Private heap initialized to 10k size, and all allocated buffers will be zeroed when allocated
        private static IUnmangedHeap PwHeap => _heap.Value;

        /*
         * The native library delegate method
         */
        [SafeMethodName("argon2id_ctx")]
        delegate int Argon2InvokeHash(IntPtr context);

        private static SafeArgon2Library LoadSharedLibInternal()
        {
            //Get the path to the argon2 library
            string? argon2EnvPath = Environment.GetEnvironmentVariable(ARGON2_LIB_ENVIRONMENT_VAR_NAME);
            //Default to the default library name
            argon2EnvPath ??= ARGON2_DEFUALT_LIB_NAME;

            Trace.WriteLine("Attempting to load global native Argon2 library from: " + argon2EnvPath, "VnArgon2");

            SafeLibraryHandle lib = SafeLibraryHandle.LoadLibrary(argon2EnvPath, DllImportSearchPath.SafeDirectories);
            return new SafeArgon2Library(lib);
        }


        /// <summary>
        /// Gets the sahred native library instance for the current process.
        /// </summary>
        /// <returns>The shared library instance</returns>
        /// <exception cref="DllNotFoundException"></exception>
        public static IArgon2Library GetOrLoadSharedLib() => _nativeLibrary.Value;

        /// <summary>
        /// Loads a native Argon2 shared library from the specified path 
        /// and returns a <see cref="IArgon2Library"/> instance. wrapper
        /// </summary>
        /// <param name="dllPath">The path to the native shared library to load</param>
        /// <param name="searchPath">The dll search path</param>
        /// <returns>A handle for the library</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="DllNotFoundException"></exception>
        public static SafeArgon2Library LoadCustomLibrary(string dllPath, DllImportSearchPath searchPath)
        {
            //Try to load the libary and always dispose it so the native method handle will unload the library
            SafeLibraryHandle lib = SafeLibraryHandle.LoadLibrary(dllPath, searchPath);
            return new SafeArgon2Library(lib);
        }

        /// <summary>
        /// Hashes a password with a salt and specified arguments
        /// </summary>
        /// <param name="lib"></param>
        /// <param name="password">Span of characters containing the password to be hashed</param>
        /// <param name="salt">Span of characters contating the salt to include in the hashing</param>
        /// <param name="secret">Optional secret to include in hash</param>
        /// <param name="hashLen">Size of the hash in bytes</param>
        /// <param name="costParams">Argon2 cost parameters</param>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="InsufficientMemoryException"></exception>
        /// <returns>A <see cref="Encoding.Unicode"/> <see cref="string"/> containg the ready-to-store hash</returns>                
        public static string Hash2id(
            this IArgon2Library lib, 
            ReadOnlySpan<char> password, 
            ReadOnlySpan<char> salt, 
            ReadOnlySpan<byte> secret,
            in Argon2CostParams costParams, 
            uint hashLen = HASH_SIZE
        )
        {
            //Get bytes count
            int saltbytes = LocEncoding.GetByteCount(salt);
            
            //Get bytes count for password
            int passBytes = LocEncoding.GetByteCount(password);
            
            //Alloc memory for salt
            using IMemoryHandle<byte> buffer = PwHeap.Alloc<byte>(saltbytes + passBytes);
            
            Span<byte> saltBuffer = buffer.AsSpan(0, saltbytes);
            Span<byte> passBuffer = buffer.AsSpan(passBytes);
            
            //Encode salt with span the same size of the salt
            _ = LocEncoding.GetBytes(salt, saltBuffer);

            //Encode password, create a new span to make sure its proper size 
            _ = LocEncoding.GetBytes(password, passBuffer);
            
            //Hash
            return Hash2id(lib, passBuffer, saltBuffer, secret, in costParams, hashLen);
        }

        /// <summary>
        /// Hashes a password with a salt and specified arguments
        /// </summary>
        /// <param name="lib"></param>
        /// <param name="password">Span of characters containing the password to be hashed</param>
        /// <param name="salt">Span of characters contating the salt to include in the hashing</param>
        /// <param name="secret">Optional secret to include in hash</param>
        /// <param name="hashLen">Size of the hash in bytes</param>
        /// <param name="costParams">Argon2 cost parameters</param>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="InsufficientMemoryException"></exception>
        /// <returns>A <see cref="Encoding.Unicode"/> <see cref="string"/> containg the ready-to-store hash</returns>
        public static string Hash2id(
            this IArgon2Library lib,
            ReadOnlySpan<char> password, 
            ReadOnlySpan<byte> salt, 
            ReadOnlySpan<byte> secret, 
            in Argon2CostParams costParams, 
            uint hashLen = HASH_SIZE
        )
        {
            //Get bytes count
            int passBytes = LocEncoding.GetByteCount(password);
            
            //Alloc memory for password
            using IMemoryHandle<byte> pwdHandle = PwHeap.Alloc<byte>(passBytes);
            
            //Encode password, create a new span to make sure its proper size 
            _ = LocEncoding.GetBytes(password, pwdHandle.Span);
            
            //Hash
            return Hash2id(lib, pwdHandle.Span, salt, secret, in costParams, hashLen);
        }

        /// <summary>
        /// Hashes a password with a salt and specified arguments
        /// </summary>
        /// <param name="lib"></param>
        /// <param name="password">Span of characters containing the password to be hashed</param>
        /// <param name="salt">Span of characters contating the salt to include in the hashing</param>
        /// <param name="secret">Optional secret to include in hash</param>
        /// <param name="hashLen">Size of the hash in bytes</param>
        /// <param name="costParams">Argon2 cost parameters</param>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <returns>A <see cref="Encoding.Unicode"/> <see cref="string"/>containg the ready-to-store hash</returns>                
        public static string Hash2id(
            this IArgon2Library lib,
            ReadOnlySpan<byte> password, 
            ReadOnlySpan<byte> salt, 
            ReadOnlySpan<byte> secret, 
            in Argon2CostParams costParams,
            uint hashLen = HASH_SIZE
        )
        {
            string hash, salts;
            //Alloc data for hash output
            using IMemoryHandle<byte> hashHandle = PwHeap.Alloc<byte>(hashLen, true);
            
            //hash the password
            Hash2id(lib, password, salt, secret, hashHandle.Span, in costParams);

            //Encode hash
            hash = Convert.ToBase64String(hashHandle.Span);
            
            //encode salt
            salts = Convert.ToBase64String(salt);
            
            //Encode salt in base64
            return $"${ID_MODE}$v={(int)Argon2Version.Version13},m={costParams.MemoryCost},t={costParams.TimeCost},p={costParams.Parallelism},s={salts}${hash}";
        }


        /// <summary>
        /// Exposes the raw Argon2-ID hashing api to C#, using spans (pins memory references)
        /// </summary>
        /// <param name="lib"></param>
        /// <param name="password">Span of characters containing the password to be hashed</param>
        /// <param name="rawHashOutput">The output buffer to store the raw hash output</param>
        /// <param name="salt">Span of characters contating the salt to include in the hashing</param>
        /// <param name="secret">Optional secret to include in hash</param>
        /// <param name="costParams">Argon2 cost parameters</param>>
        /// <exception cref="VnArgon2Exception"></exception>
        public static void Hash2id(
            this IArgon2Library lib,
            ReadOnlySpan<byte> password,
            ReadOnlySpan<byte> salt,
            ReadOnlySpan<byte> secret,
            Span<byte> rawHashOutput, 
            in Argon2CostParams costParams
        )
        {
            fixed (byte* pwd = password, slptr = salt, secretptr = secret, outPtr = rawHashOutput)
            {
                //Setup context
                Argon2Context ctx;
                //Pointer
                Argon2Context* context = &ctx;
                context->version = Argon2Version.Argon2DefaultVersion;
                context->t_cost = costParams.TimeCost;
                context->m_cost = costParams.MemoryCost;
                context->threads = costParams.Parallelism;
                context->lanes = costParams.Parallelism;
                //Default flags
                context->flags = ARGON2_DEFAULT_FLAGS;
                context->allocate_cbk = null;
                context->free_cbk = null;
                //Password
                context->pwd = pwd;
                context->pwdlen = (uint)password.Length;
                //Salt
                context->salt = slptr;
                context->saltlen = (uint)salt.Length;
                //Secret
                context->secret = secretptr;
                context->secretlen = (uint)secret.Length;
                //Output
                context->outptr = outPtr;
                context->outlen = (uint)rawHashOutput.Length;
                //Hash
                Argon2_ErrorCodes result = (Argon2_ErrorCodes)lib.Argon2Hash((IntPtr)context);
                //Throw exceptions if error
                ThrowOnArgonErr(result);
            }
        }



        /// <summary>
        /// Compares a password to a previously hashed password from this library
        /// </summary>
        /// <param name="lib"></param>
        /// <param name="rawPass">Password data</param>
        /// <param name="secret">Optional secret that was included in hash</param>
        /// <param name="hash">Full hash span</param>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="InsufficientMemoryException"></exception>
        /// <exception cref="VnArgon2PasswordFormatException"></exception>
        /// <returns>True if the password matches the hash</returns>
        public static bool Verify2id(
            this IArgon2Library lib,
            ReadOnlySpan<char> rawPass, 
            ReadOnlySpan<char> hash, 
            ReadOnlySpan<byte> secret
        )
        {
            if (!hash.Contains(ID_MODE, StringComparison.Ordinal))
            {
                throw new VnArgon2PasswordFormatException("The hash argument supplied is not a valid format and cannot be decoded");
            }
            
            Argon2PasswordEntry entry;
            Argon2CostParams costParams;
            try
            {
                //Init password breakout struct
                entry = new(hash);
                costParams = entry.GetCostParams();
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
            using IMemoryHandle<byte> rawBufferHandle = PwHeap.Alloc<byte>(passBase64BufSize + saltBase64BufSize + rawPassLen);
            
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
            return Verify2id(lib, rawPassBuf[..rawPassLen], saltBuf, secret, passBuf, in costParams);
        }

        /// <summary>
        /// Compares a raw password, with a salt to a raw hash
        /// </summary>
        /// <param name="lib"></param>
        /// <param name="rawPass">Password bytes</param>
        /// <param name="salt">Salt bytes</param>
        /// <param name="secret">Optional secret that was included in hash</param>
        /// <param name="hashBytes">Raw hash bytes</param>
        /// <param name="costParams">Argon2 cost parameters</param>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="VnArgon2PasswordFormatException"></exception>
        /// <returns>True if hashes match</returns>
        public static bool Verify2id(
            this IArgon2Library lib,
            ReadOnlySpan<byte> rawPass,
            ReadOnlySpan<byte> salt,
            ReadOnlySpan<byte> secret,
            ReadOnlySpan<byte> hashBytes,
            in Argon2CostParams costParams
        )
        {
            //Alloc data for hash output
            using IMemoryHandle<byte> outputHandle = PwHeap.Alloc<byte>(hashBytes.Length);

            //Pin to get the base pointer
            using MemoryHandle outputPtr = outputHandle.Pin(0);

            //Get pointers
            fixed (byte* secretptr = secret, pwd = rawPass, slptr = salt)
            {
                //Setup context
                Argon2Context ctx;
                //Pointer
                Argon2Context* context = &ctx;
                context->version = Argon2Version.Argon2DefaultVersion;
                context->t_cost = costParams.TimeCost;
                context->m_cost = costParams.MemoryCost;
                context->threads = costParams.Parallelism;
                context->lanes = costParams.Parallelism;
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
                Argon2_ErrorCodes result = (Argon2_ErrorCodes)lib.Argon2Hash((IntPtr)context);
                //Throw an excpetion if an error ocurred
                ThrowOnArgonErr(result);
            }
            //Return the comparison
            return CryptographicOperations.FixedTimeEquals(outputHandle.Span, hashBytes);
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