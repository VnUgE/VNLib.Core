using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Hashing.Tests
{
    [TestClass()]
    public class VnArgon2Tests
    {
        private static string LocalArgon2Lib = "../../../../../Utils.Cryptography/argon2/build/Debug/Argon2";

        [TestInitialize]
        public void InitDefaultLibPath()
        {
            Environment.SetEnvironmentVariable(VnArgon2.ARGON2_LIB_ENVIRONMENT_VAR_NAME, LocalArgon2Lib);
        }

        [TestMethod]
        public void LoadLibraryTest()
        {
            //shared lib should load without issue
            _ = VnArgon2.GetOrLoadSharedLib();
        }

        [TestMethod]
        public void Argon2IdHashTest()
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

            IArgon2Library lib = VnArgon2.GetOrLoadSharedLib();

            string passHash = lib.Hash2id(
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