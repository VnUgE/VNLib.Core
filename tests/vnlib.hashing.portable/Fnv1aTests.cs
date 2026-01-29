/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.PortableTests
* File: Fnv1aTests.cs 
*
* Fnv1aTests.cs is part of VNLib.Hashing.PortableTests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Hashing.PortableTests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Hashing.PortableTests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Hashing.PortableTests. If not, see http://www.gnu.org/licenses/.
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Text;

using VNLib.Hashing.Checksums;

namespace VNLib.Hashing.PortableTests
{
    [TestClass()]
    public class Fnv1aTests
    {
        const string KnownDataInputUtf81 = "Hello world, this is a test of the FNV1a algorithm";
        const string KnownData64ChecksumHex1 = "033b9d1635f1c2ad";

        const string KnownDataInputUtf82 = "Hello world, this is another, slightly different test of the FNV1a algorithm!";
        const string KnownData64ChecksumHex2 = "a802c807e941c5d3";

        [TestMethod()]
        public void Fnv1a64Known1()
        {
            TestKnownData(KnownDataInputUtf81, KnownData64ChecksumHex1);
            TestKnownData(KnownDataInputUtf82, KnownData64ChecksumHex2);
        }
      
        static void TestKnownData(string input, string knownChecksumHex)
        {
            byte[] knownInput = Encoding.UTF8.GetBytes(input);
            ulong knownChecksum = Convert.ToUInt64(knownChecksumHex, 16);

            ulong checksum = FNV1a.Compute64(knownInput);

            Assert.AreEqual(knownChecksum, checksum);

            //Split input into 2 parts
            byte[] part1 = knownInput[..(knownInput.Length / 2)];
            byte[] part2 = knownInput[(knownInput.Length / 2)..];

            //Compute checksum of part1
            ulong checksum1 = FNV1a.Compute64(part1);
            ulong outputChecksum = FNV1a.Update64(checksum1, part2);

            Assert.AreNotEqual(checksum1, outputChecksum);
            Assert.AreEqual(knownChecksum, outputChecksum);
        }

    }
}