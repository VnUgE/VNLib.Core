/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Diagnostics;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Native;
using VNLib.Utils.Extensions;
using VNLib.Utils.Resources;
using VNLib.Hashing.Native.MonoCypher;

/*
 * Some stuff to note
 * 
 * Functions have explicit parameters to avoid accidental buffer mixup
 * when calling nested/overload functions. Please keep it that way for now
 * I really want to avoid a whoopsie in password hasing.
 */

namespace VNLib.Hashing
{

    /// <summary>
    /// Implements the Argon2 data hashing library in .NET for cross platform use.
    /// </summary>
    /// <remarks>Buffers are allocted on a private <see cref="IUnmangedHeap"/> instance.</remarks>
    public static unsafe class VnArgon2
    {
        /// <summary>
        /// Default flags value for Argon2 operations
        /// </summary>
        public const uint ARGON2_DEFAULT_FLAGS = 0U;
        
        /// <summary>
        /// Default hash size in bytes for Argon2 output
        /// </summary>
        public const uint HASH_SIZE = 128;
        
        /// <summary>
        /// Maximum allowed salt size in bytes
        /// </summary>
        public const int MAX_SALT_SIZE = 100;
        
        /// <summary>
        /// The Argon2 variant identifier string (argon2id)
        /// </summary>
        public const string ID_MODE = "argon2id";
        
        /// <summary>
        /// Default name of the Argon2 shared library (argon2.dll on Windows, libargon2.so on Linux, etc.).
        /// </summary>
        public const string ARGON2_DEFUALT_LIB_NAME = "argon2";
        
        /// <summary>
        /// Environment variable name for specifying custom Argon2 library path. This 
        /// variable is read at library load time to determine the path to the native Argon2 library.
        /// </summary>
        public const string ARGON2_LIB_ENVIRONMENT_VAR_NAME = "VNLIB_ARGON2_DLL_PATH";

        private static readonly Encoding LocEncoding = Encoding.Unicode;
        private static readonly LazyInitializer<IUnmangedHeap> _heap = new (static () => MemoryUtil.InitializeNewHeapForProcess(true));
        private static readonly LazyInitializer<IArgon2Library> _nativeLibrary = new(LoadSharedLibInternal);


        //Private heap initialized to 10k size, and all allocated buffers will be zeroed when allocated
        private static IUnmangedHeap PwHeap => _heap.Instance;

        private static IArgon2Library LoadSharedLibInternal()
        {
            //Get the path to the argon2 library
            string? argon2EnvPath = Environment.GetEnvironmentVariable(ARGON2_LIB_ENVIRONMENT_VAR_NAME);

            //If no native library is set, try to load the monocypher library
            if (string.IsNullOrWhiteSpace(argon2EnvPath) && MonoCypherLibrary.CanLoadDefaultLibrary())
            {
                Trace.WriteLine("Using the native MonoCypher library for Argon2 password hashing", "VnArgon2");

                //Load shared monocyphter argon2 library
                return MonoCypherLibrary.Shared.Argon2CreateLibrary(_heap.Instance);
            }
            else
            {
                //Default to the default library name
                argon2EnvPath ??= ARGON2_DEFUALT_LIB_NAME;

                Trace.WriteLine("Attempting to load global native Argon2 library from: " + argon2EnvPath, "VnArgon2");
        
                return LoadCustomLibrary(argon2EnvPath, DllImportSearchPath.SafeDirectories);
            }
        }


        /// <summary>
        /// Gets the sahred native library instance for the current process.
        /// </summary>
        /// <returns>The shared library instance</returns>
        /// <exception cref="DllNotFoundException"></exception>
        public static IArgon2Library GetOrLoadSharedLib() => _nativeLibrary.Instance;

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
            int passBytes = LocEncoding.GetByteCount(password);
            
            //Alloc memory for salt
            using MemoryHandle<byte> buffer = MemoryUtil.SafeAllocNearestPage<byte>(PwHeap, saltbytes + passBytes);
            
            Span<byte> saltBuffer = buffer.AsSpan(0, saltbytes);
            Span<byte> passBuffer = buffer.AsSpan(saltbytes, passBytes);
            
            //Decode from character buffers to binary buffers using default string encoding
            _ = LocEncoding.GetBytes(salt, saltBuffer);
            _ = LocEncoding.GetBytes(password, passBuffer);
           
            string result = Hash2id(
                lib: lib, 
                password: passBuffer, 
                salt: saltBuffer, 
                secret: secret, 
                costParams: in costParams, 
                hashLen: hashLen
            );
          
            MemoryUtil.InitializeBlock(
                ref buffer.GetReference(), 
                buffer.GetIntLength()
            );

            return result;
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
            
            //Alloc memory for password, round to page size again
            using MemoryHandle<byte> pwdHandle = MemoryUtil.SafeAllocNearestPage<byte>(PwHeap, passBytes);

            //Encode password, create a new span to make sure its proper size 
            passBytes = LocEncoding.GetBytes(password, pwdHandle.Span);
           
            string result = Hash2id(
                lib: lib, 
                password: pwdHandle.AsSpan(0, passBytes), //Only actuall size for decoding 
                salt: salt, 
                secret: secret, 
                costParams: in costParams, 
                hashLen: hashLen
            );

            //Zero buffer
            MemoryUtil.InitializeBlock(
                ref pwdHandle.GetReference(), 
                pwdHandle.GetIntLength()
            );

            return result;
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

            /*
             * Alloc a buffer of the nearest page to help disguise password related 
             * allocations. Global zero is always set on PwHeap.
             */
            using MemoryHandle<byte> outputHandle = MemoryUtil.SafeAllocNearestPage<byte>(PwHeap, hashLen);

            //Trim buffer to exact hash size as it will likely be larger due to page alignment
            Span<byte> outBuffer = outputHandle.AsSpan(0, checked((int)hashLen));
         
            Hash2id(
                lib: lib, 
                password: password, 
                salt: salt, 
                secret: secret,
                rawHashOutput: outBuffer, 
                costParams: in costParams
            );
          
            hash = Convert.ToBase64String(outBuffer);
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
            //Setup context
            Argon2Context ctx = new()
            {
                version         = Argon2Version.Argon2DefaultVersion,
                t_cost          = costParams.TimeCost,
                m_cost          = costParams.MemoryCost,
                threads         = costParams.Parallelism,
                lanes           = costParams.Parallelism,
                flags           = ARGON2_DEFAULT_FLAGS,
                allocate_cbk    = null,
                free_cbk        = null,
            };

            fixed (byte*
                pSecret = secret,
                pPass = password,
                pSalt = salt,
                pRawHash = rawHashOutput
            )
            {
                ctx.pwd = pPass;
                ctx.pwdlen = (uint)password.Length;

                ctx.salt = pSalt;
                ctx.saltlen = (uint)salt.Length;

                ctx.secret = pSecret;
                ctx.secretlen = (uint)secret.Length;

                ctx.outptr = pRawHash;
                ctx.outlen = (uint)rawHashOutput.Length;    //Clamp to actual desired hash size

                Argon2_ErrorCodes argResult = (Argon2_ErrorCodes)lib.Argon2Hash(new IntPtr(&ctx));
                //Throw an excpetion if an error ocurred
                ThrowOnArgonErr(argResult);
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

            /*
             * Alloc binary buffer for decoding. Align it to the nearest page again
             * to help disguise the allocation purpose.
             */
            int bufferSize = passBase64BufSize + saltBase64BufSize + rawPassLen;
            using MemoryHandle<byte> rawBufferHandle = MemoryUtil.SafeAllocNearestPage<byte>(PwHeap, bufferSize);
            
            //Split buffers
            Span<byte> saltBuf = rawBufferHandle.AsSpan(0, saltBase64BufSize);
            Span<byte> passBuf = rawBufferHandle.AsSpan(saltBase64BufSize, passBase64BufSize);
            Span<byte> rawPassBuf = rawBufferHandle.AsSpan(saltBase64BufSize + passBase64BufSize, rawPassLen);

            //Decode hash
            {
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

            bool result = Verify2id(
                lib: lib, 
                rawPass: rawPassBuf[..rawPassLen], 
                salt: saltBuf, 
                secret: secret, 
                hashBytes: passBuf, 
                costParams: in costParams
            );

            //Zero entire buffer
            MemoryUtil.InitializeBlock(
                ref rawBufferHandle.GetReference(), 
                rawBufferHandle.GetIntLength()
            );

            return result;
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
            /*
             * Alloc a buffer of the nearest page to help disguise password related 
             * allocations. Global zero is always set on PwHeap.
             */
            using MemoryHandle<byte> outputHandle = MemoryUtil.SafeAllocNearestPage<byte>(PwHeap, hashBytes.Length);

            //Trim buffer to exact hash size as it will likely be larger due to page alignment
            Span<byte> outBuffer = outputHandle.AsSpan(0, hashBytes.Length);

            /*
             * Verification works by computing the hash of the input and comparing it
             * to the existing one. Hash functions are one-way by design, so now you know :)
             */

            Hash2id(
                lib: lib, 
                password: rawPass, 
                salt: salt, 
                secret: secret, 
                rawHashOutput: outBuffer, 
                costParams: in costParams
            );

            bool result = CryptographicOperations.FixedTimeEquals(outBuffer, hashBytes);
            
            MemoryUtil.InitializeBlock(
                ref outputHandle.GetReference(), 
                outputHandle.GetIntLength()
            );

            return result;
        }

        internal static void ThrowOnArgonErr(Argon2_ErrorCodes result)
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