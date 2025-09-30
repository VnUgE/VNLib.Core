/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: MCPasswordModule.cs 
*
* MCPasswordModule.cs is part of VNLib.Hashing.Portable which is part of the larger 
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

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing.Native.MonoCypher
{

    /// <summary>
    /// Provides argon2 hashing functionality using the MonoCypher library.
    /// <para>
    /// <seealso href="https://monocypher.org/manual/argon2"/>
    /// </para>
    /// </summary>
    public static unsafe class MCPasswordModule
    {

        const uint MC_ARGON2_WA_MEM_ALIGNMENT = sizeof(ulong);
        const ulong MC_BLOCK_MULTIPLIER = 1024UL;

        [SafeMethodName("Argon2CalcWorkAreaSize")]
        internal delegate uint Argon2CalcWorkArea(Argon2Context* context);

        [SafeMethodName("Argon2ComputeHash")]
        internal delegate Argon2_ErrorCodes Argon2Hash(Argon2Context* context, void* workArea);


        /// <summary>
        /// Creates a new <see cref="IArgon2Library"/> instance using the provided <paramref name="heap"/>.
        /// </summary>
        /// <param name="Library"></param>
        /// <param name="heap">The heap to allocate internal buffers from</param>
        /// <returns>The <see cref="IArgon2Library"/> wrapper instance</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IArgon2Library Argon2CreateLibrary(this MonoCypherLibrary Library, IUnmanagedHeap heap)
        {
            //Validate arguments
            ArgumentNullException.ThrowIfNull(Library);
            ArgumentNullException.ThrowIfNull(heap);
            return new Argon2HashLib(Library, heap);
        }

        private static void Hash(this MonoCypherLibrary library, IUnmanagedHeap heap, Argon2Context* context)
        {
            ArgumentNullException.ThrowIfNull(library);
            ArgumentNullException.ThrowIfNull(heap);

            //Validate context
            ValidateContext(context);

            CalcWorkAreaSize(context, out uint elements, out uint alignment);

            //Allocate work area
            IntPtr workArea = heap.Alloc(elements, alignment, true);

            try
            {
                //Compute the hash using the allocated work area
                Argon2_ErrorCodes result = library.Functions.Argon2Hash(context, workArea.ToPointer());
                VnArgon2.ThrowOnArgonErr(result);
            }
            finally
            {
                //Free work area
                bool free = heap.Free(ref workArea);
                Debug.Assert(free, "Failed to free work area pointer after allocation");
            }
        }

        /*
         * Since unmanaged heaps are being utilized and they support alignment args, we can compute
         * a proper alignment value and element count for the work area that best matches the native
         * impl.
         * 
         * Currently the blocks are broken into a struct array of 128 u64 elements. So currently
         * using sizeof(ulong) as the alignment value so we can pass that to the heap for better 
         * alignment
         */

        private static void CalcWorkAreaSize(Argon2Context* ctx, out uint elements, out uint alignment)
        {           
            ulong size = ctx->m_cost * MC_BLOCK_MULTIPLIER;

            //Calculate element size after alignment
            elements = checked((uint)(size / MC_ARGON2_WA_MEM_ALIGNMENT));
            alignment = MC_ARGON2_WA_MEM_ALIGNMENT;

            //Sanity check
            Debug.Assert(((ulong)elements * (ulong)alignment) == size);
        }

        private static void ValidateContext(Argon2Context* context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context->outptr == null || context->outlen == 0)
            {
                throw new ArgumentException("Output buffer is null or empty");
            }

            if (context->pwd == null || context->pwdlen == 0)
            {
                throw new ArgumentException("Password buffer is null or empty");
            }

            //Salt may be null if saltlen is 0
            if (context->salt == null && context->saltlen != 0)
            {
                throw new ArgumentException("Salt buffer is null or empty");
            }
        }

        private sealed record class Argon2HashLib(MonoCypherLibrary Library, IUnmanagedHeap BufferHeap) : IArgon2Library
        {

            ///<inheritdoc/>
            public int Argon2Hash(IntPtr context)
            {
                ArgumentNullException.ThrowIfNull((void*)context);

                //Invoke hash with argon2 context pointer
                Hash(Library, BufferHeap, (Argon2Context*)context);
                return 0;
            }
        }
    }

}