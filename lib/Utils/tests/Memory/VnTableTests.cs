/*
* Copyright (c) 2022 Vaughn Nugent
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

using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace VNLib.Utils.Memory.Tests
{
    [TestClass()]
    public class VnTableTests
    {
        [TestMethod()]
        public void VnTableTest()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                using VnTable<int> table = new(-1, 0);
            });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                using VnTable<int> table = new(0, -1);
            });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                using VnTable<int> table = new(-1, -1);
            });

            //Empty table
            using (VnTable<int> empty = new(0, 0))
            {
                Assert.IsTrue(empty.Empty);
                //Test 0 rows/cols
                Assert.AreEqual(0, empty.Rows);
                Assert.AreEqual(0, empty.Cols);
            }

            using (VnTable<int> table = new(40000, 10000))
            {
                Assert.IsFalse(table.Empty);

                //Test table size
                Assert.AreEqual(40000, table.Rows);
                Assert.AreEqual(10000, table.Cols);
            }

            
            //Test oom, should be native
            Assert.ThrowsException<NativeMemoryOutOfMemoryException>(() =>
            {
                using VnTable<int> table = new(int.MaxValue, 2);
            });
        }

        [TestMethod()]
        public void VnTableTest1()
        {
            //No throw if empty
            using VnTable<int> table = new(null!,0, 0);

            //Throw if table is not empty
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                using VnTable<int> table = new(null!,1, 1);
            });

        }

        [TestMethod()]
        public void GetSetTest()
        {
            static void TestIndexAt(VnTable<int> table, int row, int col, int value)
            {
                table[row, col] = value;
                Assert.AreEqual(value, table[row, col]);
                Assert.AreEqual(value, table.Get(row, col));
            }

            static void TestSetAt(VnTable<int> table, int row, int col, int value)
            {
                table.Set(row, col, value);
                Assert.AreEqual(value, table[row, col]);
                Assert.AreEqual(value, table.Get(row, col));
            }

            static void TestSetDirectAccess(VnTable<int> table, int row, int col, int value)
            {
                int address = row * table.Cols + col;
                table[(uint)address] = value;

                //Get value using indexer
                Assert.AreEqual(value, table[row, col]);
            }

            static void TestGetDirectAccess(VnTable<int> table, int row, int col, int value)
            {
                table[row, col] = value;

                int address = row * table.Cols + col;

                //Test direct access
                Assert.AreEqual(value, table[(uint)address]);
                
                //Get value using indexer
                Assert.AreEqual(value, table[row, col]);
                Assert.AreEqual(value, table.Get(row, col));
            }
        

            using (VnTable<int> table = new(11, 11))
            {
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
            }
        }

        [TestMethod()]
        public void DisposeTest()
        {
            //Alloc table
            VnTable<int> table = new(10, 10);
            //Dispose table
            table.Dispose();

            //Test that methods throw on access
            Assert.ThrowsException<ObjectDisposedException>(() => table[10, 10] = 10);
            Assert.ThrowsException<ObjectDisposedException>(() => table.Set(10, 10, 10));
            Assert.ThrowsException<ObjectDisposedException>(() => table[10, 10] == 10);
            Assert.ThrowsException<ObjectDisposedException>(() => table.Get(10, 10));
        }

    }
}