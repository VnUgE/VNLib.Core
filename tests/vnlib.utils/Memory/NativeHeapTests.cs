using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Runtime.InteropServices;

namespace VNLib.Utils.Memory.Tests
{
    [TestClass()]
    public class NativeHeapTests
    {
        private static string? RpMallocLibPath => Environment.GetEnvironmentVariable("TEST_RPMALLOC_LIB_PATH");
        
        private static string? MimallocLibPath => Environment.GetEnvironmentVariable("TEST_MIMALLOC_LIB_PATH");

        [TestMethod()]
        public void LoadInTreeRpmallocTest()
        {
            //Try to load the shared heap
            using NativeHeap heap = NativeHeap.LoadHeap(RpMallocLibPath!, DllImportSearchPath.SafeDirectories, HeapCreation.Shared, flags: 0);

            Assert.IsFalse(heap.IsInvalid);

            IntPtr block = heap.Alloc(100, sizeof(byte), false);

            Assert.IsTrue(block != IntPtr.Zero);

            //Attempt to realloc
            heap.Resize(ref block, 200, sizeof(byte), false);

            //Free the block
            Assert.IsTrue(heap.Free(ref block));

            //confirm the pointer it zeroed
            Assert.IsTrue(block == IntPtr.Zero);
        }

        [TestMethod()]
        public void LoadInTreeMimallocTest()
        {
            //Try to load the shared heap
            using NativeHeap heap = NativeHeap.LoadHeap(MimallocLibPath!, DllImportSearchPath.SafeDirectories, HeapCreation.Shared, flags: 0);

            Assert.IsFalse(heap.IsInvalid);

            IntPtr block = heap.Alloc(100, sizeof(byte), false);

            Assert.IsTrue(block != IntPtr.Zero);

            //Attempt to realloc
            heap.Resize(ref block, 200, sizeof(byte), false);

            //Free the block
            Assert.IsTrue(heap.Free(ref block));

            //confirm the pointer it zeroed
            Assert.IsTrue(block == IntPtr.Zero);
        }
    }
}