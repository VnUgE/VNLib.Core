/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM.Tests
* File: FBMRequestTests.cs 
*
* FBMRequestTests.cs is part of VNLib.Net.Messaging.FBM.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Net.Messaging.FBM.Tests. If not, see http://www.gnu.org/licenses/.
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Memory;
using VNLib.Net.Http;
using VNLib.Net.Messaging.FBM.Client;

namespace VNLib.Net.Messaging.FBM.Tests
{
    [TestClass]
    public class FBMRequestTests
    {

        private readonly SharedHeapFBMMemoryManager _memManager = new(MemoryUtil.Shared);

        /// <summary>
        /// Tests the creation and disposal of the FBMRequest object
        /// </summary>
        [TestMethod]
        public void CreateAndDisposeTest()
        {
            int messageId = Helpers.RandomMessageId;

            using FBMRequest request = new(messageId, _memManager, 16384, Helpers.DefaultEncoding);

            // Verify initial state
            Assert.AreEqual(messageId, request.MessageId);
            Assert.AreEqual(7, request.Length); // Only message ID header initially

            // Verify that the request can be disposed without issues
            request.Dispose();

            //Ensure methods throw ObjectDisposedException after disposal
            Assert.ThrowsExactly<ObjectDisposedException>(() => request.WriteHeader(HeaderCommand.ContentType, "test-header"));
            Assert.ThrowsExactly<ObjectDisposedException>(() => request.WriteBody([0, 1, 2, 3], ContentType.Binary));
            Assert.ThrowsExactly<ObjectDisposedException>(() => request.GetBodyWriter());
            Assert.ThrowsExactly<ObjectDisposedException>(() => request.GetBodyStream());
            Assert.ThrowsExactly<ObjectDisposedException>(() => request.GetRequestData());
            Assert.ThrowsExactly<ObjectDisposedException>(() => request.Reset());
            Assert.ThrowsExactly<ObjectDisposedException>(() => request.Compile());
            Assert.ThrowsExactly<ObjectDisposedException>(() => request.ToString());
        }

        /// <summary>
        /// Tests writing headers to the request
        /// </summary>
        [TestMethod]
        public void WriteHeaderTest()
        {
            using FBMRequest request = new(Helpers.RandomMessageId, _memManager, 16384, Helpers.DefaultEncoding);

            // Test writing header with HeaderCommand enum
            request.WriteHeader(HeaderCommand.Location, "http://example.com/resource");
            // Verify header was written
            Assert.IsTrue(request.Length > 7); // Length should be greater than just the message ID header

            //Test writing header with custom byte key
            request.WriteHeader(0x56, "custom-header-value");

            //compile the request to string and verify it contains the headers
            string compiled = request.Compile();

            Assert.IsFalse(string.IsNullOrEmpty(compiled));
            Assert.IsTrue(compiled.Contains("http://example.com/resource"));
            Assert.IsTrue(compiled.Contains("custom-header-value"), "Compiled string should include custom-header-value.");
        }

        /*
         * Known message should match 
         * 0x01 (big endian 4 byte id) termination
         * 0xa1 (encoded text header1) termination
         * 0xa1 (encoded text header2) termination
         * 0x03 "application/octet-stream" termination
         * termination
         * binary message body
         * 
         * NOTE: 
         *   Headers are repeatable, they are just added sequentially
         */
        private static readonly byte[] KnownRequest =
        [
            0x01, 0x00, 0x00, 0x00, 0x02, 0xff, 0xf1, // Message ID (big-endian)

            0xa1, 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0xff, 0xf1, // Header 1: "hello" (encoded text)

            0xa1, 0x77, 0x6f, 0x72, 0x6c, 0x64, 0xff, 0xf1, // Header 2: "world" (encoded text)

            0x03, 0x61,0x70,0x70,0x6c,0x69,0x63,0x61,0x74,0x69,0x6f,0x6e,0x2f,
            0x6f,0x63,0x74,0x65,0x74,0x2d,0x73,0x74,0x72,0x65,0x61,0x6d, 0xff,0xf1, // Header 3: "application/binary" (encoded text)

            0xff, 0xf1, // header termination

            0x01, 0x02, 0x03, 0x04, // Binary message body
        ];

        [TestMethod]
        public void KnownRequestTest()
        {
            const int MessageId = 2;

            using FBMRequest request = new(MessageId, _memManager, 16384, Helpers.DefaultEncoding);

            // Write known headers
            request.WriteHeader(0xa1, "hello");
            request.WriteHeader(0xa1, "world");

            request.WriteBody([0x01, 0x02, 0x03, 0x04], ContentType.Binary);

            ReadOnlyMemory<byte> requestData = request.GetRequestData();

            // Verify the request data matches the known request
            Assert.IsTrue(requestData.Span.SequenceEqual(KnownRequest),
                $"Request data does not match known request.\n Expected: {BitConverter.ToString(KnownRequest)}\n Actual:   {BitConverter.ToString(requestData.ToArray())}");
        }

        [TestMethod]
        public void MessageReuseTest()
        {
            // Create a basic message request, check that it's correctly initialized, then reset it, ensure it's empty, and then write a new message to it.

            using FBMRequest request = new(Helpers.RandomMessageId, _memManager, 16384, Helpers.DefaultEncoding);

            // Write initial headers and body
            request.WriteHeader(HeaderCommand.Location, "http://example.com/resource");
            request.WriteBody([0x01, 0x02, 0x03, 0x04], ContentType.Binary);

            ReadOnlyMemory<byte> initialData = request.GetRequestData();
            Assert.IsTrue(initialData.Length > 0, "Initial request data should not be empty.");
            Assert.AreEqual(0x01, initialData.Span[0], "Initial request data should start with the message ID.");

            // Reset the request
            request.Reset();

            // After reset, the request should only contain the message ID header
            Assert.AreEqual(7, request.Length, "After reset, request length should be just the message ID header.");

        }
    }
}