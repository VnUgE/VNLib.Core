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
                Assert.AreEqual((byte)((i % 251) + 1), buffer[i + 1],
                    $"Offset-1 forward overlap failed at index {i + 1}");
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
                MemoryUtil.Memmove(in buffer[0], 0, ref buffer[4], 0, 16);

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
