/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: PercentEncodingTests.cs
*
* PercentEncodingTests.cs is part of VNLib.UtilsTests which is part of the larger 
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

using System;
using System.Diagnostics;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Codecs;

namespace VNLib.Utils.Codecs.Tests
{
    [TestClass()]
    public class PercentEncodingTests
    {
        [TestMethod()]
        public void Encode_RoundTrip_ProducesExpectedOutput()
        {
            const string urlEncoded = "https%3A%2F%2Fwww.google.com%2Fsearch%3Fq%3Dtest%26oq%3Dtest%26aqs%3Dchrome..69i57j0l7.1001j0j7%26sourceid%3Dchrome%26ie%3DUTF-8";
            const string urlDecoded = "https://www.google.com/search?q=test&oq=test&aqs=chrome..69i57j0l7.1001j0j7&sourceid=chrome&ie=UTF-8";

            // '.' is allowed through unescaped
            ReadOnlySpan<byte> allowedChars = "."u8;

            /*
             * Encode and compare against the known percent-encoded string.
             */

            ReadOnlySpan<byte> utf8Input = Encoding.UTF8.GetBytes(urlDecoded);
            string percentEncoded = PercentEncoding.Encode(utf8Input, allowedChars);

            Assert.IsTrue(percentEncoded.Equals(urlEncoded, StringComparison.Ordinal));

            /*
             * Decode the percent-encoded string and confirm we recover the original.
             */

            ReadOnlySpan<byte> percentEncodedUtf8 = Encoding.UTF8.GetBytes(urlEncoded);
            byte[] outBuffer = new byte[percentEncodedUtf8.Length];

            ERRNO decoded = PercentEncoding.Decode(percentEncodedUtf8, outBuffer);

            Debug.Assert(decoded > 0);

            string decodedString = Encoding.UTF8.GetString(outBuffer, 0, decoded);

            Assert.AreEqual(urlDecoded, decodedString, ignoreCase: false);
        }
    }
}
