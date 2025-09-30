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

        /*
         * Heap implementations may alter flags supplied to them. Heap creation flags allow the caller
         * to declare suggestions to the heap implementation, such as whether it should be shared, support reallocations,
         * global zeroing, etc. The heap implementation may choose to ignore these flags, or alter them based on the
         * platform or other factors.
         */

        const HeapCreation _defaultFlags = HeapCreation.Shared | HeapCreation.SupportsRealloc;

        [TestMethod()]
        public void LoadInTreeRpmallocTest()
        {
            //Try to rpmalloc shared heap
            using NativeHeap heap = NativeHeap.LoadHeap(RpMallocLibPath!, DllImportSearchPath.SafeDirectories, _defaultFlags, flags: 0);

            Assert.IsTrue(heap.CreationFlags.HasFlag(HeapCreation.Shared), "Heap should be created with Shared flag");

            TestBasicHeapApi(heap);
        }

        [TestMethod()]
        public void LoadInTreeMimallocTest()
        {
            //Try to load Mimalloc shared heap
            using NativeHeap heap = NativeHeap.LoadHeap(MimallocLibPath!, DllImportSearchPath.SafeDirectories, _defaultFlags, flags: 0);

            Assert.IsTrue(heap.CreationFlags.HasFlag(HeapCreation.Shared), "Heap should be created with Shared flag");

            TestBasicHeapApi(heap);
        }

        [TestMethod()]
        public void LoadNativeWindowsHeap()
        {
            if (OperatingSystem.IsWindows())
            {
                using Win32PrivateHeap heap = Win32PrivateHeap.Create(MemoryUtil.SHARED_HEAP_INIT_SIZE, _defaultFlags, flags: 0);

                Assert.IsTrue(heap.CreationFlags.HasFlag(HeapCreation.Shared), "Heap should be created with Shared flag");

                TestBasicHeapApi(heap);
            }
            else
            {
                Assert.Inconclusive("This test is only applicable on Windows platforms.");
            }
        }

        [TestMethod]
        public void LoadNativePlatformHeap()
        {
            using ProcessHeap heap = new (HeapCreation.Shared);

            Assert.IsTrue(heap.CreationFlags.HasFlag(HeapCreation.Shared), "Heap should be created with Shared flag");

            TestBasicHeapApi(heap);
        }

        private static void TestBasicHeapApi(IUnmanagedHeap heap)
        {            
            TestAllocAndFreeWithSizes(heap, elements: 0); // Test zero elements allocation
            TestAllocAndFreeWithSizes(heap, elements: 1);
            TestAllocAndFreeWithSizes(heap, elements: 10);
            TestAllocAndFreeWithSizes(heap, elements: 100);
            TestAllocAndFreeWithSizes(heap, elements: 1000);
            TestAllocAndFreeWithSizes(heap, elements: 10000);
            TestAllocAndFreeWithSizes(heap, elements: 100000);
        }

        private static void TestAllocAndFreeWithSizes(IUnmanagedHeap heap, nuint elements)
        {
            //Test reallocations
            DoAllocAndResize(heap, elements, sizeof(byte), false);
            DoAllocAndResize(heap, elements, sizeof(sbyte), false);
            DoAllocAndResize(heap, elements, sizeof(short), false);
            DoAllocAndResize(heap, elements, sizeof(ushort), false);
            DoAllocAndResize(heap, elements, sizeof(int), false);
            DoAllocAndResize(heap, elements, sizeof(uint), false);
            DoAllocAndResize(heap, elements, sizeof(long), false);
            DoAllocAndResize(heap, elements, sizeof(ulong), false);
            DoAllocAndResize(heap, elements, sizeof(float), false);
            DoAllocAndResize(heap, elements, sizeof(double), false);
            DoAllocAndResize(heap, elements, (nuint)IntPtr.Size, false);

            //Test zeroed reallocations
            DoAllocAndResize(heap, elements, sizeof(byte), true);
            DoAllocAndResize(heap, elements, sizeof(sbyte), true);
            DoAllocAndResize(heap, elements, sizeof(short), true);
            DoAllocAndResize(heap, elements, sizeof(ushort), true);
            DoAllocAndResize(heap, elements, sizeof(int), true);
            DoAllocAndResize(heap, elements, sizeof(uint), true);
            DoAllocAndResize(heap, elements, sizeof(long), true);
            DoAllocAndResize(heap, elements, sizeof(ulong), true);
            DoAllocAndResize(heap, elements, sizeof(float), true);
            DoAllocAndResize(heap, elements, sizeof(double), true);
            DoAllocAndResize(heap, elements, (nuint)IntPtr.Size, true);
        }


        private static void DoAllocAndResize(IUnmanagedHeap heap, nuint elements, nuint size, bool zero)
        {
            //Allocate some memory
            IntPtr ptr = heap.Alloc(elements, size, zero);

            Assert.AreNotEqual(IntPtr.Zero, ptr, "Failed to allocate memory from the native heap");

            if ((heap.CreationFlags & HeapCreation.SupportsRealloc) > 0)
            {  
                //Resize the memory (always double the size even for zero initial elements
                heap.Resize(ref ptr, Math.Max(elements, 1) * 2, size, zero);

                Assert.AreNotEqual(IntPtr.Zero, ptr, "Failed to resize memory from the native heap");
            }
            else
            {
                Console.WriteLine("Heap does not support reallocations, skipping resize test.");
            }

            //Free the memory
            Assert.IsTrue(heap.Free(ref ptr));

            Assert.AreEqual(IntPtr.Zero, ptr, "Pointer should be null after freeing memory");
        }
    }
}