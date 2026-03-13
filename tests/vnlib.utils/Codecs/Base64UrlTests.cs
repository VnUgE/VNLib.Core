/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: Base64UrlTests.cs
*
* Base64UrlTests.cs is part of VNLib.UtilsTests which is part of the larger 
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
using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Codecs;

namespace VNLib.Utils.Codecs.Tests
{
    [TestClass()]
    public class Base64UrlTests
    {
        private static int GetRandomBase64Bytes(int size, Span<byte> encodeBuffer)
        {
            byte[] randomData = RandomNumberGenerator.GetBytes(size);

            OperationStatus status = Base64.EncodeToUtf8(randomData, encodeBuffer, out _, out int bytesEncoded, true);

            Assert.AreEqual(OperationStatus.Done, status);

            return bytesEncoded;
        }

        [TestMethod()]
        public void MakeUrlSafe_InPlaceReplacesIllegalChars()
        {
            byte[] encodeBuffer = new byte[Base64.GetMaxEncodedToUtf8Length(64)];
            Span<byte> encodeSpan;

            do
            {
                int bytesEncoded = GetRandomBase64Bytes(64, encodeBuffer);
                encodeSpan = encodeBuffer.AsSpan(0, bytesEncoded);

            } while (!(encodeSpan.Contains((byte)'+') || encodeSpan.Contains((byte)'/')));

            Base64Url.MakeUrlSafe(encodeSpan);

            Assert.IsFalse(encodeSpan.Contains((byte)'+') || encodeSpan.Contains((byte)'/'));
        }

        [TestMethod()]
        public void RestoreBase64_InPlaceRestoresLegalChars()
        {
            // Known Base64URL string that contains '-' and '_' substitutions
            const string base64UrlSafe = "lZUABUd8q2BS7p8giysuC7PpEabAFBnMqBPL-9A-qgfR1lbTHQ4tMm8E8nimm2YAd5NGDIQ0vxfU9i5l53tF_WXa_H4vkHfzlv0Df-lLADJV7z8sn-8sfUGdaAiIS8_4OmVGnnY4-TppLMsVR6ov2t07HdOHPPsFFhSpBMXa2pwRveRATcxBA2XxVe09FOWgahhssNS7lU9eC7fRw7icD4ZoJcLSRBbxrjRmeVXKhPIaXR-4mnQ5-vqYzAr9S99CthgbAtVn_WjmDcda6pUB9JW9lp7ylDa9e1r_z39cihTXMOGaUSjVURJaWrNF8CkfW56_x2ODCBmZPov1YyEhww==";

            byte[] utf8 = Encoding.UTF8.GetBytes(base64UrlSafe);

            Base64Url.RestoreBase64(utf8);

            // '-' and '_' must have been replaced with '+' and '/'
            Assert.IsFalse(Array.Exists(utf8, b => b == '_' || b == '-'));

            // Confirm the result decodes as valid standard Base64
            OperationStatus status = Base64.DecodeFromUtf8InPlace(utf8, out _);

            Assert.AreNotEqual(OperationStatus.NeedMoreData, status);
            Assert.AreNotEqual(OperationStatus.DestinationTooSmall, status);
            Assert.AreNotEqual(OperationStatus.InvalidData, status);
        }

        [TestMethod()]
        public void TryToBase64CharsTest()
        {
        }
    }
}
