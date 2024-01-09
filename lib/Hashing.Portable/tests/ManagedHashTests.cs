using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Text;

using VNLib.Utils.Memory;
using VNLib.Utils;
using System.Diagnostics;


namespace VNLib.Hashing.Tests
{
    [TestClass()]
    public class ManagedHashTests
    {
        [TestMethod()]
        public void ComputeHashTest()
        {
            byte[] testData = Encoding.UTF8.GetBytes("Hello World!");
            using UnsafeMemoryHandle<byte> heapBuffer = MemoryUtil.UnsafeAlloc(64, false);

            Trace.WriteLineIf(ManagedHash.SupportsSha3, "SHA3 is supported");
            Trace.WriteLineIf(ManagedHash.SupportsBlake2b, "Blake2b is supported");

            //Test all supported algorithms
            foreach (HashAlg alg in Enum.GetValues<HashAlg>())
            {
                if (alg == HashAlg.None) continue;

                //Skip unsupported algorithms
                if (alg == HashAlg.BlAKE2B && !ManagedHash.SupportsBlake2b) continue;

                if (!ManagedHash.SupportsSha3)
                {
                    switch (alg)
                    {
                        case HashAlg.SHA3_256:
                        case HashAlg.SHA3_384:
                        case HashAlg.SHA3_512:
                            continue;
                    }
                }

                //Compute hash
                ERRNO hashSize = ManagedHash.ComputeHash(testData, heapBuffer.Span, alg);
                Assert.IsTrue(hashSize == Math.Abs(hashSize));

                //With input string and heap buffer
                hashSize = ManagedHash.ComputeHash("test", heapBuffer.Span, alg);
                Assert.IsTrue(hashSize == Math.Abs(hashSize));

                //Compute string and byte array
                byte[] testdata = ManagedHash.ComputeHash(testData, alg);
                Assert.IsTrue(testdata.Length == Math.Abs(hashSize));

                //With input string
                testdata = ManagedHash.ComputeHash("test", alg);
                Assert.IsTrue(testdata.Length == Math.Abs(hashSize));

                //Compute hash as string
                string testEnc = ManagedHash.ComputeHash(testdata, alg, HashEncodingMode.Hexadecimal);
                Assert.IsTrue(testEnc.Length == Math.Abs(hashSize) * 2);

                //With input string
                testEnc = ManagedHash.ComputeHash("test", alg, HashEncodingMode.Hexadecimal);
                Assert.IsTrue(testEnc.Length == Math.Abs(hashSize) * 2);
            }
        }

        [TestMethod()]
        public void ComputeHmacTest()
        {
            byte[] testData = Encoding.UTF8.GetBytes("Hello World!");
            byte[] testKey = RandomHash.GetRandomBytes(32);
            using UnsafeMemoryHandle<byte> heapBuffer = MemoryUtil.UnsafeAlloc(64, false);

            Trace.WriteLineIf(ManagedHash.SupportsSha3, "SHA3 is supported");
            Trace.WriteLineIf(ManagedHash.SupportsBlake2b, "Blake2b is supported");

            //Test all supported algorithms
            foreach (HashAlg alg in Enum.GetValues<HashAlg>())
            {
                if (alg == HashAlg.None) continue;

                //Skip unsupported algorithms
                if (alg == HashAlg.BlAKE2B && !ManagedHash.SupportsBlake2b) continue;

                if (!ManagedHash.SupportsSha3)
                {
                    switch (alg)
                    {
                        case HashAlg.SHA3_256:
                        case HashAlg.SHA3_384:
                        case HashAlg.SHA3_512:
                            continue;
                    }
                }

                //Compute hash
                ERRNO hashSize = ManagedHash.ComputeHmac(testKey, testData, heapBuffer.Span, alg);
                Assert.IsTrue(hashSize == Math.Abs(hashSize));

                //With input string and heap buffer
                hashSize = ManagedHash.ComputeHmac(testKey, "test", heapBuffer.Span, alg);
                Assert.IsTrue(hashSize == Math.Abs(hashSize));

                //Compute string and byte array
                byte[] testdata = ManagedHash.ComputeHmac(testKey, testData, alg);
                Assert.IsTrue(testdata.Length == Math.Abs(hashSize));

                //With input string
                testdata = ManagedHash.ComputeHmac(testKey, "test", alg);
                Assert.IsTrue(testdata.Length == Math.Abs(hashSize));

                //Compute hash as string
                string testEnc = ManagedHash.ComputeHmac(testKey, testdata, alg, HashEncodingMode.Hexadecimal);
                Assert.IsTrue(testEnc.Length == Math.Abs(hashSize) * 2);

                //With input string
                testEnc = ManagedHash.ComputeHmac(testKey, "test", alg, HashEncodingMode.Hexadecimal);
                Assert.IsTrue(testEnc.Length == Math.Abs(hashSize) * 2);
            }
        }
    }
}