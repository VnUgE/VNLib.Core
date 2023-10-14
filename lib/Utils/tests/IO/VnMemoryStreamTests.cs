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
                Assert.IsTrue(vms.Length == 0);
                Assert.IsTrue(vms.Position == 0);
                Assert.IsTrue(vms.CanSeek == true);
                Assert.IsTrue(vms.CanRead == true);
                Assert.IsTrue(vms.CanWrite == true);
            }

            //Test heap
            IUnmangedHeap privateHeap = MemoryUtil.InitializeNewHeapForProcess();

            using (VnMemoryStream vms = new(privateHeap, 1024, false))
            {
                Assert.IsTrue(vms.Length == 0);
                Assert.IsTrue(vms.Position == 0);
                Assert.IsTrue(vms.CanSeek == true);
                Assert.IsTrue(vms.CanRead == true);
                Assert.IsTrue(vms.CanWrite == true);
            }


            //Create from mem handle
            MemoryHandle<byte> handle = privateHeap.Alloc<byte>(byte.MaxValue);

            using (VnMemoryStream vms = VnMemoryStream.ConsumeHandle(handle, handle.GetIntLength(), false))
            {
                Assert.IsTrue(vms.Length == byte.MaxValue);
                Assert.IsTrue(vms.Position == 0);
                Assert.IsTrue(vms.CanSeek == true);
                Assert.IsTrue(vms.CanRead == true);
                Assert.IsTrue(vms.CanWrite == true);
            }

            //From existing data
            ReadOnlySpan<byte> testSpan = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            using (VnMemoryStream vms = new (privateHeap, testSpan))
            {
                Assert.IsTrue(vms.Length == testSpan.Length);
                Assert.IsTrue(vms.Position == 0);

                //Check values copied
                while (vms.Position < vms.Length)
                {
                    byte test = testSpan[(int)vms.Position];
                    Assert.IsTrue(vms.ReadByte() == test);
                }
            }

            ReadOnlyMemory<byte> testMemory = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            using (VnMemoryStream vms = new (privateHeap, testMemory))
            {
                Assert.IsTrue(vms.Length == testMemory.Length);
                Assert.IsTrue(vms.Position == 0);

                //Check values copied
                while(vms.Position < vms.Length)
                {
                    byte test = testMemory.Span[(int)vms.Position];
                    Assert.IsTrue(vms.ReadByte() == test);
                }
            }
        }

        [TestMethod()]
        public void VnMemoryStreamReadonlyTest()
        {
            using VnMemoryStream vms = new(MemoryUtil.Shared, 0, false);

            Assert.IsTrue(vms.CanWrite == true);

            //Convert to readonly
            _ = VnMemoryStream.CreateReadonly(vms);

            Assert.IsTrue(vms.CanSeek == true);
            Assert.IsTrue(vms.CanRead == true);
            Assert.IsTrue(vms.CanWrite == false);

            //Try to write
            Assert.ThrowsException<NotSupportedException>(() => vms.WriteByte(0));

        }
    }
}