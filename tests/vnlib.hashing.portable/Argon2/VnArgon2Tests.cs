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
            const string NativeArgon2KnownHash = "5rE9QJP1nqQXlLDSxjj7PwVv7oub33lP+zxvaRjcM4ApKPtirF5N5V7dH55MYqwNv/e7qh+mqQWT2u/OR4wU3w==";

            TestArgon2Lib(VnArgon2.GetOrLoadSharedLib(), NativeArgon2KnownHash);
        }

        [TestMethod]
        public void MonocypherArgon2Test()
        {
            /*
             * Monocypher will aways have a different hash output value compared to 
             * the native library because it does not actually support mutli-threading.
             * 
             * So they have to be compared separately.
             */

            const string McKnownHash = "V7Xfd2i6lt3ZfWhrpN+6NCH9o2SZ1lJ/roqchm9uGU+XCizqS6ZAOs6am97tw/WGpWzQMKmE/F3Yp/2kPMavkA==";

            Assert.IsTrue(MonoCypherLibrary.CanLoadDefaultLibrary());

            TestArgon2Lib(
                MonoCypherLibrary.Shared.Argon2CreateLibrary(MemoryUtil.Shared), 
                McKnownHash
            );
        }

        private static void TestArgon2Lib(IArgon2Library library, string knownHash)
        {
            const string RawPass = "HelloWorld1!*";
            const string SaltBase64 = "USixgneVaOQYhlglhfLr9A==";            
            const uint HashSize = 64u;

            /*
             * The output of vnlib-argon2 is slightly different from the native library
             * as it encodes the salt as base64 as an s= paremter in the parameter
             * list instead of prepending to the hash with a $ delimiter. It also
             * uses standard base64 encoding instead of url-safe base64 encoding.
             */
            string KnownOutput = $"$argon2id$v=19,m=65536,t=2,p=4,s={SaltBase64}${knownHash}";

            Argon2CostParams a2Params = new()
            {
                MemoryCost = 65536,
                Parallelism = 4,        // MC ignores this value, sort of.
                TimeCost = 3,           // >=3 is recommended
            };

            //Check that empty secret is allowed
            string passHash = library.Hash2id(
                password: RawPass,
                salt: Convert.FromBase64String(SaltBase64),
                secret: [],
                costParams: in a2Params,
                hashLen: HashSize
            );

            Assert.AreEqual(KnownOutput, passHash, ignoreCase: false);

            //Test that a secret also works okay
            _ = library.Hash2id(
                password: RawPass,
                salt: Convert.FromBase64String(SaltBase64),
                secret: Convert.FromBase64String("Ds1PAqf7RGi9XnvaOFAuaw=="),
                costParams: in a2Params,
                hashLen: HashSize
            );
        }
    }
}