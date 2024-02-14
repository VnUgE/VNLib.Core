/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Reflection;
using System.Diagnostics;
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
            const nuint _avx32ByteAlignment = 0x20u;

            private static readonly ReflectedInternalMemmove _reflectedMemmove = new();
            private static readonly FallbackUnsafeMemmove _fallbackMemmove = new();
            private static readonly FallbackBufferCopy _bufferCopy = new();
            private static readonly AvxCopyStrategy _avxCopy = new();

            /// <summary>
            /// Gets a value that indicates if the platform supports hardware 
            /// acceleration for memmove operations.
            /// </summary>
            public static readonly bool IsHwAccelerationSupported = _avxCopy.Features.HasFlag(CopyFeatures.HwAccelerated);

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
                 * Pinnin is required, if reflected memmove is not supported on the platform
                 * AND the size of the data to copy is larger than 32 bit width.
                 * 
                 * Otherwise if accleration is forced, pinning will always be required.
                 */

                if(byteSize > uint.MaxValue && _reflectedMemmove.Features == CopyFeatures.NotSupported)
                {
                    return true;
                }

                if((forceAcceleration || Is32ByteAligned(byteSize)) && _avxCopy.Features != CopyFeatures.NotSupported)
                {
                    return true;
                }

                return false;
            }

            /*
             * Why does this function exist. For centralized memmove operations primarily.
             * 
             * When the block is known to be small, all of the brances in memmove can be
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

                _fallbackMemmove.Memmove(in srcByte, ref dstByte, byteCount);
                return;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public static void Memmove(ref readonly byte srcByte, ref byte dstByte, nuint byteCount, bool forceAcceleration)
            {
                Debug.Assert(!Unsafe.IsNullRef(in srcByte), "Null source reference passed to MemmoveByRef");
                Debug.Assert(!Unsafe.IsNullRef(in dstByte), "Null destination reference passed to MemmoveByRef");

                //Check for 64bit copy
                if(byteCount > uint.MaxValue)
                {
                    //We need a 64bit copy strategy
                    if(forceAcceleration || Is32ByteAligned(byteCount))
                    {
                        //Must be supported
                        if(_avxCopy.Features != CopyFeatures.NotSupported)
                        {
                            //Copy
                            _avxCopy.Memmove(in srcByte, ref dstByte, byteCount);
                            return;
                        }
                    }

                    //try reflected memove incase it supports 64bit blocks
                    if(_reflectedMemmove.Features != CopyFeatures.NotSupported)
                    {
                        //Copy
                        _reflectedMemmove.Memmove(in srcByte, ref dstByte, byteCount);
                        return;
                    }

                    //Fallback to buffer copy, caller should have used pinning
                    _bufferCopy.Memmove(in srcByte, ref dstByte, byteCount);
                    return;
                }

                //32byte copy

                //Try hardware acceleration if supported and aligned
                if ((forceAcceleration || Is32ByteAligned(byteCount)) && _avxCopy.Features != CopyFeatures.NotSupported)
                {
                    //Copy
                    _avxCopy.Memmove(in srcByte, ref dstByte, byteCount);
                    return;
                }

                //fallback to unsafe.copy on 32bit copy
                _fallbackMemmove.Memmove(in srcByte, ref dstByte, byteCount);
                return;
            }

            /// <summary>
            /// Determines if the given size 32-byte aligned
            /// </summary>
            /// <param name="size">The block size to test</param>
            /// <returns>A value that indicates if the block size is 32byte aligned</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool Is32ByteAligned(nuint size) => unchecked(size % _avx32ByteAlignment) == 0;

            private enum CopyFeatures
            {
                None = 0,
                NotSupported = 1,
                Supports64Bit = 2,
                HwAccelerated = 4
            }

            private interface ICopyStrategy
            {
                CopyFeatures Features { get; }

                void Memmove(ref readonly byte src, ref byte dst, nuint byteCount);
            }

            private sealed class ReflectedInternalMemmove : ICopyStrategy
            {
                /*
                * Dirty little trick to access internal Buffer.Memmove method for 
                * large references. May not always be supported, so optional safe
                * guards are in place.
                */
                private delegate void BigMemmove(ref byte dest, ref readonly byte src, nuint len);
                private static readonly BigMemmove? _clrMemmove = ManagedLibrary.TryGetStaticMethod<BigMemmove>(typeof(Buffer), "Memmove", BindingFlags.NonPublic);

                //Cache features flags
                private readonly CopyFeatures _features = _clrMemmove is null ? CopyFeatures.NotSupported : CopyFeatures.Supports64Bit;

                ///<inheritdoc/>
                public CopyFeatures Features => _features;

                ///<inheritdoc/>
                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
                public void Memmove(ref readonly byte src, ref byte dst, nuint byteCount)
                {
                    Debug.Assert(_clrMemmove != null, "Memmove delegate is null and flags assumed is was supported");
                    _clrMemmove!.Invoke(ref dst, in src, byteCount);
                }
            }

            private sealed class FallbackUnsafeMemmove : ICopyStrategy
            {
                ///<inheritdoc/>
                public CopyFeatures Features => CopyFeatures.None;

                ///<inheritdoc/>
                public void Memmove(ref readonly byte src, ref byte dst, nuint byteCount)
                {
                    Debug.Assert(byteCount < uint.MaxValue, "Byte count must be less than uint.MaxValue and flags assumed 64bit blocks were supported");
                    Unsafe.CopyBlock(ref dst, in src, (uint)byteCount);
                }
            }

            private sealed class FallbackBufferCopy : ICopyStrategy
            {
                /*
                 * This class is considered a fallback because it require's a fixed
                 * statment to get a pointer from a reference. This should avoided
                 * unless pinning happens and pointers are converted to references.
                 * 
                 * Then it is a zero cost fixed statment capturing pointers from references
                 * that were already pinned.
                 */

                ///<inheritdoc/>
                public CopyFeatures Features => CopyFeatures.Supports64Bit;

                ///<inheritdoc/>
                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
                public void Memmove(ref readonly byte src, ref byte dst, nuint byteCount)
                {
                    /*
                     * We assume that the references passed are not null and are 
                     * already pinned, so this statement is zero cost.
                     */
                    fixed (byte* srcPtr = &src, dstPtr = &dst)
                    {
                        Buffer.MemoryCopy(srcPtr, dstPtr, byteCount, byteCount);
                    }
                }
            }

            private sealed class AvxCopyStrategy : ICopyStrategy
            {
                const nuint _avx32ByteAlignment = 0x20u;

                //If avx is supported, then set 64bit flags and hw acceleration
                private readonly CopyFeatures _features = Avx2.IsSupported ? CopyFeatures.HwAccelerated | CopyFeatures.Supports64Bit : CopyFeatures.NotSupported;

                ///<inheritdoc/>
                public CopyFeatures Features => _features;

                ///<inheritdoc/>
                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
                public void Memmove(ref readonly byte src, ref byte dst, nuint byteCount)
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