/*
* Copyright (c) 2024 Vaughn Nugent
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
                Assert.IsTrue(0 == empty.Rows);
                Assert.IsTrue(0 == empty.Cols);

                //Test that empty table throws on access
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = empty[0, 0]);
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = empty.Get(0, 0));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => empty.Set(0, 0, 10));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => empty[0, 0] = 10);
            }

            using (VnTable<int> table = new(40000, 10000))
            {
                Assert.IsFalse(table.Empty);

                //Test table size
                Assert.IsTrue(40000 == table.Rows);
                Assert.IsTrue(10000 == table.Cols);
            }

            //Test params
            Assert.ThrowsException<ArgumentNullException>(() => _ = new VnTable<int>(null!, 1, 1));

            /*
             * Try-catch is used because underlying heaps 
             * may cause different OOM exceptions to be raised
             * but still have a base class of OutOfMemoryException.
             * So catch covers all OOM exceptions.
             */

            try
            {
                using VnTable<int> table = new(uint.MaxValue, 20);

                Assert.Fail("The table allocation did not fail as expected");
            }
            catch (OutOfMemoryException)
            {}
            catch(Exception ex) 
            {
                Assert.Fail("Table overflow creation test failed because another exception type was raised, {0}", ex.GetType().Name);
            }
        }

        [TestMethod()]
        public void GetSetTest()
        {
            static void TestIndexAt(VnTable<int> table, uint row, uint col, int value)
            {
                table[row, col] = value;
                Assert.IsTrue(value == table[row, col]);
                Assert.IsTrue(value == table.Get(row, col));
            }

            static void TestSetAt(VnTable<int> table, uint row, uint col, int value)
            {
                table.Set(row, col, value);
                Assert.IsTrue(value == table[row, col]);
                Assert.IsTrue(value == table.Get(row, col));
            }

            static void TestSetDirectAccess(VnTable<int> table, uint row, uint col, int value)
            {
                uint address = row * table.Cols + col;
                table[address] = value;

                //Get value using indexer
                Assert.IsTrue(value == table[row, col]);
            }

            static void TestGetDirectAccess(VnTable<int> table, uint row, uint col, int value)
            {
                table[row, col] = value;

                uint address = row * table.Cols + col;

                //Test direct access
                Assert.IsTrue(value == table[address]);
                
                //Get value using indexer
                Assert.IsTrue(value == table[row, col]);
                Assert.IsTrue(value == table.Get(row, col));
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

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = table[11, 11]);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = table.Get(11, 11));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => table.Set(11, 11, 10));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => table[11, 11] = 10);
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