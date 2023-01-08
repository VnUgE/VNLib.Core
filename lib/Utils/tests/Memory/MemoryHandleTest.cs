/*
* Copyright (c) 2022 Vaughn Nugent
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


using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils;
using VNLib.Utils.Extensions;

using static VNLib.Utils.Memory.Memory;

namespace VNLib.Utils.Memory.Tests
{
    [TestClass]
    public class MemoryHandleTest
    {

        [TestMethod]
        public void MemoryHandleAllocLongExtensionTest()
        {
            //Check for negatives
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Shared.Alloc<byte>(-1));

            //Make sure over-alloc throws
            Assert.ThrowsException<NativeMemoryOutOfMemoryException>(() => Shared.Alloc<byte>(ulong.MaxValue, false));
        }
#if TARGET_64_BIT
        [TestMethod]
        public unsafe void MemoryHandleBigAllocTest()
        {
            const long bigHandleSize = (long)uint.MaxValue + 1024;

            using MemoryHandle<byte> handle = Shared.Alloc<byte>(bigHandleSize);

            //verify size
            Assert.AreEqual(handle.ByteLength, (ulong)bigHandleSize);
            //Since handle is byte, should also match
            Assert.AreEqual(handle.Length, (ulong)bigHandleSize);

            //Should throw overflow
            Assert.ThrowsException<OverflowException>(() => _ = handle.Span);
            Assert.ThrowsException<OverflowException>(() => _ = handle.IntLength);

            //Should get the remaining span
            Span<byte> offsetTest = handle.GetOffsetSpan(int.MaxValue, 1024);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.GetOffsetSpan((long)int.MaxValue + 1, 1024));

        }
#else
        
#endif

        [TestMethod]
        public unsafe void BasicMemoryHandleTest()
        {
            using MemoryHandle<byte> handle = Shared.Alloc<byte>(128, true);

            Assert.AreEqual(handle.IntLength, 128);

            Assert.AreEqual(handle.Length, (ulong)128);

            //Check span against base pointer deref

            handle.Span[120] = 10;

            Assert.AreEqual(*handle.GetOffset(120), 10);
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

            Assert.AreEqual(handle.IntLength, 1024);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => handle.Resize(-1));

            //Resize the handle 
            handle.Resize(2048);

            Assert.AreEqual(handle.IntLength, 2048);

            Assert.IsTrue(handle.AsSpan(2048).IsEmpty);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.AsSpan(2049));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.GetOffset(2049));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.GetOffset(-1));

            //test resize
            handle.ResizeIfSmaller(100);
            //Handle should be unmodified
            Assert.AreEqual(handle.IntLength, 2048);
            
            //test working
            handle.ResizeIfSmaller(4096);
            Assert.AreEqual(handle.IntLength, 4096);
        }
    }
}
