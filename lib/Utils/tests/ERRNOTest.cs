/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: ERRNOTest.cs 
*
* ERRNOTest.cs is part of VNLib.UtilsTests which is part of the larger 
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

namespace VNLib.Utils.Tests
{
    [TestClass]
    public class ERRNOTest
    {

        [TestMethod]
        public unsafe void ERRNOSizeTest()
        {
            Assert.IsTrue(sizeof(ERRNO) == sizeof(nint));
        }

    }
}
