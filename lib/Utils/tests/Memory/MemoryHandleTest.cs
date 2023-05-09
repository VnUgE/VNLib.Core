/*
* Copyright (c) 2023 Vaughn Nugent
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
            Assert.IsTrue(sizeof(nuint) == 8);

            //Check for negatives
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Shared.Alloc<byte>(-1).Dispose());

            //Make sure over-alloc throws
            Assert.ThrowsException<OverflowException>(() => Shared.Alloc<short>(nuint.MaxValue, false).Dispose());
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

            Assert.IsTrue(handle.Length == 128);

            Assert.IsTrue(handle.Length == 128);

            //Check span against base pointer deref

            handle.Span[120] = 10;

            Assert.IsTrue(*handle.GetOffset(120) == 10);
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

            Assert.ThrowsException<ObjectDisposedException>(() => _ = handle.Span);
            Assert.ThrowsException<ObjectDisposedException>(() => _ = handle.BasePtr);
            Assert.ThrowsException<ObjectDisposedException>(() => _ = handle.Base);
            Assert.ThrowsException<ObjectDisposedException>(() => handle.Resize(10));
            Assert.ThrowsException<ObjectDisposedException>(() => _ = handle.GetOffset(10));
            Assert.ThrowsException<ObjectDisposedException>(() => handle.ThrowIfClosed());
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

            //Asser is valid still

            //Make sure handle is not invalid until disposed
            Assert.IsFalse(handle.IsInvalid);
            Assert.IsFalse(handle.IsClosed);
            Assert.AreNotEqual(IntPtr.Zero, handle.BasePtr);

            //Dec handle count
            handle.DangerousRelease();
            
            //Now make sure the class is disposed

            Assert.IsTrue(handle.IsInvalid);
            Assert.IsTrue(handle.IsClosed);
            Assert.ThrowsException<ObjectDisposedException>(() => _ = handle.Span);
        }

        [TestMethod]
        public unsafe void MemoryHandleExtensionsTest()
        {
            using MemoryHandle<byte> handle = Shared.Alloc<byte>(1024);

            Assert.IsTrue(handle.Length == 1024);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => handle.Resize(-1));

            //Resize the handle 
            handle.Resize(2048);

            Assert.IsTrue(handle.Length == 2048);

            Assert.IsTrue(handle.AsSpan(2048).IsEmpty);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(2049));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.GetOffset(2049));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.GetOffset(-1));

            //test resize
            handle.ResizeIfSmaller(100);
            //Handle should be unmodified
            Assert.IsTrue(handle.Length == 2048);
            
            //test working
            handle.ResizeIfSmaller(4096);
            Assert.IsTrue(handle.Length == 4096);
        }

        [TestMethod]
        public unsafe void EmptyHandleTest()
        {
            //Confirm that an empty handle does not raise exceptions when in IMemoryHandle
            using (IMemoryHandle<byte> thandle = new MemoryHandle<byte>())
            {
                Assert.IsTrue(thandle.Length == 0);

                Assert.IsTrue(thandle.Span == Span<byte>.Empty);

                //Empty span should not throw
                _ = thandle.AsSpan(0);

                //Pin should throw
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = thandle.Pin(0));
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
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = mHandle.GetOffset(0));

                Assert.ThrowsException<ObjectDisposedException>(() => mHandle.Resize(10));

                Assert.ThrowsException<ArgumentOutOfRangeException>(() => mHandle.BasePtr);
            }
        }
    }
}
