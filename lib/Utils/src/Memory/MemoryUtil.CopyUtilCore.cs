/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: MemoryUtil.CopyUtilCore.cs 
*
* MemoryUtil.CopyUtilCore.cs is part of VNLib.Utils which is part
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using VNLib.Utils.Resources;

namespace VNLib.Utils.Memory
{

    public static unsafe partial class MemoryUtil
    {
        private static class CopyUtilCore
        {
            /// <summary>
            /// Determines whether two memory regions of the same size overlap.
            /// </summary>
            /// <param name="src">A reference to the start of the source region</param>
            /// <param name="dst">A reference to the start of the destination region</param>
            /// <param name="size">The size of both regions in bytes</param>
            /// <returns><c>true</c> if the regions overlap; otherwise <c>false</c></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool RegionsOverlap(ref readonly byte src, ref readonly byte dst, nuint size)
            {
                nint offset = Unsafe.ByteOffset(in src, in dst);
                nuint absOffset = offset >= 0 ? (nuint)offset : (nuint)(-offset);
                return absOffset < size;
            }

            /*
             * The following function allows callers to determine if a memmove 
             * operation may require pinning memory to complete a copy operation.
             * 
             * If known ahead of time, the caller may be able to optimize the 
             * pinning mechanism to avoid the GC overhead of pinning memory.
             * 
             * The caller will then pass pointers as references to the memmove
             * function that may fix pointers in memory.
             */

            /// <summary>
            /// Determines if the given block size to copy will require memory pinning.
            /// </summary>
            /// <param name="byteSize">The number of bytes to copy in a memmove operation</param>
            /// <returns>A value that indicates if pinning will be required</returns>

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool RequiresPinning(nuint byteSize)
                => byteSize > uint.MaxValue && !ReflectedInternalMemmove.IsSupported;

            /*
             * Why does this function exist? For centralized memmove operations primarily.
             * 
             * When the block is known to be small, all of the branches in memmove can be
             * alot of overhead including the possibility of Avx2 being used for really 
             * small blocks if they are sized correctly. If the block is known to be small, we
             * can just skip all of that and use the fastest method for small blocks,
             * which is currently the Unsafe.CopyBlock method. It is intrinsic to 
             * the CLR at the moment.
             */

            /// <summary>
            /// Copies a known small block of memory from one location to another,
            /// as fast as possible. Hardware acceleration is not used.
            /// </summary>
            /// <param name="srcByte">A reference to the first byte in the source sequence</param>
            /// <param name="dstByte">A reference to the first byte in the target sequence</param>
            /// <param name="byteCount">The number of bytes to copy</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void SmallMemmove(ref readonly byte srcByte, ref byte dstByte, uint byteCount)
            {
                Debug.Assert(!Unsafe.IsNullRef(in srcByte), "Null source reference passed to MemmoveByRef");
                Debug.Assert(!Unsafe.IsNullRef(in dstByte), "Null destination reference passed to MemmoveByRef");

                Unsafe.CopyBlockUnaligned(ref dstByte, in srcByte, byteCount);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public static void Memmove(ref readonly byte srcByte, ref byte dstByte, nuint byteCount)
            {
                Debug.Assert(!Unsafe.IsNullRef(in srcByte), "Null source reference passed to MemmoveByRef");
                Debug.Assert(!Unsafe.IsNullRef(in dstByte), "Null destination reference passed to MemmoveByRef");

                if (byteCount == 1)
                {
                    dstByte = srcByte;
                }
                // Prefer vector copy if non-overlapping & backend supports the copy
                else if (Vector256Copy.CanAccelerate(byteCount) && !RegionsOverlap(in srcByte, in dstByte, byteCount))
                {
                    Vector256Copy.Copy(in srcByte, ref dstByte, byteCount);
                }
                else if (ReflectedInternalMemmove.IsSupported)   // always prefer memmove when available
                {
                    ReflectedInternalMemmove.Memmove(in srcByte, ref dstByte, byteCount);
                }
                //Check for 64bit copy (should get optimized away when sizeof(nuint == uint) aka 32bit platforms)
                else if (byteCount > uint.MaxValue)
                {
                    /*
                     * At the moment, .NET's Buffer.MemoryCopy just calls Memmove internally
                     * by passing pointers by reference. So it's a fallback to avoid pinning.
                     * Memmove will pin internally if it has to fall back to the PInvoke.
                     * 
                     * Anyway, the point with the reflected version is to avoid pinning, 
                     * unless completely necessary, so it should be available on most 
                     * .NET 8.0 supported platforms, but this is fallback incase it's not.
                     * 
                     */

                    fixed (byte* srcPtr = &srcByte, dstPtr = &dstByte)
                    {
                        Buffer.MemoryCopy(srcPtr, dstPtr, byteCount, byteCount);
                    }
                }
                else
                {
                    SmallMemmove(in srcByte, ref dstByte, checked((uint)byteCount));
                }
            }

            private static class ReflectedInternalMemmove
            {
                /*
                * Dirty little trick to access internal Buffer.Memmove method for 
                * large references. May not always be supported, so optional safe
                * guards are in place.
                */
                private delegate void BigMemmove(ref byte dest, ref readonly byte src, nuint len);
                private static readonly BigMemmove? _clrMemmove = ManagedLibrary.TryGetStaticMethod<BigMemmove>(typeof(Buffer), "Memmove", BindingFlags.NonPublic);

                //Cache features flags
                public static readonly bool IsSupported = _clrMemmove != null;

                ///<inheritdoc/>
                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
                public static void Memmove(ref readonly byte src, ref byte dst, nuint byteCount)
                {
                    Debug.Assert(_clrMemmove != null, "Memmove delegate is null and flags assumed is was supported");
                    _clrMemmove!.Invoke(ref dst, in src, byteCount);
                }
            }

            private static class Vector256Copy
            {
                private const nuint _alignment = 0x20u; // sizeof(Vector256<byte>)
                private static readonly bool _supported = Vector256.IsHardwareAccelerated;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static bool CanAccelerate(nuint size) => _supported && size >= _alignment;

                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
                public static void Copy(ref readonly byte src, ref byte dst, nuint byteCount)
                {
                    const nuint Unroll    = 4;  // Matches the number of vectors unrolled in the main loop.
                    nuint ptrOffset       = 0;
                    nuint totalVectors    = byteCount / _alignment;
                    nuint unrolledVectors = totalVectors / Unroll;
                    nuint remainingVecs   = totalVectors % Unroll;

                    // Unrolled main loop: 4 vectors per iteration
                    for (nuint i = 0; i < unrolledVectors; i++)
                    {
                        Vector256<byte> v0 = Vector256.LoadUnsafe(in src, ptrOffset);
                        Vector256<byte> v1 = Vector256.LoadUnsafe(in src, ptrOffset + _alignment);
                        Vector256<byte> v2 = Vector256.LoadUnsafe(in src, ptrOffset + _alignment * 2);
                        Vector256<byte> v3 = Vector256.LoadUnsafe(in src, ptrOffset + _alignment * 3);

                        Vector256.StoreUnsafe(v0, ref dst, ptrOffset);
                        Vector256.StoreUnsafe(v1, ref dst, ptrOffset + _alignment);
                        Vector256.StoreUnsafe(v2, ref dst, ptrOffset + _alignment * 2);
                        Vector256.StoreUnsafe(v3, ref dst, ptrOffset + _alignment * 3);

                        ptrOffset += _alignment * Unroll;
                    }

                    // Handle 0..3 remaining aligned vectors
                    for (nuint r = 0; r < remainingVecs; r++)
                    {
                        Vector256<byte> v = Vector256.LoadUnsafe(in src, ptrOffset);
                        Vector256.StoreUnsafe(v, ref dst, ptrOffset);

                        ptrOffset += _alignment;
                    }

                    // Overlapping tail covers any sub-32-byte remainder
                    if ((byteCount & (_alignment - 1)) != 0)
                    {
                        nuint tailOffset = byteCount - _alignment;

                        Vector256<byte> tail = Vector256.LoadUnsafe(in src, tailOffset);
                        Vector256.StoreUnsafe(tail, ref dst, tailOffset);
                    }
                }
            }
        }
    }
}
