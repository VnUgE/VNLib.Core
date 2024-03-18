using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Text;

using VNLib.Hashing.Checksums;

namespace VNLib.Hashing.Tests
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