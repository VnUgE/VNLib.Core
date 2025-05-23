﻿/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: MemoryHandleTest.cs 
*
* MemoryHandleTest.cs is part of VNLib.UtilsTests which is part of the larger 
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Extensions;

using static VNLib.Utils.Memory.MemoryUtil;

namespace VNLib.Utils.Memory.Tests
{

    [TestClass]
    public class MemoryHandleTest
    {

        [TestMethod]
        public unsafe void MemoryHandleAllocLongExtensionTest()
        {
            Assert.AreEqual(8, sizeof(nuint));

            //Check for negatives
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Shared.Alloc<byte>(-1).Dispose());

            //Make sure over-alloc throws
            Assert.ThrowsExactly<OverflowException>(() => Shared.Alloc<short>(nuint.MaxValue, false).Dispose());
        }
#if TARGET_64_BIT
        [TestMethod]
        public unsafe void MemoryHandleBigAllocTest()
        {
            const long bigHandleSize = (long)uint.MaxValue + 1024;

            using MemoryHandle<byte> handle = Shared.Alloc<byte>(bigHandleSize);

            //verify size
            Assert.IsTrue(handle.ByteLength, (ulong)bigHandleSize);
            //Since handle is byte, should also match
            Assert.IsTrue(handle.Length, (ulong)bigHandleSize);

            //Should throw overflow
            Assert.ThrowsException<OverflowException>(() => _ = handle.Span);
            Assert.ThrowsException<OverflowException>(() => _ = handle.IntLength);

            //Should get the remaining span
            Span<byte> offsetTest = handle.GetOffsetSpan(int.MaxValue, 1024);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.GetOffsetSpan((long)int.MaxValue + 1, 1024));

        }
#endif

        [TestMethod]
        public unsafe void BasicMemoryHandleTest()
        {
            using MemoryHandle<byte> handle = Shared.Alloc<byte>(128, true);

            Assert.AreEqual(128ul, handle.Length);

            Assert.AreEqual(128ul, handle.Length);

            //Check span against base pointer deref

            handle.Span[120] = 10;

            Assert.AreEqual(10, *handle.GetOffset(120));
            Assert.AreEqual(10, handle.GetOffsetRef(120));
        }


        [TestMethod]
        public unsafe void MemoryHandleDisposedTest()
        {
            using MemoryHandle<byte> handle = Shared.Alloc<byte>(1024);

            //Make sure handle is not invalid until disposed
            Assert.IsFalse(handle.IsInvalid);
            Assert.IsFalse(handle.IsClosed);
            Assert.AreNotEqual(IntPtr.Zero, handle.BasePtr);

            //Dispose the handle early and test
            handle.Dispose();

            Assert.IsTrue(handle.IsInvalid);
            Assert.IsTrue(handle.IsClosed);

            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = handle.Span);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = handle.BasePtr);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = handle.Base);
            Assert.ThrowsExactly<ObjectDisposedException>(() => handle.Resize(10));
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = handle.GetOffset(10));
            Assert.ThrowsExactly<ObjectDisposedException>(handle.ThrowIfClosed);
        }

        [TestMethod]
        public unsafe void MemoryHandleCountDisposedTest()
        {
            using MemoryHandle<byte> handle = Shared.Alloc<byte>(1024);

            //Make sure handle is not invalid until disposed
            Assert.IsFalse(handle.IsInvalid);
            Assert.IsFalse(handle.IsClosed);
            Assert.AreNotEqual(IntPtr.Zero, handle.BasePtr);

            bool test = false;
            //Increase handle counter
            handle.DangerousAddRef(ref test);
            Assert.IsTrue(test);

            //Dispose the handle early and test
            handle.Dispose();

            //Assert the handle is still valid

            //Make sure handle is not invalid until disposed
            Assert.IsFalse(handle.IsInvalid);
            Assert.IsFalse(handle.IsClosed);
            Assert.AreNotEqual(IntPtr.Zero, handle.BasePtr);

            //Dec handle count (should dispose the handle now)
            handle.DangerousRelease();

            //Now make sure the class is disposed

            Assert.IsTrue(handle.IsInvalid);
            Assert.IsTrue(handle.IsClosed);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = handle.Span);
        }

        [TestMethod]
        public unsafe void MemoryHandleExtensionsTest()
        {
            using MemoryHandle<byte> handle = Shared.Alloc<byte>(1024);

            Assert.AreEqual(1024u, handle.Length);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => handle.Resize(-1));

            //Resize the handle 
            handle.Resize(2048);

            Assert.AreEqual(2048u, handle.Length);

            Assert.IsTrue(handle.AsSpan(2048).IsEmpty);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(2049));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetOffset(2049));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetOffset(-1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetOffsetByteRef(2049));           

            //test resize
            handle.ResizeIfSmaller(100);
            //Handle should be unmodified
            Assert.AreEqual(2048u, handle.Length);

            //test working
            handle.ResizeIfSmaller(4096);
            Assert.AreEqual(4096u, handle.Length);
        }

        [TestMethod]
        public unsafe void EmptyHandleTest()
        {
            //Confirm that an empty handle does not raise exceptions when in IMemoryHandle
            using (IMemoryHandle<byte> thandle = new MemoryHandle<byte>())
            {
                Assert.AreEqual(0u, thandle.Length);

                Assert.IsTrue(thandle.Span == Span<byte>.Empty);

                //Empty span should not throw
                _ = thandle.AsSpan(0);

                //Pin should throw
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = thandle.Pin(0));

                Assert.ThrowsExactly<ObjectDisposedException>(() => _ = thandle.GetReference());
            }

            //Full ref to mhandle check status
            using (MemoryHandle<byte> mHandle = new())
            {

                //Some members should not throw
                _ = mHandle.ByteLength;

                //Handle should be invalid
                Assert.IsTrue(mHandle.IsInvalid);

                Assert.IsFalse(mHandle.IsClosed);

                //Confirm empty handle protected values throw
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = mHandle.GetOffset(0));

                Assert.ThrowsExactly<ObjectDisposedException>(() => mHandle.Resize(10));

                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = mHandle.BasePtr);

                Assert.ThrowsExactly<ObjectDisposedException>(() => _ = mHandle.GetReference());
            }
        }
    }
}
