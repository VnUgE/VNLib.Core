/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: MCBlake2Module.cs 
*
* MCBlake2Module.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing.Native.MonoCypher
{
    /// <summary>
    /// Adds Blake2b hashing support to the <see cref="MonoCypherLibrary"/>
    /// </summary>
    public static unsafe class MCBlake2Module
    {
        [SafeMethodName("Blake2GetContextSize")]
        internal delegate uint Blake2GetContextSize();

        [SafeMethodName("Blake2Init")]
        internal delegate int Blake2Init(IntPtr context, byte hashSize, void* key = null, uint keyLen = 0);

        [SafeMethodName("Blake2Update")]
        internal delegate int Blake2Update(IntPtr context, void* data, uint dataLen);

        [SafeMethodName("Blake2Final")]
        internal delegate int Blake2Final(IntPtr context, void* hash, uint hashLen);

        [SafeMethodName("Blake2GetHashSize")]
        internal delegate uint Blake2GetHashSize(IntPtr context);


        public const int MaxHashSize = 64;
        public const int MaxKeySize = 64;
        public const int MinSuggestedKDFHashSize = 32;
        public const int MinSuggestedMACHashSize = 16;

        /// <summary>
        /// Creates a new <see cref="IHashStream"/> instance with the specified hash size
        /// which must be between 1 and <see cref="MaxHashSize"/> inclusive.
        /// </summary>
        /// <remarks>
        /// A hash size greater than 32 is recommended for KDFs and a value greater than 16 is recommended for MACs.
        /// <para>
        /// <seealso href="https://monocypher.org/manual/blake2b"/>
        /// </para>
        /// </remarks>
        /// <param name="library"></param>
        /// <param name="hashSize">The hash size between 1 and <see cref="MaxHashSize"/> inclusive</param>
        /// <param name="heap">The heap to allocate the stream on</param>
        /// <returns>The initialzied <see cref="IHashStream"/> instance</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static IHashStream Blake2CreateStream(this MonoCypherLibrary library, byte hashSize, IUnmangedHeap? heap)
        {
            _ = library ?? throw new ArgumentNullException(nameof(library));
            if(hashSize == 0 || hashSize > MaxHashSize)
            {
                throw new ArgumentOutOfRangeException(nameof(hashSize), $"The hash size must be between 1 and {MaxHashSize} inclusive");
            }
            //Fall back to the shared heap if none is provided
            heap ??= MemoryUtil.Shared;

            //Alloc stream and initialize it as a non-hmac stream
            Blake2Stream stream = new(library, heap, hashSize);
            try
            {
                //Initialize the stream
                stream.Initialize();
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a new <see cref="IHmacStream"/> keyed MAC instance with the specified hash size
        /// and key which must be between 1 and <see cref="MaxHashSize"/> inclusive. You must 
        /// initialize the instance before use, otherwise results are undefined. 
        /// </summary>
        /// <remarks>
        /// A hash size greater than 32 is recommended for KDFs and a value greater than 16 is recommended for MACs.
        /// <para>
        /// <seealso href="https://monocypher.org/manual/blake2b"/>
        /// </para>
        /// </remarks>
        /// <param name="library"></param>
        /// <param name="hashSize">The hash size between 1 and <see cref="MaxHashSize"/> inclusive</param>
        /// <param name="heap">The heap to allocate the stream on</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static IHmacStream Blake2CreateHmacStream(this MonoCypherLibrary library, byte hashSize, IUnmangedHeap? heap)
        {
            _ = library ?? throw new ArgumentNullException(nameof(library));
            if (hashSize == 0 || hashSize > MaxHashSize)
            {
                throw new ArgumentOutOfRangeException(nameof(hashSize), $"The hash size must be between 1 and {MaxHashSize} inclusive");
            }
            //Fall back to the shared heap if none is provided
            heap ??= MemoryUtil.Shared;

            //Return the raw stream, it will be initialized later
            return new Blake2Stream(library, heap, hashSize);
        }

        /// <summary>
        /// Computes a Blake2b one-shot hash of the specified data and writes it to the variable-length output buffer.
        /// The output buffer must be between 1 and <see cref="MaxHashSize"/> inclusive. 
        /// <para>
        /// See <see href="https://monocypher.org/manual/blake2b"></see> for more information.
        /// </para>
        /// </summary>
        /// <param name="library"></param>
        /// <param name="data">The data buffer to compute the hash of</param>
        /// <param name="output">The hash output buffer</param>
        /// <returns>The number of bytes written to the output buffer or the error code from the native library</returns>
        public static ERRNO Blake2ComputeHash(this MonoCypherLibrary library, ReadOnlySpan<byte> data, Span<byte> output)
        {
            ArgumentNullException.ThrowIfNull(library, nameof(library));
         
            if(output.Length > MaxHashSize)
            {
                return ERRNO.E_FAIL;
            }

            if(output.IsEmpty)
            {
                return ERRNO.E_FAIL;
            }

            //Get context size
            int contextSize = (int)library.Functions.Blake2GetContextSize();

            //Allocate context on the stack
            void* ctx = stackalloc byte[contextSize];

            //Init context
            if(library.Functions.Blake2Init((IntPtr)ctx, (byte)output.Length) != 0)
            {
                return ERRNO.E_FAIL;
            }

            //initialize the context for the stream with hmac key
            fixed (byte* keyPtr = &MemoryMarshal.GetReference(data),
                hashPtr = &MemoryMarshal.GetReference(output)
                )
            {
                //Update with data
                if(library.Functions.Blake2Update((IntPtr)ctx, keyPtr, (uint)data.Length) != 0)
                {
                    return ERRNO.E_FAIL;
                }
                //Copy hash to output
                if (library.Functions.Blake2Final((IntPtr)ctx, hashPtr, (uint)output.Length) != 0)
                {
                    return ERRNO.E_FAIL;
                }
            }

            return output.Length;
        }

        /// <summary>
        /// Computes a Blake2b one-shot keyed MAC of the specified data and writes it to the variable-length output buffer.
        /// The output buffer must be between 1 and <see cref="MaxHashSize"/> inclusive. 
        /// The key must be between 1 and <see cref="MaxKeySize"/> inclusive.
        /// <para>
        /// See <see href="https://monocypher.org/manual/blake2b"></see> for more information.
        /// </para>
        /// </summary>
        /// <param name="library"></param>
        /// <param name="key">The HMAC key</param>
        /// <param name="data">The data buffer to compute the hash of</param>
        /// <param name="output">The hash output buffer</param>
        /// <returns>The number of bytes written to the output buffer or the error code from the native library</returns>
        public static ERRNO Blake2ComputeHmac(this MonoCypherLibrary library, ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output)
        {
            ArgumentNullException.ThrowIfNull(library, nameof(library));

            if (output.Length > MaxHashSize)
            {
                return ERR_HASH_LEN_INVLID;
            }

            if (output.IsEmpty)
            {
                return ERR_NULL_PTR;
            }

            if(key.IsEmpty)
            {
                return ERR_KEY_PTR_INVALID;
            }

            if(key.Length > MaxKeySize)
            {
                return ERR_KEY_LEN_INVALID;
            }

            //Get context size
            int contextSize = (int)library.Functions.Blake2GetContextSize();

            //Allocate context on the stack
            void* ctx = stackalloc byte[contextSize];

            fixed(byte* keyPtr = &MemoryMarshal.GetReference(key))
            {
                //Init context with hmac parameters
                if (library.Functions.Blake2Init((IntPtr)ctx, (byte)output.Length, keyPtr, (uint)key.Length) != 0)
                {
                    return ERRNO.E_FAIL;
                }
            }

            //initialize the context for the stream with hmac key
            fixed (byte* keyPtr = &MemoryMarshal.GetReference(data))
            {
                //Update with data
                if (library.Functions.Blake2Update((IntPtr)ctx, keyPtr, (uint)data.Length) != 0)
                {
                    return ERRNO.E_FAIL;
                }
            }

            //Finalize/copy the current hash
            fixed (byte* hashPtr = &MemoryMarshal.GetReference(output))
            {
                if (library.Functions.Blake2Final((IntPtr)ctx, hashPtr, (uint)output.Length) != 0)
                {
                    return ERRNO.E_FAIL;
                }
            }

            //Clear the context before returning
            MemoryUtil.InitializeBlock((byte*)ctx, contextSize);

            return output.Length;
        }

        const int ERR_NULL_PTR = -1;
        const int ERR_HASH_LEN_INVLID = -16;
        const int ERR_KEY_LEN_INVALID = -17;
        const int ERR_KEY_PTR_INVALID = -18;

        private static void ThrowOnBlake2Error(int result)
        {
            switch (result)
            {
                //Success
                case 0:
                    break;
                //Null pointer
                case ERR_NULL_PTR:
                    throw new ArgumentException("An illegal null pointer was passed to the function");
                //Invalid hash length
                case ERR_HASH_LEN_INVLID:
                    throw new ArgumentOutOfRangeException("hashLen", "The hash length is invalid");
                //Invalid key length
                case ERR_KEY_LEN_INVALID:
                    throw new ArgumentOutOfRangeException("keyLen", "The key length is invalid");

                case ERR_KEY_PTR_INVALID:
                    throw new ArgumentException("The key pointer is null");

                default:
                    throw new Exception($"An unknown error occured while hashing: {result}");

            }
        }

        private sealed class Blake2Stream : SafeHandle, IHashStream, IHmacStream
        {
            private readonly MonoCypherLibrary _library;
            private readonly IUnmangedHeap _heap;

            ///<inheritdoc/>
            public override bool IsInvalid => handle == IntPtr.Zero;

            internal Blake2Stream(MonoCypherLibrary library, IUnmangedHeap heap, byte hashSize) :base(IntPtr.Zero, true)
            {
                Debug.Assert(hashSize > 0 && hashSize <= MaxHashSize, "Hash size must be between 1 and 64 inclusive");
                Debug.Assert(library != null, "Library argument passed to internal blake2 stream constructur is null");
                _library = library;
                _heap = heap;
                HashSize = hashSize;
            }

            internal void Initialize()
            {
                //Make sure context is initialized
                InitContextHandle();

                //Init non-hmac
                int initResult = _library.Functions.Blake2Init(handle, HashSize);
                ThrowOnBlake2Error(initResult);
            }

            ///<inheritdoc/>
            public byte HashSize { get; }

            ///<inheritdoc/>
            public int MaxKeySize => MaxKeySize;

            ///<inheritdoc/>
            public void Flush(ref byte hashOut, byte hashSize)
            {
                this.ThrowIfClosed();

                if(Unsafe.IsNullRef(ref hashOut))
                {
                    throw new ArgumentNullException(nameof(hashOut));
                }

                //Guard for hash size
                if(hashSize != HashSize)
                {
                    throw new ArgumentException("The hash output must be the configured hash size", nameof(hashSize));
                }
                //get the address of our context and the hash output reference
               
                fixed(byte* hashOutPtr = &hashOut)
                {
                    _library.Functions.Blake2Final(handle, hashOutPtr, hashSize);
                }
            }

            ///<inheritdoc/>
            public void Initialize(ref byte key, byte keySize)
            {
                if (Unsafe.IsNullRef(ref key))
                {
                    throw new ArgumentNullException(nameof(key));
                }

                if(keySize > MaxKeySize)
                {
                    throw new ArgumentOutOfRangeException(nameof(keySize), $"The key size must be between 1 and {MaxKeySize} inclusive bytes");
                }

                //Make sure context is initialized
                InitContextHandle();

                //initialize the context for the stream with hmac key
                fixed (byte* keyPtr = &key)
                {
                    int result = _library.Functions.Blake2Init(handle, HashSize, keyPtr, keySize);
                    ThrowOnBlake2Error(result);
                }
            }

            ///<inheritdoc/>
            public void Update(ref byte mRef, uint mSize)
            {
                this.ThrowIfClosed();

                if (Unsafe.IsNullRef(ref mRef))
                {
                    throw new ArgumentNullException(nameof(mRef));
                }

                if (mSize == 0)
                {
                    return;
                }

                //get the address of the message reference
                fixed (byte* message = &mRef)
                {
                    int result = _library.Functions.Blake2Update(handle, message, mSize);
                    ThrowOnBlake2Error(result);
                }
            }

            private void InitContextHandle()
            {
                if (IsClosed)
                {
                    throw new ObjectDisposedException(nameof(Blake2Stream));
                }

                //alloc buffer on the heap if not allocated
                if (handle == IntPtr.Zero)
                {
                    handle = _heap.Alloc(1, _library.Functions.Blake2GetContextSize(), true);
                }
            }

            ///<inheritdoc/>
            protected override bool ReleaseHandle() => _heap.Free(ref handle);
        }
    }
}