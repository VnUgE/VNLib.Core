/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: UnsafeMemoryHandleTest.cs 
*
* UnsafeMemoryHandleTest.cs is part of VNLib.UtilsTests which is part of the larger 
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
using System.Buffers;
using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory.Tests
{

    [TestClass]
    public class UnsafeMemoryHandleTest
    {

        [TestMethod]
        public unsafe void MemoryHandleAllocLongExtensionTest()
        {
            Assert.AreEqual(8, sizeof(nuint));

            //Check for negatives
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.UnsafeAlloc(- 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.UnsafeAlloc<long>(-1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.UnsafeAlloc<byte>(MemoryUtil.Shared, -1));
        }

        [TestMethod]
        public unsafe void BasicMemoryHandleTest()
        {
            using UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAlloc(128, true);

            Assert.AreEqual(128ul, handle.Length);
            Assert.AreEqual(128, handle.IntLength);
            Assert.AreEqual(128, handle.AsSpan().Length);
            Assert.AreEqual(128, handle.AsSpan(0, 128).Length);

            Assert.AreEqual(0, handle.AsSpan(128, 0).Length);
            Assert.AreEqual(128, handle.AsSpan(0, 128).Length);

            //Test mid length span
            Assert.AreEqual(64, handle.AsSpan(64, 64).Length);
            Assert.AreEqual(68, handle.AsSpan(60, 68).Length);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(129));

            //Check span against base pointer deref

            handle.Span[120] = 10;

            Assert.AreEqual(10, handle.GetOffsetRef(120));
        }
      
        [TestMethod]
        public unsafe void MemoryHandleExtensionsTest()
        {
            using UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAlloc<byte>(1024);

            Assert.AreEqual(1024u, handle.Length);
            Assert.AreEqual(1024, handle.IntLength);

            Assert.IsTrue(handle.AsSpan(1024).IsEmpty);
            Assert.IsTrue(handle.AsSpan(1024, 0).IsEmpty);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(1025));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetOffsetRef(1025));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetOffsetByteRef(1025));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(1024, 1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(512, 513));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(513, 512));
        }

        [TestMethod]
        public unsafe void TestNegativeInputs()
        {
            using UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAlloc<byte>(1024);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(-1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetOffsetByteRef(-1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(-1, 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(-1, 0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(1, -1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(0, -1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(-1, -1));
        }

        [TestMethod]
        public unsafe void EmptyHandleTest()
        {
            //Full ref to mhandle check status
            using UnsafeMemoryHandle<byte> mHandle = new();

            //Some members should not throw
            _ = mHandle.IntLength;
            _ = mHandle.Length;
            _ = mHandle.GetIntLength();
            _ = mHandle.AsSpan(0);

            Assert.IsTrue(mHandle.Span == Span<byte>.Empty); 
            Assert.IsTrue(mHandle.AsSpan(0).IsEmpty);
            Assert.IsTrue(Unsafe.IsNullRef(ref mHandle.GetReference()));

            //Pin should throw
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = mHandle.Pin(0));          
        }
    }
}
