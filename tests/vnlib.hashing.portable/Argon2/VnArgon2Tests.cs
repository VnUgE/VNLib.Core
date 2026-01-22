/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.PortableTests
* File: VnArgon2Tests.cs 
*
* VnArgon2Tests.cs is part of VNLib.Hashing.PortableTests which is part of the larger 
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
            const string NativeArgon2KnownHash = "VqnpGP/WhJou62BB4Lcunaih5+l4wp2kFzL2XdDo8fZ898u/SizOj008MdxPB0uAGy9Ed5UBkqp8XyZwzS8kfQ==";

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

            const string McKnownHash = "x5G97+BiK04sZ9lUh+BI/jeTzxzESrCbXX7gKKHaqm0UF6FIEuIJAX5lPAF8+uhnbGomZXuOrXDCxyIRMjubuA==";

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

            Argon2CostParams a2Params = new()
            {
                MemoryCost = 65536,
                Parallelism = 4,        // MC ignores this value, sort of.
                TimeCost = 3,           // >=3 is recommended
            };

            /*
             * The output of vnlib-argon2 is slightly different from the native library
             * as it encodes the salt as base64 as an s= paremter in the parameter
             * list instead of prepending to the hash with a $ delimiter. It also
             * uses standard base64 encoding instead of url-safe base64 encoding.
             */
            string KnownOutput = $"$argon2id$v=19,m={a2Params.MemoryCost},t={a2Params.TimeCost},p={a2Params.Parallelism},s={SaltBase64}${knownHash}";

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