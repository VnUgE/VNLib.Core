using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Hashing.Native.MonoCypher;
using VNLib.Utils.Memory;

namespace VNLib.Hashing.Tests
{
    [TestClass()]
    public class VnArgon2Tests
    {
        
        [TestMethod]
        public void LoadLibraryTest()
        {
            //shared lib should load without issue
            _ = VnArgon2.GetOrLoadSharedLib();
        }

        [TestMethod]
        public void Argon2IdHashTest()
        {
            TestArgon2Lib(VnArgon2.GetOrLoadSharedLib());
        }

        [TestMethod]
        public void MonocypherArgon2Test()
        {
            Assert.IsTrue(MonoCypherLibrary.CanLoadDefaultLibrary());

            TestArgon2Lib(MonoCypherLibrary.Shared.Argon2CreateLibrary(MemoryUtil.Shared));
        }

        private static void TestArgon2Lib(IArgon2Library library)
        {
            const string RawPass = "HelloWorld1!*";
            const string SaltHex = "de7cdb9d59828ac9";
            const string PepperHex = "13fe89892162d477";
            const string KnownOutput = "";
            const uint HashSize = 64u;

            Argon2CostParams a2Params = new()
            {
                MemoryCost = 65535,
                Parallelism = 4,
                TimeCost = 2,
            };

            string passHash = library.Hash2id(
                password: RawPass,
                salt: Convert.FromHexString(SaltHex),
                secret: Convert.FromHexString(PepperHex),
                costParams: in a2Params,
                hashLen: HashSize
            );

            Console.WriteLine(passHash);
        }
    }
}