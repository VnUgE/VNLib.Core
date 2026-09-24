/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: VnMemoryStreamTests.cs 
*
* VnMemoryStreamTests.cs is part of VNLib.UtilsTests which is part of the larger 
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
using System.IO;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Utils.IO.Tests
{
    [TestClass]
    public sealed class VnMemoryStreamTests
    {
        #region Constructors

        /// <summary>
        /// Verifies all constructor overloads initialize an empty stream with the expected length, position, and capabilities.
        /// </summary>
        [TestMethod]
        public void Constructor_Overloads_InitializeExpectedState()
        {
            using (VnMemoryStream vms = new())
            {
                Assert.AreEqual(0, vms.Length);
                Assert.AreEqual(0, vms.Position);
                Assert.IsTrue(vms.CanSeek);
                Assert.IsTrue(vms.CanRead);
                Assert.IsTrue(vms.CanWrite);
            }

            //Test heap
            using IUnmanagedHeap privateHeap = MemoryUtil.InitializeNewHeapForProcess();

            using (VnMemoryStream vms = new(privateHeap, 1024, false))
            {
                Assert.AreEqual(0, vms.Length);
                Assert.AreEqual(0, vms.Position);
                Assert.IsTrue(vms.CanSeek);
                Assert.IsTrue(vms.CanRead);
                Assert.IsTrue(vms.CanWrite);
            }


            //Create from mem handle
            MemoryHandle<byte> handle = privateHeap.Alloc<byte>(byte.MaxValue);

            using (VnMemoryStream vms = VnMemoryStream.FromHandle(handle, true, handle.GetIntLength(), false))
            {
                Assert.AreEqual(byte.MaxValue, vms.Length);
                Assert.AreEqual(0, vms.Position);
                Assert.IsTrue(vms.CanSeek);
                Assert.IsTrue(vms.CanRead);
                Assert.IsTrue(vms.CanWrite);
            }

            //Handle should throw since the stream owns the handle and it gets dispoed
            Assert.ThrowsExactly<ObjectDisposedException>(handle.ThrowIfClosed);

            //From existing data
            ReadOnlySpan<byte> testSpan = [1, 2, 3, 4, 5, 6, 7, 8];
            using (VnMemoryStream vms = new (privateHeap, testSpan))
            {
                Assert.AreEqual(testSpan.Length, vms.Length);
                Assert.AreEqual(0, vms.Position);

                //Check values copied
                while (vms.Position < vms.Length)
                {
                    byte test = testSpan[(int)vms.Position];
                    Assert.AreEqual(test, vms.ReadByte());
                }
            }

            ReadOnlyMemory<byte> testMemory = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            using (VnMemoryStream vms = new (privateHeap, testMemory))
            {
                Assert.AreEqual(testMemory.Length, vms.Length);
                Assert.AreEqual(0, vms.Position);

                //Check values copied
                while (vms.Position < vms.Length)
                {
                    byte test = testMemory.Span[(int)vms.Position];
                    Assert.AreEqual(test, vms.ReadByte());
                }
            }
        }

        #endregion

        #region Readonly

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.CreateReadonly(VnMemoryStream)"/> marks a writable stream readonly and blocks writes.
        /// </summary>
        [TestMethod]
        public void CreateReadonly_WritableStream_MarksReadonly()
        {
            using VnMemoryStream vms = new(MemoryUtil.Shared, 0, false);

            Assert.IsTrue(vms.CanWrite);

            //Convert to readonly
            _ = VnMemoryStream.CreateReadonly(vms);

            Assert.IsTrue(vms.CanSeek);
            Assert.IsTrue(vms.CanRead);
            Assert.IsFalse(vms.CanWrite);

            //Try to write
            Assert.ThrowsExactly<NotSupportedException>(() => vms.WriteByte(0));

        }

        #endregion

        #region Windows

        [TestMethod()]
        public void GetMemOrSpanTest()
        {
            //Alloc stream with some initial buffer size
            using VnMemoryStream vms = new(1024, false);

            //Ensure since no data was written, the returned windows are empty
            Assert.IsTrue(vms.AsSpan().IsEmpty);
            Assert.IsTrue(vms.AsMemory().IsEmpty);

            //Write some data
            byte[] testData = [1, 2, 3, 4, 5, 6, 7, 8];
            vms.Write(testData);

            Assert.HasCount((int)vms.Length, testData);

            //Get the data as a span
            ReadOnlySpan<byte> span = vms.AsSpan();
            Assert.HasCount(span.Length, testData);

            for (int i = 0; i < span.Length; i++)
            {
                Assert.AreEqual(span[i], testData[i]);
            }

            //Get the data as a memory
            ReadOnlyMemory<byte> memory = vms.AsMemory();
            Assert.HasCount(memory.Length, testData);

            Assert.IsTrue(memory.Span.SequenceEqual(testData));

            //Get the data as a byte array
            byte[] array = vms.ToArray();
            Assert.HasCount(array.Length, testData);

            Assert.IsTrue(array.AsSpan().SequenceEqual(testData));
        }

        #endregion

        #region SetLength

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.SetLength(long)"/> resizes the stream, clamps an out-of-range position, and rejects negative values.
        /// </summary>
        [TestMethod]
        public void SetLength_Resize_UpdatesLengthAndClampsPosition()
        {
            using VnMemoryStream vms = new(1024, false);

            Assert.AreEqual(0, vms.Length);

            // Set length to 0
            vms.SetLength(0);
            Assert.AreEqual(0, vms.Length);

            // Set length to a positive value
            vms.SetLength(512);
            Assert.AreEqual(512, vms.Length);
            Assert.AreEqual(0, vms.Position);

            // Check that position smaller than length gets reset below new length
            vms.Seek(100, System.IO.SeekOrigin.Begin);           
            Assert.AreEqual(100, vms.Position, "Position should not change if it is less than the new length.");

            vms.SetLength(25);
            Assert.AreEqual(25, vms.Length);
            Assert.AreEqual(25, vms.Position, "Position should be shrunk to point within the new length");

            // Check invalid arguments
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => vms.SetLength(-1), "Setting length to a negative value should throw ArgumentOutOfRangeException.");
        }

        /// <summary>
        /// Verifies a zero-size stream initializes empty and allows later resizing.
        /// </summary>
        [TestMethod]
        public void Constructor_EmptyStream_AllowsResize()
        {
            using VnMemoryStream vms = new(0, false);
            
            Assert.AreEqual(0, vms.Length);          
            Assert.AreEqual(0, vms.Position);
            Assert.IsTrue(vms.CanSeek);            
            Assert.IsTrue(vms.CanRead);            
            Assert.IsTrue(vms.CanWrite);

            // Resize should be allowed
            vms.SetLength(128);
            Assert.AreEqual(128, vms.Length);
            Assert.AreEqual(0, vms.Position);
        }

        #endregion

        #region CopyToAsync

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.CopyToAsync(Stream, int, CancellationToken)"/> advances the position by bytes written, not the buffer size, on a partial final chunk.
        /// </summary>
        [TestMethod]
        public async Task CopyToAsync_PartialFinalChunk_AdvancesByBytesWritten()
        {
            using VnMemoryStream vms = new(MemoryUtil.Shared, 128, false);
            using MemoryStream dest = new();

            for (int i = 0; i < 100; i++)
            {
                vms.WriteByte((byte)i);
            }

            Assert.AreEqual(100, vms.Position);
            Assert.AreEqual(100, vms.Length);

            //Rewind, the copy starts at the current position
            vms.Seek(0, SeekOrigin.Begin);

            //100 bytes with a 16-byte copy buffer forces a partial final chunk (6x16 + 4),
            //the position must advance by bytes written, not the buffer size
            await vms.CopyToAsync(dest, 16);

            Assert.AreEqual(100, vms.Position, "Position should match the number of bytes copied on a partial final chunk.");

            byte[] array = dest.ToArray();
            Assert.HasCount(100, array);

            Assert.IsTrue(vms.AsSpan().SequenceEqual(array));
        }

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.CopyToAsync(Stream, int, CancellationToken)"/> copies only the remaining bytes when starting mid-stream.
        /// </summary>
        [TestMethod]
        public async Task CopyToAsync_PartialPosition_CopiesRemainder()
        {
            using VnMemoryStream vms = new(MemoryUtil.Shared, 128, false);
            using MemoryStream dest = new();

            for (int i = 0; i < 100; i++)
            {
                vms.WriteByte((byte)i);
            }

            //Start mid-stream, only the remaining 70 bytes (30..100) should be copied
            vms.Seek(30, SeekOrigin.Begin);

            //70 bytes with a 16-byte copy buffer forces a partial final chunk (4x16 + 6)
            await vms.CopyToAsync(dest, 16);

            Assert.AreEqual(100, vms.Position, "Position should match the end of the stream after copying from a partial position.");

            byte[] array = dest.ToArray();
            Assert.HasCount(70, array);

            for (int i = 0; i < array.Length; i++)
            {
                Assert.AreEqual((byte)(i + 30), array[i]);
            }
        }

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.CopyToAsync(Stream, int, CancellationToken)"/> copies all data when the length is an exact multiple of the buffer size.
        /// </summary>
        [TestMethod]
        public async Task CopyToAsync_ExactMultiple_CopiesAll()
        {
            using VnMemoryStream vms = new(MemoryUtil.Shared, 128, false);
            using MemoryStream dest = new();

            for (int i = 0; i < 64; i++)
            {
                vms.WriteByte((byte)i);
            }

            vms.Seek(0, SeekOrigin.Begin);

            //64 bytes with a 16-byte copy buffer copies in full chunks with no partial final chunk
            await vms.CopyToAsync(dest, 16);

            Assert.AreEqual(64, vms.Position);

            byte[] array = dest.ToArray();
            Assert.HasCount(64, array);

            Assert.IsTrue(vms.AsSpan().SequenceEqual(array));
        }

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.CopyToAsync(Stream, int, CancellationToken)"/> on an empty stream moves no data and leaves the position unchanged.
        /// </summary>
        [TestMethod]
        public async Task CopyToAsync_EmptyStream_WritesNothing()
        {
            using VnMemoryStream vms = new(MemoryUtil.Shared, 128, false);
            using MemoryStream dest = new();

            //Nothing to copy, position must not move and no data should be written
            await vms.CopyToAsync(dest, 16);

            Assert.AreEqual(0, vms.Position);
            Assert.AreEqual(0, dest.Length);
        }

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.CopyToAsync(Stream, int, CancellationToken)"/> rejects a null destination and a non-positive buffer size.
        /// </summary>
        [TestMethod]
        public async Task CopyToAsync_InvalidArgs_Throws()
        {
            using VnMemoryStream vms = new(MemoryUtil.Shared, 128, false);
            using MemoryStream dest = new();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => vms.CopyToAsync(null!, 16));
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => vms.CopyToAsync(dest, 0));
        }

        #endregion

        #region AsyncIO

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.ReadAsync(Memory{byte}, CancellationToken)"/> completes synchronously with the requested data.
        /// </summary>
        [TestMethod]
        public async Task ReadAsync_Memory_ReturnsSynchronousResult()
        {
            using VnMemoryStream vms = new(128, false);

            for (int i = 0; i < 100; i++)
            {
                vms.WriteByte((byte)i);
            }

            vms.Seek(0, SeekOrigin.Begin);

            byte[] chunk = new byte[25];

            int read = await vms.ReadAsync(chunk.AsMemory());

            Assert.AreEqual(25, read, "ReadAsync should return the number of bytes read.");
            Assert.AreEqual(25, vms.Position, "Position should advance by the number of bytes read.");

            for (int i = 0; i < read; i++)
            {
                Assert.AreEqual((byte)i, chunk[i], "Read data should match the written data.");
            }
        }

        /// <summary>
        /// Verifies <see cref="VnMemoryStream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> writes synchronously and updates the stream.
        /// </summary>
        [TestMethod]
        public async Task WriteAsync_Memory_WritesSynchronously()
        {
            using VnMemoryStream vms = new(128, false);

            byte[] testData = [1, 2, 3, 4, 5, 6, 7, 8];

            await vms.WriteAsync(testData.AsMemory());

            Assert.AreEqual(testData.Length, vms.Length, "Length should match the written data.");
            Assert.AreEqual(testData.Length, vms.Position, "Position should advance by the written data.");
            Assert.IsTrue(vms.AsSpan().SequenceEqual(testData), "Stream contents should match the written data.");
        }

        #endregion
    }
}
