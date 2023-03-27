using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

namespace VNLib.Utils.Memory.Tests
{
    [TestClass()]
    public class NativeHeapTests
    {
        [TestMethod()]
        public void LoadHeapTest()
        {
            const string TEST_HEAP_FILENAME = @"rpmalloc.dll";

            //Try to load the global heap
            using NativeHeap heap = NativeHeap.LoadHeap(TEST_HEAP_FILENAME, System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories, HeapCreation.None, 0);

            Assert.IsFalse(heap.IsInvalid);

            IntPtr block = heap.Alloc(100, sizeof(byte), false);

            Assert.IsTrue(block != IntPtr.Zero);

            //Free the block
            Assert.IsTrue(heap.Free(ref block));

            //confirm the pointer it zeroed
            Assert.IsTrue(block == IntPtr.Zero);

        }
    }
}