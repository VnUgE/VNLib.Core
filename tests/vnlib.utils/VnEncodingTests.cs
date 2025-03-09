/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: VnEncodingTests.cs 
*
* VnEncodingTests.cs is part of VNLib.UtilsTests which is part of the larger 
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
using System.Linq;
using System.Text;
using System.Buffers;
using System.Diagnostics;
using System.Buffers.Text;
using System.Security.Cryptography;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Utils.Tests
{
    [TestClass()]
    public class VnEncodingTests
    {
        private static int GetRandomBase64Bytes(int size, Span<byte> encodeBuffer)
        {
            byte[] randomData = RandomNumberGenerator.GetBytes(size);            
            //Base64 encode
            OperationStatus status = Base64.EncodeToUtf8(randomData, encodeBuffer, out _, out int bytesEncoded, true);

            Assert.AreEqual(OperationStatus.Done, status);

            return bytesEncoded;
        }


        [TestMethod()]
        public void Base64ToUrlSafeInPlaceTest()
        {
            byte[] encodeBuffer = new byte[Base64.GetMaxEncodedToUtf8Length(64)];
           
            Span<byte> encodeSpan;

            do
            {
                //Get random base64 bytes and ensure the data contains illegal characters
                int bytesEncoded = GetRandomBase64Bytes(64, encodeBuffer);
                encodeSpan = encodeBuffer.AsSpan(0, bytesEncoded);

                //Make sure some illegal characters are present
            } while (!(encodeSpan.Contains((byte)'+') || encodeSpan.Contains((byte)'/')));          

            //Convert to url safe
            VnEncoding.Base64ToUrlSafeInPlace(encodeSpan);

            //Make sure the illegal characters are gone
            Assert.IsFalse(encodeSpan.Contains((byte)'+') || encodeSpan.Contains((byte)'/'));
        }

        [TestMethod()]
        public void Base64FromUrlSafeInPlaceTest()
        {
            //url safe base64 with known encoded characters
            const string base64UrlSafe = "lZUABUd8q2BS7p8giysuC7PpEabAFBnMqBPL-9A-qgfR1lbTHQ4tMm8E8nimm2YAd5NGDIQ0vxfU9i5l53tF_WXa_H4vkHfzlv0Df-lLADJV7z8sn-8sfUGdaAiIS8_4OmVGnnY4-TppLMsVR6ov2t07HdOHPPsFFhSpBMXa2pwRveRATcxBA2XxVe09FOWgahhssNS7lU9eC7fRw7icD4ZoJcLSRBbxrjRmeVXKhPIaXR-4mnQ5-vqYzAr9S99CthgbAtVn_WjmDcda6pUB9JW9lp7ylDa9e1r_z39cihTXMOGaUSjVURJaWrNF8CkfW56_x2ODCBmZPov1YyEhww==";

            //Convert to utf8 binary
            byte[] utf8 = Encoding.UTF8.GetBytes(base64UrlSafe);

            //url decode
            VnEncoding.Base64FromUrlSafeInPlace(utf8);

            //Confirm illegal chars have been converted back to base64
            Assert.IsFalse(utf8.Contains((byte)'_') || utf8.Contains((byte)'-'));

            //Decode in place to confrim its valid
            OperationStatus status = Base64.DecodeFromUtf8InPlace(utf8, out int bytesWritten);

            Assert.AreNotEqual(OperationStatus.NeedMoreData, status);
            Assert.AreNotEqual(OperationStatus.DestinationTooSmall, status);
            Assert.AreNotEqual(OperationStatus.InvalidData, status);
        }
        
        [TestMethod()]
        public void TryToBase64CharsTest()
        {
            
        }

        [TestMethod()]
        public void PercentEncodeTest()
        {
            const string urlEnoded = "https%3A%2F%2Fwww.google.com%2Fsearch%3Fq%3Dtest%26oq%3Dtest%26aqs%3Dchrome..69i57j0l7.1001j0j7%26sourceid%3Dchrome%26ie%3DUTF-8";
            const string urlDecoded = "https://www.google.com/search?q=test&oq=test&aqs=chrome..69i57j0l7.1001j0j7&sourceid=chrome&ie=UTF-8";

            //We need to allow the '.' character to be encoded
            ReadOnlySpan<byte> allowedChars = Encoding.UTF8.GetBytes(".");

            
            /*
             * Test that the url encoded string is the same as the percent encoded string
             */

            ReadOnlySpan<byte> utf8Encoded = Encoding.UTF8.GetBytes(urlDecoded);

            string percentEncoded = VnEncoding.PercentEncode(utf8Encoded, allowedChars);

            Assert.IsTrue(percentEncoded.Equals(urlEnoded, StringComparison.Ordinal));

            /*
             * Test decoding the percent encoded string
             */

            ReadOnlySpan<byte> percentEncodedUtf8 = Encoding.UTF8.GetBytes(urlEnoded);

            byte[] outBuffer = new byte[percentEncodedUtf8.Length];

            ERRNO decoded = VnEncoding.PercentDecode(percentEncodedUtf8, outBuffer);

            //Make sure result is valid
            Debug.Assert(decoded > 0);

            string decodedString = Encoding.UTF8.GetString(outBuffer, 0, decoded);

            Assert.AreEqual(urlDecoded, decodedString, false);
        }

        [TestMethod()]
        public void Base32BasicEncodeDecodeTest()
        {
            const string base32Encoded = "JBSWY3DPEBLW64TMMQQQ====";
            const string base32Decoded = "Hello World!";
            byte[] rawBytes = Encoding.UTF8.GetBytes(base32Decoded);

            //Recover bytes from base32 encoded string
            byte[]? fromString = VnEncoding.FromBase32String(base32Encoded); 
            Assert.IsNotNull(fromString);
          
            //Test that the decoded bytes are the same as the raw bytes
            Assert.IsTrue(rawBytes.SequenceEqual(fromString));

            //Test that the encoded string is the same as the base32 encoded string
            string toString = VnEncoding.ToBase32String(rawBytes, true);
            Assert.AreEqual(base32Encoded, toString, false);
        }
    }
}