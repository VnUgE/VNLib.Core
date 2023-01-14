/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Text;
using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Utils.Tests
{
    [TestClass()]
    public class VnEncodingTests
    {

        [TestMethod()]
        public void Base64ToUrlSafeInPlaceTest()
        {
            //Get randomd data to encode
            byte[] dataToEncode = RandomNumberGenerator.GetBytes(64);
            //Calc buffer size
            int base64Output = Base64.GetMaxEncodedToUtf8Length(64);

            byte[] encodeBuffer = new byte[base64Output];
            //Base64 encode
            OperationStatus status = Base64.EncodeToUtf8(dataToEncode, encodeBuffer, out _, out int bytesEncoded, true);

            Assert.IsTrue(status == OperationStatus.Done);

            Span<byte> encodeSpan = encodeBuffer.AsSpan(0, bytesEncoded);

            //Make sure some illegal characters are encoded
            Assert.IsTrue(encodeSpan.Contains((byte)'+') || encodeSpan.Contains((byte)'/'));

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

            Assert.IsFalse(status == OperationStatus.NeedMoreData);
            Assert.IsFalse(status == OperationStatus.DestinationTooSmall);
            Assert.IsFalse(status == OperationStatus.InvalidData);
        }
        
        [TestMethod()]
        public void TryToBase64CharsTest()
        {
            
        }

        
    }
}