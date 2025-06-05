/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Runtime.Intrinsics.X86;

using VNLib.Utils.Resources;

namespace VNLib.Utils.Memory
{

    public static unsafe partial class MemoryUtil
    {
        private static class CopyUtilCore
        {
            /// <summary>
            /// Gets a value that indicates if the platform supports hardware 
            /// acceleration for memmove operations.
            /// </summary>
            public static readonly bool IsHwAccelerationSupported = AvxCopyStrategy.Features.HasFlag(CopyFeatures.HwAccelerated);

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
            /// <param name="forceAcceleration">A value that indicates that hardware acceleration is requested</param>
            /// <returns>A value that indicates if pinning will be required</returns>

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool RequiresPinning(nuint byteSize, bool forceAcceleration)
            {
                /*
                 * Pinning is required, if reflected memmove is not supported on the platform
                 * AND the size of the data to copy is larger than 32 bit width.
                 * 
                 * Otherwise if accleration is forced, pinning will always be required.
                 */

                if (byteSize > uint.MaxValue && ReflectedInternalMemmove.Features == CopyFeatures.NotSupported)
                {
                    return true;
                }

                if (forceAcceleration || AvxCopyStrategy.CanAccelerate(byteSize))
                {
                    return true;
                }

                return false;
            }

            /*
             * Why does this function exist? For centralized memmove operations primarily.
             * 
             * When the block is known to be small, all of the branches in memmove can be
             * alot of overhead including the possability of Avx2 being used for really 
             * small blocks if they are aligned. If the block is known to be small, we
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
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public static void SmallMemmove(ref readonly byte srcByte, ref byte dstByte, uint byteCount)
            {
                Debug.Assert(!Unsafe.IsNullRef(in srcByte), "Null source reference passed to MemmoveByRef");
                Debug.Assert(!Unsafe.IsNullRef(in dstByte), "Null destination reference passed to MemmoveByRef");

                Unsafe.CopyBlock(ref dstByte, in srcByte, byteCount);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public static void Memmove(ref readonly byte srcByte, ref byte dstByte, nuint byteCount, bool forceAcceleration)
            {
                Debug.Assert(!Unsafe.IsNullRef(in srcByte), "Null source reference passed to MemmoveByRef");
                Debug.Assert(!Unsafe.IsNullRef(in dstByte), "Null destination reference passed to MemmoveByRef");

                // Always try to accelerate if the caller has requested it
                if (forceAcceleration || AvxCopyStrategy.CanAccelerate(byteCount))
                {
                    AvxCopyStrategy.Memmove(in srcByte, ref dstByte, byteCount);
                    return;
                }

                //Check for 64bit copy (should get optimized away when sizeof(nuint == uint) aka 32bit platforms)
                if (byteCount > uint.MaxValue)
                {
                    //try reflected memove incase it supports 64bit blocks
                    if (ReflectedInternalMemmove.Features != CopyFeatures.NotSupported)
                    {
                        ReflectedInternalMemmove.Memmove(in srcByte, ref dstByte, byteCount);
                        return;
                    }

                    /*
                     * At the moment, .NET's Buffer.MemoryCopy just calls Memmove internally
                     * by passing pointers by reference. So it's a fallback to avoid pinning.
                     * Memmove will pin internally if it has to fall back to the PInvoke.
                     * 
                     * Anyway, the point with the reflected version is to avoid pinning, 
                     * unless completly necessary, so it should be available on most 
                     * .NET 8.0 supported platforms, but this is fallback incase it's not.
                     * 
                     */

                    fixed (byte* srcPtr = &srcByte, dstPtr = &dstByte)
                    {
                        Buffer.MemoryCopy(srcPtr, dstPtr, byteCount, byteCount);
                    }

                    return;
                }

                //fallback to unsafe.copy on 32bit copy
                SmallMemmove(in srcByte, ref dstByte, (uint)byteCount);
                return;
            }        

            private enum CopyFeatures
            {
                None = 0,
                NotSupported = 1,
                Supports64Bit = 2,
                HwAccelerated = 4
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
                public static readonly CopyFeatures Features = _clrMemmove is null ? CopyFeatures.NotSupported : CopyFeatures.Supports64Bit;

                ///<inheritdoc/>
                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
                public static void Memmove(ref readonly byte src, ref byte dst, nuint byteCount)
                {
                    Debug.Assert(_clrMemmove != null, "Memmove delegate is null and flags assumed is was supported");
                    _clrMemmove!.Invoke(ref dst, in src, byteCount);
                }
            }         

            private sealed class AvxCopyStrategy 
            {
                const nuint _avx32ByteAlignment = 0x20u;

                //If avx is supported, then set 64bit flags and hw acceleration
                public static readonly CopyFeatures Features = Avx2.IsSupported ? CopyFeatures.HwAccelerated | CopyFeatures.Supports64Bit : CopyFeatures.NotSupported;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static bool CanAccelerate(nuint size) 
                    => unchecked(size % _avx32ByteAlignment) == 0 && (Features & CopyFeatures.HwAccelerated) > 0;

                ///<inheritdoc/>
                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
                public static void Memmove(ref readonly byte src, ref byte dst, nuint byteCount)
                {
                    Debug.Assert(Avx2.IsSupported, "AVX2 is not supported on this platform");
                    Debug.Assert(_avx32ByteAlignment == (nuint)Vector256<byte>.Count, "AVX2 vector size is not 32 bytes");

                    //determine the number of loops
                    nuint loopCount = byteCount / _avx32ByteAlignment;

                    //Remaining bytes if not exactly 32 byte aligned
                    nuint remaingBytes = byteCount % _avx32ByteAlignment;

                    fixed (byte* srcPtr = &src, dstPtr = &dst)
                    {
                        //local mutable copies
                        byte* srcOffset = srcPtr;
                        byte* dstOffset = dstPtr;

                        for (nuint i = 0; i < loopCount; i++)
                        {
                            //avx vector load
                            Vector256<byte> srcVector = Avx.LoadVector256(srcOffset);
                            Avx.Store(dstOffset, srcVector);

                            //Upshift pointers
                            srcOffset += _avx32ByteAlignment;
                            dstOffset += _avx32ByteAlignment;
                        }

                        //finish copy manually since it will always be less than 32 bytes
                        for (nuint i = 0; i < remaingBytes; i++)
                        {
                            dstOffset[i] = srcOffset[i];
                        }
                    }
                }
            }
        }
    }
}
