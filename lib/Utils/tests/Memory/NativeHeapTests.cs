using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

namespace VNLib.Utils.Memory.Tests
{
    [TestClass()]
    public class NativeHeapTests
    {
        const string RpMallocLibPath = "../../../../../Utils.Memory/vnlib_rpmalloc/build/Debug/vnlib_rpmalloc.dll";
        const string MimallocLibPath = "../../../../../Utils.Memory/vnlib_mimalloc/build/Debug/vnlib_mimalloc.dll";

        [TestMethod()]
        public void LoadInTreeRpmallocTest()
        {
            //Try to load the shared heap
            using NativeHeap heap = NativeHeap.LoadHeap(RpMallocLibPath, System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories, HeapCreation.Shared, 0);

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
            using NativeHeap heap = NativeHeap.LoadHeap(MimallocLibPath, System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories, HeapCreation.Shared, 0);

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