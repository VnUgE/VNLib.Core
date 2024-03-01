using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Text;
using System.Diagnostics;

using VNLib.Utils;
using VNLib.Utils.Memory;

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
                if (!ManagedHash.IsAlgSupported(alg)) continue;

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
                if (!ManagedHash.IsAlgSupported(alg)) continue;

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

        const string TestHashInput = "Hello World!";
        static readonly string[] TestHashOutputHex =
        [
            //None
            "", 
            //Md5
            "ed076287532e86365e841e92bfc50d8c",
            //Sha1
            "2ef7bde608ce5404e97d5f042f95f89f1c232871",
            //Sha2
            "7f83b1657ff1fc53b92dc18148a1d65dfc2d4b1fa3d677284addd200126d9069",            
            "bfd76c0ebbd006fee583410547c1887b0292be76d582d96c242d2a792723e3fd6fd061f9d5cfd13b8f961358e6adba4a",
            "861844d6704e8573fec34d967e20bcfef3d424cf48be04e6dc08f2bd58c729743371015ead891cc3cf1c9d34b49264b510751b1ff9e537937bc46b5d6ff4ecc8",           

            //Sha3           
            "d0e47486bbf4c16acac26f8b653592973c1362909f90262877089f9c8a4536af",
            "f324cbd421326a2abaedf6f395d1a51e189d4a71c755f531289e519f079b224664961e385afcc37da348bd859f34fd1c",
            "32400b5e89822de254e8d5d94252c52bdcb27a3562ca593e980364d9848b8041b98eabe16c1a6797484941d2376864a1b0e248b0f7af8b1555a778c336a5bf48",

            //Blake2b (64 bytes/512 bits)
            "54b113f499799d2f3c0711da174e3bc724737ad18f63feb286184f0597e1466436705d6c8e8c7d3d3b88f5a22e83496e0043c44a3c2b1700e0e02259f8ac468e"
        ];

        //Known hash sizes to compare against
        static readonly int[] HashSizes = 
        [
            0,
            16,
            20,
            32,
            48,
            64,
            32,
            48,
            64,
            64
        ];

        [TestMethod()]
        public void ComputeHexHashTest()
        {
            HashAlg[] algs = Enum.GetValues<HashAlg>();

            Assert.AreEqual(algs.Length, TestHashOutputHex.Length);

            for (int i = 0; i < algs.Length; i++)
            {
                if (algs[i] == HashAlg.None)
                    continue;

                //Skip unsupported algorithms
                if (!ManagedHash.IsAlgSupported(algs[i]))
                    continue;

                string hash = ManagedHash.ComputeHash(TestHashInput, algs[i], HashEncodingMode.Hexadecimal);

                //Make sure exact length (x2 for hex)
                Assert.AreEqual(HashSizes[i] * 2, hash.Length);

                //Make sure exact value
                Assert.AreEqual(TestHashOutputHex[i], hash, true);
            }
        }

        [TestMethod()]
        public void AlgSizeTest()
        {
            HashAlg[] algs = Enum.GetValues<HashAlg>();

            for (int i = 0; i < algs.Length; i++)
            {
                if (algs[i] == HashAlg.None)
                    continue;

                //Make sure exact length
                Assert.AreEqual(HashSizes[i], ManagedHash.GetHashSize(algs[i]));
            }

            Assert.ThrowsException<ArgumentException>(() => ManagedHash.GetHashSize(HashAlg.None));
        }
    }
}