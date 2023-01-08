/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: MemoryTests.cs 
*
* MemoryTests.cs is part of VNLib.UtilsTests which is part of the larger 
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
using System.Runtime.InteropServices;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory.Tests
{
    [TestClass()]
    public class MemoryTests
    {
        [TestMethod]
        public void MemorySharedHeapLoadedTest()
        {
            Assert.IsNotNull(Memory.Shared);
        }

        [TestMethod()]
        public void UnsafeAllocTest()
        {
            //test against negative number
            Assert.ThrowsException<ArgumentException>(() => Memory.UnsafeAlloc<byte>(-1));

            //Alloc large block test (100mb)
            const int largTestSize = 100000 * 1024;
            //Alloc super small block
            const int smallTestSize = 5;

            using (UnsafeMemoryHandle<byte> buffer = Memory.UnsafeAlloc<byte>(largTestSize, false))
            {
                Assert.AreEqual(largTestSize, buffer.IntLength);
                Assert.AreEqual(largTestSize, buffer.Span.Length);

                buffer.Span[0] = 254;
                Assert.AreEqual(buffer.Span[0], 254);
            }

            using (UnsafeMemoryHandle<byte> buffer = Memory.UnsafeAlloc<byte>(smallTestSize, false))
            {
                Assert.AreEqual(smallTestSize, buffer.IntLength);
                Assert.AreEqual(smallTestSize, buffer.Span.Length);

                buffer.Span[0] = 254;
                Assert.AreEqual(buffer.Span[0], 254);
            }

            //Different data type

            using(UnsafeMemoryHandle<long> buffer = Memory.UnsafeAlloc<long>(largTestSize, false))
            {
                Assert.AreEqual(largTestSize, buffer.IntLength);
                Assert.AreEqual(largTestSize, buffer.Span.Length);

                buffer.Span[0] = long.MaxValue;
                Assert.AreEqual(buffer.Span[0], long.MaxValue);
            }

            using (UnsafeMemoryHandle<long> buffer = Memory.UnsafeAlloc<long>(smallTestSize, false))
            {
                Assert.AreEqual(smallTestSize, buffer.IntLength);
                Assert.AreEqual(smallTestSize, buffer.Span.Length);

                buffer.Span[0] = long.MaxValue;
                Assert.AreEqual(buffer.Span[0], long.MaxValue);
            }
        }

        [TestMethod()]
        public void UnsafeZeroMemoryAsSpanTest()
        {
            //Alloc test buffer
            Span<byte> test = new byte[1024];
            test.Fill(0);
            //test other empty span
            Span<byte> verify = new byte[1024];
            verify.Fill(0);

            //Fill test buffer with random values
            Random.Shared.NextBytes(test);

            //make sure buffers are not equal
            Assert.IsFalse(test.SequenceEqual(verify));

            //Zero buffer
            Memory.UnsafeZeroMemory<byte>(test);

            //Make sure buffers are equal
            Assert.IsTrue(test.SequenceEqual(verify));
        }

        [TestMethod()]
        public void UnsafeZeroMemoryAsMemoryTest()
        {
            //Alloc test buffer
            Memory<byte> test = new byte[1024];
            test.Span.Fill(0);
            //test other empty span
            Memory<byte> verify = new byte[1024];
            verify.Span.Fill(0);

            //Fill test buffer with random values
            Random.Shared.NextBytes(test.Span);

            //make sure buffers are not equal
            Assert.IsFalse(test.Span.SequenceEqual(verify.Span));

            //Zero buffer
            Memory.UnsafeZeroMemory<byte>(test);

            //Make sure buffers are equal
            Assert.IsTrue(test.Span.SequenceEqual(verify.Span));
        }

        [TestMethod()]
        public void InitializeBlockAsSpanTest()
        {
            //Alloc test buffer
            Span<byte> test = new byte[1024];
            test.Fill(0);
            //test other empty span
            Span<byte> verify = new byte[1024];
            verify.Fill(0);

            //Fill test buffer with random values
            Random.Shared.NextBytes(test);

            //make sure buffers are not equal
            Assert.IsFalse(test.SequenceEqual(verify));

            //Zero buffer
            Memory.InitializeBlock(test);

            //Make sure buffers are equal
            Assert.IsTrue(test.SequenceEqual(verify));
        }

        [TestMethod()]
        public void InitializeBlockMemoryTest()
        {
            //Alloc test buffer
            Memory<byte> test = new byte[1024];
            test.Span.Fill(0);
            //test other empty span
            Memory<byte> verify = new byte[1024];
            verify.Span.Fill(0);

            //Fill test buffer with random values
            Random.Shared.NextBytes(test.Span);

            //make sure buffers are not equal
            Assert.IsFalse(test.Span.SequenceEqual(verify.Span));

            //Zero buffer
            Memory.InitializeBlock(test);

            //Make sure buffers are equal
            Assert.IsTrue(test.Span.SequenceEqual(verify.Span));
        }

        #region structmemory tests

        [StructLayout(LayoutKind.Sequential)]
        struct TestStruct
        {
            public int X;
            public int Y;
        }

        [TestMethod()]
        public unsafe void ZeroStructAsPointerTest()
        {
            TestStruct* s = Memory.Shared.StructAlloc<TestStruct>();
            s->X = 10;
            s->Y = 20;
            Assert.AreEqual(10, s->X);
            Assert.AreEqual(20, s->Y);
            //zero struct
            Memory.ZeroStruct(s);
            //Verify data was zeroed
            Assert.AreEqual(0, s->X);
            Assert.AreEqual(0, s->Y);
            //Free struct
            Memory.Shared.StructFree(s);
        }

        [TestMethod()]
        public unsafe void ZeroStructAsVoidPointerTest()
        {
            TestStruct* s = Memory.Shared.StructAlloc<TestStruct>();
            s->X = 10;
            s->Y = 20;
            Assert.AreEqual(10, s->X);
            Assert.AreEqual(20, s->Y);
            //zero struct
            Memory.ZeroStruct<TestStruct>((void*)s);
            //Verify data was zeroed
            Assert.AreEqual(0, s->X);
            Assert.AreEqual(0, s->Y);
            //Free struct
            Memory.Shared.StructFree(s);
        }

        [TestMethod()]
        public unsafe void ZeroStructAsIntPtrTest()
        {
            TestStruct* s = Memory.Shared.StructAlloc<TestStruct>();
            s->X = 10;
            s->Y = 20;
            Assert.AreEqual(10, s->X);
            Assert.AreEqual(20, s->Y);
            //zero struct
            Memory.ZeroStruct<TestStruct>((IntPtr)s);
            //Verify data was zeroed
            Assert.AreEqual(0, s->X);
            Assert.AreEqual(0, s->Y);
            //Free struct
            Memory.Shared.StructFree(s);
        }
        #endregion
    }
}