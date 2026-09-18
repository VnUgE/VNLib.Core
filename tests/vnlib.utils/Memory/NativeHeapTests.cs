/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: NativeHeapTests.cs 
*
* NativeHeapTests.cs is part of VNLib.UtilsTests which is part of the larger 
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
            using NativeHeap heap = NativeHeap.LoadHeap(
                RpMallocLibPath!, 
                DllImportSearchPath.SafeDirectories, 
                _defaultFlags, 
                flags: 0
            );

            Assert.IsTrue(heap.CreationFlags.HasFlag(HeapCreation.Shared), "Heap should be created with Shared flag");

            TestBasicHeapApi(heap);
        }

        [TestMethod()]
        public void LoadInTreeMimallocTest()
        {
            //Try to load Mimalloc shared heap
            using NativeHeap heap = NativeHeap.LoadHeap(
                MimallocLibPath!, 
                DllImportSearchPath.SafeDirectories, 
                _defaultFlags, 
                flags: 0
            );

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

        /// <summary>
        /// Tests that first-class heap support works for mimalloc and multi-threaded allocations
        /// don't cause corruption.
        /// </summary>
        [TestMethod]
        public void Mimalloc_FirstClass_SupportsMultithreaded()
        {
            using NativeHeap heap = NativeHeap.LoadHeap(
                MimallocLibPath!, 
                DllImportSearchPath.SafeDirectories, 
                creationFlags: HeapCreation.UseSynchronization, // default enable synchronization since we know it's multithreaded
                flags: 0
            );

            Assert.IsFalse(
                heap.CreationFlags.HasFlag(HeapCreation.Shared), 
                "Mimalloc heap should be private/first-class "
            );

            MultithreadedAllocAndFree(heap);
        }

        /// <summary>
        /// Tests that first-class heap support works for rpmalloc and multi-threaded allocations
        /// don't cause corruption.
        /// </summary>
        [TestMethod]
        public void RpMalloc_FirstClass_SupportsMultithreaded()
        {
            using NativeHeap heap = NativeHeap.LoadHeap(
                RpMallocLibPath!,
                DllImportSearchPath.SafeDirectories,
                creationFlags: HeapCreation.UseSynchronization,  // default enable synchronization since we know it's multithreaded
                flags: 0
            );

            Assert.IsFalse(
                heap.CreationFlags.HasFlag(HeapCreation.Shared),
                "rpmalloc heap should be private/first-class "
            );

            MultithreadedAllocAndFree(heap);
        }

       /// <summary>
       /// Attempts to allocate a bunch of blocks from the heap in parallel 
       /// then free them in parallel. Parallel should introduce some uncertainty 
       /// in which thread ids are used to allocate/free blocks testing the heaps 
       /// durability during-cross thread allocations/frees
       /// </summary>
       /// <param name="heap">The heap to test support for</param>
        private static void MultithreadedAllocAndFree(IUnmanagedHeap heap)
        {
            IntPtr[] blocks = new IntPtr[5000];

            Parallel.For(0, blocks.Length, (i) =>
            {
                blocks[i] = heap.Alloc((uint)i, sizeof(int), false);
                Assert.AreNotEqual(0, blocks[i]);
            });

            /*
             * Add some randomness to the ordering of the blocks to ensure that the loop 
             * does not have any ordering from alloc to free to help ensure that thread ID
             * used to alloc should be different for free.
             */
            Random.Shared.Shuffle(blocks);

            Parallel.For(0, blocks.Length, (i) =>
            {
                Assert.IsTrue(heap.Free(ref blocks[i]));
            });
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
