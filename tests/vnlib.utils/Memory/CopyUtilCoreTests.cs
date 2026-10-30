/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: CopyUtilCoreTests.cs 
*
* CopyUtilCoreTests.cs is part of VNLib.UtilsTests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.UtilsTests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.UtilsTests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of 
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.UtilsTests. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Runtime.Intrinsics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Utils.Memory.Tests
{
    /// <summary>
    /// Tests for <see cref="MemoryUtil"/> memmove and copy operations,
    /// focusing on overlapping region handling and vector-sized boundary
    /// conditions.
    /// </summary>
    [TestClass]
    public class CopyUtilCoreTests
    {
        /// <summary>
        /// Tests that memmove with zero length is a no-op and does not
        /// corrupt surrounding memory.
        /// </summary>
        [TestMethod]
        public void Memmove_ZeroLength_IsNoOp()
        {
            Span<byte> buffer = new byte[64];

            FillPattern(buffer);

            MemoryUtil.Memmove(in buffer[0], 0, ref buffer[0], 0, 0);

            AssertPattern(buffer);
        }

        /// <summary>
        /// Tests that memmove is a no-op when source and destination
        /// are the same region (identity overlap).
        /// </summary>
        [TestMethod]
        public void Memmove_OverlappingIdentity_IsNoOp()
        {
            // Test with managed references
            {
                Span<byte> buffer = new byte[64];
                TestPattern(buffer);
            }

            // Test with unmanaged references
            {
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(64, true);
                TestPattern(buffer.Span);
            }

            // Test with stack buffers
            {
                Span<byte> buffer = stackalloc byte[64];
                TestPattern(buffer);
            }

            static void TestPattern(Span<byte> buffer)
            {
                FillPattern(buffer);

                // Copy bytes [8..23] onto themselves
                MemoryUtil.Memmove(in buffer[8], 0, ref buffer[8], 0, 16);

                AssertPattern(buffer);
            }
        }

        /// <summary>
        /// Tests memmove with a near-full forward overlap (offset of 1),
        /// which is the most extreme overlapping scenario for forward copies.
        /// </summary>
        [TestMethod]
        public void Memmove_OverlappingForward_OffsetOne()
        {
            Span<byte> buffer = new byte[64];
            FillPattern(buffer, 0, 32);

            // Copy bytes [0..31] to [1..32] — offset of 1
            MemoryUtil.Memmove(in buffer[0], 0, ref buffer[0], 1, 32);

            Assert.AreEqual((byte)1, buffer[1], "First byte wrong after offset-1 shift");

            for (int i = 0; i < 32; i++)
            {
                Assert.AreEqual(
                    expected:(byte)((i % 251) + 1), 
                    actual: buffer[i + 1],
                    $"Offset-1 forward overlap failed at index {i + 1}"
                );
            }
        }

        /// <summary>
        /// Tests that memmove correctly copies data when the destination
        /// overlaps the source with a forward offset (dst > src). This is
        /// the classic overlapping scenario where a naive forward copy would
        /// corrupt source data before it is read.
        /// </summary>
        [TestMethod]
        public void Memmove_OverlappingForward_ShiftsCorrectly()
        {
            {
                Span<byte> buffer = new byte[64];
                CopyAndAssert(buffer);
            }

            {
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(64, true);
                CopyAndAssert(buffer.Span);
            }

            {
                Span<byte> buffer = stackalloc byte[64];
                CopyAndAssert(buffer);
            }

            static void CopyAndAssert(Span<byte> buffer)
            {
                FillPattern(buffer, 0, 32);

                // Copy bytes [0..15] to [4..19] — forward overlap, dst > src
                MemoryUtil.Memmove(in buffer[0], 0, ref buffer[0], 4, 16);

                // Verify the shifted region contains the original values
                for (int i = 0; i < 16; i++)
                {
                    Assert.AreEqual((byte)(i + 1), buffer[i + 4], $"Forward overlap failed at index {i + 4}");
                }

                // Verify source prefix was preserved (not corrupted by forward copy)
                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual((byte)(i + 1), buffer[i], $"Source prefix corrupted at index {i}");
                }
            }
        }

        /// <summary>
        /// Tests that memmove correctly copies data when the source
        /// overlaps the destination with a backward offset (src > dst). This
        /// tests that the overlap detection routes to a backward-safe copy
        /// rather than the vector fast path.
        /// </summary>
        [TestMethod]
        public void Memmove_OverlappingBackward_ShiftsCorrectly()
        {
            {
                Span<byte> buffer = new byte[64];
                CopyAndAssert(buffer);
            }

            {
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(64, true);
                CopyAndAssert(buffer.Span);
            }

            {
                Span<byte> buffer = stackalloc byte[64];
                CopyAndAssert(buffer);
            }

            static void CopyAndAssert(Span<byte> buffer)
            {
                // Init bytes [16..47] with pattern, rest zero
                FillPattern(buffer, 16, 32);

                // Copy bytes [20..35] to [16..31] — backward overlap, src > dst
                MemoryUtil.Memmove(in buffer[0], 20, ref buffer[0], 16, 16);

                for (int i = 0; i < 16; i++)
                {
                    Assert.AreEqual((byte)(i + 21), buffer[16 + i], $"Backward overlap failed at index {16 + i}");
                }

                // Verify suffix beyond destination was not corrupted
                for (int i = 32; i < 48; i++)
                {
                    Assert.AreEqual((byte)((i % 251) + 1), buffer[i], $"Suffix corrupted at index {i}");
                }
            }
        }

        /// <summary>
        /// Tests that memmove produces correct results at vector boundary
        /// sizes that exercise different internal dispatch paths:
        /// <list type="bullet">
        ///   <item>1, 15 — scalar / SmallMemmove path</item>
        ///   <item>16, 31 — SmallMemmove path</item>
        ///   <item>32, 33, 47 — Vector256 single-transfer and overlapping tail</item>
        ///   <item>64, 128, 256, 4096 — Vector256 unrolled loop path</item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void Memmove_VectorBoundarySizes_CopiesCorrectly()
        {
            if (!Vector256.IsHardwareAccelerated)
            {
                Assert.Inconclusive("Vector256 hardware acceleration not available on this platform");
            }

            int[] sizes = [1, 15, 16, 31, 32, 33, 47, 63, 64, 128, 256, 4096];

            // Test with non-overlapped unmanaged memory buffers
            {
                using UnsafeMemoryHandle<byte> src = MemoryUtil.UnsafeAlloc(4096, true);
                using UnsafeMemoryHandle<byte> dst = MemoryUtil.UnsafeAlloc(4096, true);

                foreach (int size in sizes)
                {
                    CopyAndAssert(src.Span, dst.Span, size);
                }
            }

            // Test again with managed byte arrays that don't overlap
            {
                byte[] src = new byte[4096];
                byte[] dst = new byte[4096];

                foreach (int size in sizes)
                {
                    CopyAndAssert(src, dst, size);
                }
            }

            static void CopyAndAssert(Span<byte> src, Span<byte> dst, int size)
            {
                // Shape buffers to the desired test block size
                src = src[0..size];
                dst = dst[0..size];

                // Fill source with a deterministic pattern using a prime modulus
                FillPattern(src);

                MemoryUtil.Memmove(in src[0], 0, ref dst[0], 0, (nuint)size);

                for (int i = 0; i < size; i++)
                {
                    Assert.AreEqual(src[i], dst[i], $"Size {size}: mismatch at index {i}");
                }
            }
        }     

        /// <summary>
        /// Regression test: verifies that memmove with non-vector-aligned sizes
        /// does not overread the source or overrun the destination buffer.
        /// The Vector256Copy tail logic loads a full 32-byte vector at
        /// byteCount - 32 to cover the sub-32-byte remainder; this test uses
        /// a small, isolated buffer with guard bytes after the destination
        /// region to detect any access past the region's end.
        /// </summary>
        [TestMethod]
        public void Memmove_VectorTail_NoOverrun()
        {
            if (!Vector256.IsHardwareAccelerated)
            {
                Assert.Inconclusive("Vector256 hardware acceleration not available on this platform");
            }

            // Sizes that trigger the tail path: 33, 47, 63, 65, etc.
            int[] sizes = [33, 47, 63, 65, 95, 97, 127, 129];

            // Alloc buffers up to the max size and pad with some guard bytes
            const int buffPlusGuard = 256;

            using UnsafeMemoryHandle<byte> src = MemoryUtil.UnsafeAlloc<byte>(buffPlusGuard, true);
            using UnsafeMemoryHandle<byte> dst = MemoryUtil.UnsafeAlloc<byte>(buffPlusGuard, true);

            foreach (int size in sizes)
            {
                Span<byte> srcSpan = src.Span[..size];
                Span<byte> dstGuard = dst.Span[size..];

                FillPattern(srcSpan);

                // Fill guard with known sentinel
                FillConst(dstGuard, 0xAA);

                MemoryUtil.Memmove(
                    in src.GetReference(),
                    srcOffset: 0,
                    ref dst.GetReference(),
                    dstOffset: 0,
                    (nuint)size
                );

                // Verify copied region
                {
                    Span<byte> dstSpan = dst.Span[..size];

                    int mismatchIndex = FindMismatch(srcSpan, dstSpan);

                    Assert.AreEqual(
                        expected: -1,
                        actual: mismatchIndex,
                        $"Size {size}: data mismatch at {mismatchIndex}"
                    );
                }

                // Verify guard bytes were not touched – overrun would corrupt them
                {
                    for (int i = 0; i < dstGuard.Length; i++)
                    {
                        Assert.AreEqual(
                            expected: 0xAA, 
                            dstGuard[i], 
                            $"Size {size}: guard byte {i} corrupted – overrun detected"
                        );
                    }
                }

                // Reset full buffer for reuse
                MemoryUtil.InitializeBlock(src.Span);
                MemoryUtil.InitializeBlock(dst.Span);
            }
        }

        /// <summary>
        /// Tests that memmove handles the overlap detection boundary correctly:
        /// adjacent regions (dst = src +/- size) are not overlapping and may
        /// use the vector fast path, while regions that overlap by a single
        /// byte must use the overlap safe copy. An off-by-one in the overlap
        /// check would corrupt data in the one byte overlap cases.
        /// </summary>
        [TestMethod]
        public void Memmove_AdjacentRegions_CopiesCorrectly()
        {
            // Test with managed references
            {
                Span<byte> buffer = new byte[256];
                TestBoundaries(buffer);
            }

            // Test with unmanaged references
            {
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(256, true);
                TestBoundaries(buffer.Span);
            }

            // Test with stack buffers
            {
                Span<byte> buffer = stackalloc byte[256];
                TestBoundaries(buffer);
            }

            static void TestBoundaries(Span<byte> buffer)
            {
                // Sizes that exercise the SmallMemmove and Vector256 dispatch paths
                int[] sizes = [16, 64];

                foreach (int size in sizes)
                {
                    // Forward adjacent: dst = src + size
                    {
                        FillPattern(buffer, 0, size);

                        MemoryUtil.Memmove(in buffer[0], 0, ref buffer[0], (uint)size, (nuint)size);

                        AssertShifted(buffer, size, 0, size, "Forward adjacent copy");
                        AssertShifted(buffer, 0, 0, size, "Forward adjacent source");
                    }

                    // Backward adjacent: dst = src - size
                    {
                        FillPattern(buffer, size, size);

                        MemoryUtil.Memmove(in buffer[size], 0, ref buffer[0], 0, (nuint)size);

                        AssertShifted(buffer, 0, size, size, "Backward adjacent copy");
                        AssertShifted(buffer, size, size, size, "Backward adjacent source");
                    }

                    // One byte forward overlap: dst = src + size - 1
                    {
                        FillPattern(buffer, 0, size);

                        MemoryUtil.Memmove(in buffer[0], srcOffset: 0, ref buffer[0], (uint)(size - 1), (nuint)size);

                        AssertShifted(buffer, size - 1, 0, size, "One byte forward overlap");
                    }

                    // One byte backward overlap: dst = src - size + 1
                    {
                        FillPattern(buffer, size, size);

                        MemoryUtil.Memmove(in buffer[size], 0, ref buffer[0], 1, (nuint)size);

                        AssertShifted(buffer, 1, size, size, "One byte backward overlap");
                    }
                }

                // Verifies the region [regionStart..regionStart+count) holds the
                // pattern copied from patternStart
                static void AssertShifted(Span<byte> buffer, int regionStart, int patternStart, int count, string caseName)
                {
                    for (int i = 0; i < count; i++)
                    {
                        Assert.AreEqual(
                            expected: (byte)(((patternStart + i) % 251) + 1),
                            actual: buffer[regionStart + i],
                            $"Size {count}: {caseName} failed at index {regionStart + i}"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Tests that memmove copies correctly when the source and destination
        /// start at unaligned offsets. The vector fast path relies on unaligned
        /// vector loads and stores, so a regression to aligned vector
        /// instructions would be detected here.
        /// </summary>
        [TestMethod]
        public void Memmove_VectorUnaligned_CopiesCorrectly()
        {
            if (!Vector256.IsHardwareAccelerated)
            {
                Assert.Inconclusive("Vector256 hardware acceleration not available on this platform");
            }

            // Test with unmanaged references
            {
                using UnsafeMemoryHandle<byte> src = MemoryUtil.UnsafeAlloc<byte>(512, true);
                using UnsafeMemoryHandle<byte> dst = MemoryUtil.UnsafeAlloc<byte>(512, true);
                RunAll(src.Span, dst.Span, 3, 31);
            }

            // Test with managed byte arrays
            {
                byte[] src = new byte[512];
                byte[] dst = new byte[512];
                RunAll(src, dst, 7, 31);
            }

            // Test with stack buffers
            {
                Span<byte> src = stackalloc byte[512];
                Span<byte> dst = stackalloc byte[512];
                RunAll(src, dst, 31, 7);
            }

            // Runs the full size/offset matrix, then a mixed offset pair
            static void RunAll(Span<byte> src, Span<byte> dst, int mixedSrcOffset, int mixedDstOffset)
            {
                // Sizes that exercise the vector loop with and without a tail
                int[] sizes = [33, 64, 96, 129, 448];

                // Unaligned start offsets
                int[] offsets = [1, 3, 7, 31];

                foreach (int size in sizes)
                {
                    foreach (int offset in offsets)
                    {
                        CopyAndAssert(src, dst, offset, offset, size);
                    }
                }

                // Also with different source and destination offsets
                CopyAndAssert(src, dst, mixedSrcOffset, mixedDstOffset, 129);
            }

            static void CopyAndAssert(Span<byte> src, Span<byte> dst, int srcOffset, int dstOffset, int size)
            {
                // Pre-fill the destination with a sentinel so partial copies are detected
                FillConst(dst, 0x5A);

                // Fill the source region at the unaligned offset
                FillPattern(src, srcOffset, size);

                MemoryUtil.Memmove(
                    in src[0], 
                    (uint)srcOffset, 
                    ref dst[0], 
                    (uint)dstOffset, 
                    (nuint)size
                );

                int mismatch = FindMismatch(
                    expected: src.Slice(srcOffset, size),
                    actual: dst.Slice(dstOffset, size)
                );

                Assert.AreEqual(
                    expected: -1,
                    actual: mismatch,
                    $"Src offset {srcOffset}, dst offset {dstOffset}, size {size}: mismatch at index {srcOffset + mismatch}"
                );
            }
        }

        /// <summary>
        /// Differential test: compares memmove results against a simple, overlap
        /// safe byte by byte reference copy across a matrix of sizes and offsets
        /// spanning non-overlapping, adjacent, and overlapping regions in both
        /// directions. Any dispatch regression that corrupts data is detected
        /// here regardless of which internal path is taken.
        /// </summary>
        [TestMethod]
        public void Memmove_OverlapMatrix_MatchesReferenceMemmove()
        {
            const int BaseOffset = 256;

            // Sizes across the internal dispatch boundaries
            int[] sizes = [1, 31, 32, 33, 63, 64, 129, 512];

            // Offsets spanning non-overlapping, adjacent, and overlapping regions
            int[] offsets = [-255, -129, -65, -64, -63, -33, -32, -31, -1, 0, 1, 31, 32, 33, 63, 64, 65, 129, 255];

            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(1024, true);

            foreach (int size in sizes)
            {
                foreach (int offset in offsets)
                {
                    VerifyAgainstReference(buffer, size, offset);
                }
            }

            static void VerifyAgainstReference(UnsafeMemoryHandle<byte> buffer, int size, int offset)
            {
                // Initialize the buffer with the deterministic pattern
                FillPattern(buffer.Span);

                // Snapshot the initial state for the reference copy
                byte[] snapshot = buffer.Span.ToArray();

                // Copy under test
                MemoryUtil.Memmove(
                    in buffer.GetReference(),
                    srcOffset: BaseOffset,
                    ref buffer.GetReference(),
                    dstOffset: (nuint)(BaseOffset + offset),
                    (nuint)size
                );

                // Reference copy: the destination region receives the source's
                // original values, read from the snapshot so the copy direction
                // is irrelevant
                byte[] expected = (byte[])snapshot.Clone();

                for (int i = 0; i < size; i++)
                {
                    expected[BaseOffset + offset + i] = snapshot[BaseOffset + i];
                }

                // Find the first mismatch, if any
                int mismatch = FindMismatch(expected, buffer.Span);

                Assert.AreEqual(
                    expected: -1,
                    actual: mismatch,
                    message: $"Size {size}, offset {offset}: result differs from the reference memmove at index {mismatch}"
                );
            }
        }

        /// <summary>
        /// Finds the first index where expected and actual differ.
        /// Returns -1 when no mismatch is found in the overlapping range.
        /// </summary>
        private static int FindMismatch(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
        {
            // Compare only the overlapping range so mismatched lengths
            // report the first difference instead of throwing
            int length = Math.Min(expected.Length, actual.Length);
            int mismatch = -1;
            for (int i = 0; i < length; i++)
            {
                if (actual[i] != expected[i])
                {
                    mismatch = i;
                    break;
                }
            }

            return mismatch;
        }

        /// <summary>
        /// Fills a span with a deterministic pattern using a prime modulus.
        /// </summary>
        private static void FillPattern(Span<byte> span, int offset = 0, int? count = null)
        {
            int end = count.HasValue ? offset + count.Value : span.Length;
            for (int i = offset; i < end; i++)
            {
                span[i] = (byte)((i % 251) + 1);
            }
        }

        /// <summary>
        /// Fills a span with a constant byte value.
        /// </summary>
        private static void FillConst(Span<byte> span, byte value)
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = value;
            }
        }

        /// <summary>
        /// Asserts that a span matches the expected deterministic pattern.
        /// </summary>
        private static void AssertPattern(ReadOnlySpan<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                Assert.AreEqual((byte)((i % 251) + 1), span[i], $"Buffer corrupted at index {i}");
            }
        }
    }
}
