/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: BitFieldTests.cs 
*
* BitFieldTests.cs is part of VNLib.UtilsTests which is part of the larger 
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

﻿using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace VNLib.Utils.Tests
{
    [TestClass()]
    public class BitFieldTests
    {
        [TestMethod()]
        public void BasicFuntionalityTest()
        {
            BitField bf = new(0);

            Assert.AreEqual(0ul, bf.Value);

            bf.Set(1);
            Assert.IsTrue(bf.IsSet(1));
            Assert.AreEqual(1ul, bf.Value);

            bf.Set(1 << 1);
            Assert.IsTrue(bf.IsSet(1 << 1));
            Assert.AreEqual(3ul, bf.Value);

            bf.Set(4);
            Assert.IsTrue(bf.IsSet(4));
            Assert.AreEqual(7ul, bf.Value);

            bf.Clear(0x02);
            Assert.AreEqual(5ul, bf.Value);

            bf.ClearAll();
            Assert.IsFalse(bf.IsSet(1));
            Assert.AreEqual(0ul, bf.Value);

            bf.Set(1u << 63);
            Assert.IsTrue(bf.IsSet(1u << 63));
            Assert.IsFalse(bf.IsSet(1u << 62));
            Assert.AreEqual(1u << 63, bf.Value);
        }
    }
}