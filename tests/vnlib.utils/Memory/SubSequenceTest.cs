/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: SubSequenceTest.cs 
*
* SubSequenceTest.cs is part of VNLib.UtilsTests which is part of the larger 
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

namespace VNLib.Utils.Memory.Tests
{
    [TestClass]
    public class SubSequenceTest
    {
        [TestMethod]
        public void SubSequenceSliceTest()
        {
            const int TestHandleSize = 8192;

            //Alloc handle
            using MemoryHandle<byte> handle = MemoryUtil.Shared.Alloc<byte>(TestHandleSize, false);

            //Should be able to get an empty span at the beginning
            Assert.IsTrue(_ = handle.GetSubSequence(0, 0).Span.IsEmpty);

            //Should be able to get an empty span at the end
            Assert.IsTrue(_ = handle.GetSubSequence(8192, 0).Span.IsEmpty);

            //Test extension bounds checking, may defer to the sequence itself

            //Overrun the handle by offset
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetSubSequence(8193, 1).Span);

            //Overrun the handle by size
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetSubSequence(0, 8193).Span);

            //Overrun the handle by size at the end of a valid handle
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetSubSequence(8192, 1).Span);

            //Negative offset
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.GetSubSequence(0, -1).Span);
         

            //Test slicing 

            SubSequence<byte> full = handle.GetSubSequence(0, TestHandleSize);

            Assert.AreEqual(TestHandleSize, full.Span.Length);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = full.Slice(0, -1).Span);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = full.Slice(8192, 1).Span);

            //Test slicing with offset only, size should be the remainder of the handle
            Assert.AreEqual(TestHandleSize - 100, full.Slice(100).Span.Length);

            //Slice of slice
            SubSequence<byte> slice = full.Slice(8190, 2);

            Assert.AreEqual(2, slice.Span.Length);

            //Allow slice of the exact same size or smaller
            Assert.AreEqual(2, slice.Slice(0, 2).Span.Length);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = slice.Slice(0, 3).Span);

            //Allow empty slice at the end
            Assert.IsTrue(slice.Slice(2, 0).Span.IsEmpty);

            //Overrun by offset
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = slice.Slice(3, 0).Span);

            //Overrun by size
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = slice.Slice(0, 3).Span);

            //Overrun by size at the end of a valid slice
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = slice.Slice(2, 1).Span);

           
        }
    }
}
