/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: Base32Tests.cs
*
* Base32Tests.cs is part of VNLib.UtilsTests which is part of the larger 
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

using System.Linq;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Codecs;

namespace VNLib.Utils.Codecs.Tests
{
    [TestClass()]
    public class Base32Tests
    {
        [TestMethod()]
        public void Encode_Decode_RoundTrip_ProducesExpectedOutput()
        {
            const string base32Encoded = "JBSWY3DPEBLW64TMMQQQ====";
            const string base32Decoded = "Hello World!";

            byte[] rawBytes = Encoding.UTF8.GetBytes(base32Decoded);

            // Decode the known encoded string and confirm bytes match
            byte[]? fromString = Base32.Decode(base32Encoded);
            Assert.IsNotNull(fromString);
            Assert.IsTrue(rawBytes.SequenceEqual(fromString));

            // Re-encode the raw bytes and confirm output matches   
            string toString = Base32.Encode(rawBytes, includePadding: true);
            Assert.AreEqual(base32Encoded, toString, ignoreCase: false);
        }
    }
}
