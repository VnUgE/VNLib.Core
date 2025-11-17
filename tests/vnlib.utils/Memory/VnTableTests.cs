/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: VnTableTests.cs 
*
* VnTableTests.cs is part of VNLib.UtilsTests which is part of the larger 
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


namespace VNLib.Utils.Memory.Tests
{
    [TestClass()]
    public class VnTableTests
    {
        [TestMethod()]
        public void VnTableTest()
        {
            //Empty table
            using (VnTable<int> empty = new(0, 0))
            {
                Assert.IsTrue(empty.Empty);
                //Test 0 rows/cols
                Assert.AreEqual(0u, empty.Rows);
                Assert.AreEqual(0u, empty.Cols);

                //Test that empty table throws on access
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = empty[0, 0]);
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = empty.Get(0, 0));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => empty.Set(0, 0, 10));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => empty[0, 0] = 10);
            }

            using (VnTable<int> table = new(40000, 10000))
            {
                Assert.IsFalse(table.Empty);

                //Test table size
                Assert.AreEqual(40000u, table.Rows);
                Assert.AreEqual(10000u, table.Cols);
            }

            //Test params
            Assert.ThrowsExactly<ArgumentNullException>(() => _ = new VnTable<int>(null!, 1, 1));

        }

        [TestMethod()]
        public void GetSetTest()
        {
            static void TestIndexAt(VnTable<int> table, uint row, uint col, int value)
            {
                table[row, col] = value;
                Assert.AreEqual(table[row, col], value);
                Assert.AreEqual(table.Get(row, col), value);
            }

            static void TestSetAt(VnTable<int> table, uint row, uint col, int value)
            {
                table.Set(row, col, value);
                Assert.AreEqual(table[row, col], value);
                Assert.AreEqual(table.Get(row, col), value);
            }

            static void TestSetDirectAccess(VnTable<int> table, uint row, uint col, int value)
            {
                uint address = row * table.Cols + col;
                table[address] = value;

                //Get value using indexer
                Assert.AreEqual(table[row, col], value);
            }

            static void TestGetDirectAccess(VnTable<int> table, uint row, uint col, int value)
            {
                table[row, col] = value;

                uint address = row * table.Cols + col;

                //Test direct access
                Assert.AreEqual(table[address], value);
                
                //Get value using indexer
                Assert.AreEqual(table[row, col], value);
                Assert.AreEqual(table.Get(row, col), value);
            }


            using VnTable<int> table = new(11, 11);
            
            //Test index at 10,10
            TestIndexAt(table, 10, 10, 11);
            //Test same index with different value using the .set() method
            TestSetAt(table, 10, 10, 25);

            //Test direct access
            TestSetDirectAccess(table, 10, 10, 50);
                
            TestGetDirectAccess(table, 10, 10, 37);

            //Test index at 0,0
            TestIndexAt(table, 0, 0, 13);
            TestSetAt(table, 0, 0, 85);

            //Test at 0,0
            TestSetDirectAccess(table, 0, 0, 100);
            TestGetDirectAccess(table, 0, 0, 86);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = table[11, 11]);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = table.Get(11, 11));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => table.Set(11, 11, 10));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => table[11, 11] = 10);
        }

        [TestMethod()]
        public void DisposeTest()
        {
            //Alloc table
            VnTable<int> table = new(10, 10);
            //Dispose table
            table.Dispose();

            //Test that methods throw on access
            Assert.ThrowsExactly<ObjectDisposedException>(() => table[10, 10] = 10);
            Assert.ThrowsExactly<ObjectDisposedException>(() => table.Set(10, 10, 10));
            Assert.ThrowsExactly<ObjectDisposedException>(() => table[10, 10] == 10);
            Assert.ThrowsExactly<ObjectDisposedException>(() => table.Get(10, 10));
        }

    }
}