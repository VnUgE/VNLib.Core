using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Utils.IO.Tests
{
    [TestClass()]
    public class VnMemoryStreamTests
    {
        [TestMethod()]
        public void VnMemoryStreamConstructorTest()
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
                while(vms.Position < vms.Length)
                {
                    byte test = testMemory.Span[(int)vms.Position];
                    Assert.AreEqual(test, vms.ReadByte());
                }
            }
        }

        [TestMethod()]
        public void VnMemoryStreamReadonlyTest()
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

            Assert.AreEqual(vms.Length, testData.Length);

            //Get the data as a span
            ReadOnlySpan<byte> span = vms.AsSpan();
            Assert.AreEqual(span.Length, testData.Length);

            for (int i = 0; i < span.Length; i++)
            {
                Assert.AreEqual(span[i], testData[i]);
            }

            //Get the data as a memory
            ReadOnlyMemory<byte> memory = vms.AsMemory();
            Assert.AreEqual(memory.Length, testData.Length);

            Assert.IsTrue(memory.Span.SequenceEqual(testData));

            //Get the data as a byte array
            byte[] array = vms.ToArray();
            Assert.AreEqual(array.Length, testData.Length);

            Assert.IsTrue(array.AsSpan().SequenceEqual(testData));
        }

        [TestMethod]
        public void SetLengthTest()
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

            // Check that position smaller than legnth gets reset below new length
            vms.Seek(100, System.IO.SeekOrigin.Begin);           
            Assert.AreEqual(100, vms.Position, "Position should not change if it is less than the new length.");

            vms.SetLength(25);
            Assert.AreEqual(25, vms.Length);
            Assert.AreEqual(25, vms.Position, "Position should be shrunk to point within the new length");

            // Check invalid arguments
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => vms.SetLength(-1), "Setting length to a negative value should throw ArgumentOutOfRangeException.");
        }
    }
}