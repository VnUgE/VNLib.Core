
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Diagnostics;

namespace VNLib.Utils.Memory.Tests
{
    [TestClass()]
    public class MemoryUtilTests
    {
        const int ZERO_TEST_LOOP_ITERATIONS = 1000000;
        const int ZERO_TEST_MAX_BUFFER_SIZE = 10 * 1024;

        [TestMethod()]
        public void InitializeNewHeapForProcessTest()
        {
            //Check if rpmalloc is loaded
            if (MemoryUtil.IsRpMallocLoaded)
            {
                //Initialize the heap
                using IUnmangedHeap heap = MemoryUtil.InitializeNewHeapForProcess();

                //Confirm that the heap is actually a rpmalloc heap
                Assert.IsInstanceOfType(heap, typeof(RpMallocPrivateHeap));
            }
            else
            {
                //Confirm that Rpmalloc will throw DLLNotFound if the lib is not loaded
                Assert.ThrowsException<DllNotFoundException>(() => _ = RpMallocPrivateHeap.GlobalHeap.Alloc(1, 1, false));
            }
        }

        [TestMethod()]
        public void UnsafeZeroMemoryTest()
        {
            //Get random data buffer as a readonly span
            ReadOnlyMemory<byte> buffer = RandomNumberGenerator.GetBytes(1024);

            //confirm buffer is not all zero
            Assert.IsFalse(AllZero(buffer.Span));

            //Zero readonly memory
            MemoryUtil.UnsafeZeroMemory(buffer);

            //Confirm all zero
            Assert.IsTrue(AllZero(buffer.Span));
        }

        private static bool AllZero(ReadOnlySpan<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != 0)
                {
                    return false;
                }
            }
            return true;
        }

        [TestMethod()]
        public void UnsafeZeroMemoryTest1()
        {
            //Get random data buffer as a readonly span
            ReadOnlySpan<byte> buffer = RandomNumberGenerator.GetBytes(1024);

            //confirm buffer is not all zero
            Assert.IsFalse(AllZero(buffer));

            //Zero readonly span
            MemoryUtil.UnsafeZeroMemory(buffer);

            //Confirm all zero
            Assert.IsTrue(AllZero(buffer));
        }


        [TestMethod()]
        public void InitializeBlockAsSpanTest()
        {
            //Get random data buffer as a readonly span
            Span<byte> buffer = RandomNumberGenerator.GetBytes(1024);

            //confirm buffer is not all zero
            Assert.IsFalse(AllZero(buffer));

            //Zero readonly span
            MemoryUtil.InitializeBlock(buffer);

            //Confirm all zero
            Assert.IsTrue(AllZero(buffer));
        }

        [TestMethod()]
        public void InitializeBlockMemoryTest()
        {
            //Get random data buffer as a readonly span
            Memory<byte> buffer = RandomNumberGenerator.GetBytes(1024);

            //confirm buffer is not all zero
            Assert.IsFalse(AllZero(buffer.Span));

            //Zero readonly span
            MemoryUtil.InitializeBlock(buffer);

            //Confirm all zero
            Assert.IsTrue(AllZero(buffer.Span));
        }


        [TestMethod()]
        public unsafe void UnsafeAllocTest()
        {
            //No fail
            using (UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAlloc<byte>(1024))
            {
                _ = handle.Span;
                _ = handle.Length;
                _ = handle.IntLength;

                //Test span pointer against pinned handle
                using (MemoryHandle pinned = handle.Pin(0))
                {
                    fixed (void* ptr = &MemoryMarshal.GetReference(handle.Span))
                    {
                        Assert.IsTrue(ptr == pinned.Pointer);
                    }
                }

                //Test negative pin
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

                //Test pinned outsie handle size
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(1024));
            }

            //test against negative number
            Assert.ThrowsException<ArgumentException>(() => MemoryUtil.UnsafeAlloc<byte>(-1));

            //Alloc large block test (100mb)
            const int largTestSize = 100000 * 1024;
            //Alloc super small block
            const int smallTestSize = 5;

            using (UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(largTestSize, false))
            {
                Assert.IsTrue(largTestSize == buffer.IntLength);
                Assert.IsTrue(largTestSize == buffer.Span.Length);

                buffer.Span[0] = 254;
                Assert.IsTrue(buffer.Span[0] == 254);
            }

            using (UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(smallTestSize, false))
            {
                Assert.IsTrue(smallTestSize == buffer.IntLength);
                Assert.IsTrue(smallTestSize == buffer.Span.Length);

                buffer.Span[0] = 254;
                Assert.IsTrue(buffer.Span[0] == 254);
            }

            //Different data type
            using (UnsafeMemoryHandle<long> buffer = MemoryUtil.UnsafeAlloc<long>(largTestSize, false))
            {
                Assert.IsTrue(largTestSize == buffer.IntLength);
                Assert.IsTrue(largTestSize == buffer.Span.Length);

                buffer.Span[0] = long.MaxValue;
                Assert.IsTrue(buffer.Span[0] == long.MaxValue);
            }

            using (UnsafeMemoryHandle<long> buffer = MemoryUtil.UnsafeAlloc<long>(smallTestSize, false))
            {
                Assert.IsTrue(smallTestSize == buffer.IntLength);
                Assert.IsTrue(smallTestSize == buffer.Span.Length);

                buffer.Span[0] = long.MaxValue;
                Assert.IsTrue(buffer.Span[0] == long.MaxValue);
            }

            //Test empty handle
            using (UnsafeMemoryHandle<byte> empty = new())
            {
                Assert.IsTrue(0 == empty.Length);
                Assert.IsTrue(0 == empty.IntLength);

                //Test pinning while empty
                Assert.ThrowsException<InvalidOperationException>(() => _ = empty.Pin(0));
            }

            //Negative value
            Assert.ThrowsException<ArgumentException>(() => _ = MemoryUtil.UnsafeAlloc<byte>(-1));


            /*
             * Alloc random sized blocks in a loop, confirm they are empty
             * then fill the block with random data before freeing it back to 
             * the pool. This confirms that if blocks are allocated from a shared
             * pool are properly zeroed when requestd
             */

            for (int i = 0; i < ZERO_TEST_LOOP_ITERATIONS; i++)
            {
                int randBufferSize = Random.Shared.Next(1024, ZERO_TEST_MAX_BUFFER_SIZE);

                //Alloc block, check if all zero, then free
                using UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAlloc<byte>(randBufferSize, true);

                //Confirm all zero
                Assert.IsTrue(AllZero(handle.Span));

                //Fill with random data
                Random.Shared.NextBytes(handle.Span);
            }
        }

        [TestMethod()]
        public unsafe void SafeAllocTest()
        {
            //No fail
            using (IMemoryHandle<byte> handle = MemoryUtil.SafeAlloc<byte>(1024))
            {
                _ = handle.Span;
                _ = handle.Length;
                _ = handle.GetIntLength();

                //Test span pointer against pinned handle
                using (MemoryHandle pinned = handle.Pin(0))
                {
                    fixed (void* ptr = &MemoryMarshal.GetReference(handle.Span))
                    {
                        Assert.IsTrue(ptr == pinned.Pointer);
                    }
                }

                //Test negative pin
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

                //Test pinned outsie handle size
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(1024));
            }


            //Negative value
            Assert.ThrowsException<ArgumentException>(() => _ = MemoryUtil.SafeAlloc<byte>(-1));


            /*
             * Alloc random sized blocks in a loop, confirm they are empty
             * then fill the block with random data before freeing it back to 
             * the pool. This confirms that if blocks are allocated from a shared
             * pool are properly zeroed when requestd
             */

            for (int i = 0; i < ZERO_TEST_LOOP_ITERATIONS; i++)
            {
                int randBufferSize = Random.Shared.Next(1024, ZERO_TEST_MAX_BUFFER_SIZE);

                //Alloc block, check if all zero, then free
                using IMemoryHandle<byte> handle = MemoryUtil.SafeAlloc<byte>(randBufferSize, true);

                //Confirm all zero
                Assert.IsTrue(AllZero(handle.Span));

                //Fill with random data
                Random.Shared.NextBytes(handle.Span);
            }
        }


        [StructLayout(LayoutKind.Sequential)]
        struct TestStruct
        {
            public int X;
            public int Y;
        }

        [TestMethod()]
        public unsafe void ZeroStructAsPointerTest()
        {
            TestStruct* s = MemoryUtil.Shared.StructAlloc<TestStruct>();
            s->X = 10;
            s->Y = 20;
            Assert.IsTrue(10 == s->X);
            Assert.IsTrue(20 == s->Y);
            //zero struct
            MemoryUtil.ZeroStruct(s);
            //Verify data was zeroed
            Assert.IsTrue(0 == s->X);
            Assert.IsTrue(0 == s->Y);
            //Free struct
            MemoryUtil.Shared.StructFree(s);
        }

        [TestMethod()]
        public unsafe void ZeroStructAsVoidPointerTest()
        {
            TestStruct* s = MemoryUtil.Shared.StructAlloc<TestStruct>();
            s->X = 10;
            s->Y = 20;
            Assert.IsTrue(10 == s->X);
            Assert.IsTrue(20 == s->Y);
            //zero struct
            MemoryUtil.ZeroStruct<TestStruct>((void*)s);
            //Verify data was zeroed
            Assert.IsTrue(0 == s->X);
            Assert.IsTrue(0 == s->Y);
            //Free struct
            MemoryUtil.Shared.StructFree(s);
        }

        [TestMethod()]
        public unsafe void ZeroStructAsIntPtrTest()
        {
            TestStruct* s = MemoryUtil.Shared.StructAlloc<TestStruct>();
            s->X = 10;
            s->Y = 20;
            Assert.IsTrue(10 == s->X);
            Assert.IsTrue(20 == s->Y);
            //zero struct
            MemoryUtil.ZeroStruct<TestStruct>((IntPtr)s);
            //Verify data was zeroed
            Assert.IsTrue(0 == s->X);
            Assert.IsTrue(0 == s->Y);
            //Free struct
            MemoryUtil.Shared.StructFree(s);
        }

        [TestMethod()]
        public void GetSharedHeapStatsTest()
        {
            //Confirm heap diagnostics are enabled
            Assert.AreEqual<string?>(Environment.GetEnvironmentVariable(MemoryUtil.SHARED_HEAP_ENABLE_DIAGNOISTICS_ENV), "1");

            //Get current stats
            HeapStatistics preTest = MemoryUtil.GetSharedHeapStats();

            //Alloc block
            using IMemoryHandle<byte> handle = MemoryUtil.Shared.Alloc<byte>(1024);

            //Get stats
            HeapStatistics postTest = MemoryUtil.GetSharedHeapStats();

            Assert.IsTrue(postTest.AllocatedBytes == preTest.AllocatedBytes + 1024);
            Assert.IsTrue(postTest.AllocatedBlocks == preTest.AllocatedBlocks + 1);

            //Free block
            handle.Dispose();

            //Get stats
            HeapStatistics postFree = MemoryUtil.GetSharedHeapStats();

            //Confirm stats are back to pre test
            Assert.IsTrue(preTest.AllocatedBytes == postFree.AllocatedBytes);
            Assert.IsTrue(preTest.AllocatedBlocks == postFree.AllocatedBlocks);
        }

        [TestMethod()]
        public void DiagnosticsHeapWraperTest()
        {
            //Get a fresh heap
            IUnmangedHeap heap = MemoryUtil.InitializeNewHeapForProcess();

            //Init wrapper and dispose
            using TrackedHeapWrapper wrapper = new(heap);

            //Confirm 0 stats
            HeapStatistics preTest = wrapper.GetCurrentStats();

            Assert.IsTrue(preTest.AllocatedBytes == 0);
            Assert.IsTrue(preTest.AllocatedBlocks == 0);
            Assert.IsTrue(preTest.MaxHeapSize == 0);
            Assert.IsTrue(preTest.MaxBlockSize == 0);
            Assert.IsTrue(preTest.MinBlockSize == ulong.MaxValue);

            //Alloc a test block
            using IMemoryHandle<byte> handle = wrapper.Alloc<byte>(1024);

            //Get stats
            HeapStatistics postTest = wrapper.GetCurrentStats();

            //Confirm stats represent a single block
            Assert.IsTrue(postTest.AllocatedBytes == 1024);
            Assert.IsTrue(postTest.AllocatedBlocks == 1);
            Assert.IsTrue(postTest.MaxHeapSize == 1024);
            Assert.IsTrue(postTest.MaxBlockSize == 1024);
            Assert.IsTrue(postTest.MinBlockSize == 1024);

            //Free the block
            handle.Dispose();

            //Get stats
            HeapStatistics postFree = wrapper.GetCurrentStats();

            //Confirm stats are back to 0, or represent the single block
            Assert.IsTrue(postFree.AllocatedBytes == 0);
            Assert.IsTrue(postFree.AllocatedBlocks == 0);
            Assert.IsTrue(postFree.MaxHeapSize == 1024);
            Assert.IsTrue(postFree.MaxBlockSize == 1024);
            Assert.IsTrue(postFree.MinBlockSize == 1024);
        }

        [TestMethod()]
        public void NearestPageTest()
        {
            //Test less than 1 page
            const nint TEST_1 = 458;

            nint pageSize = MemoryUtil.NearestPage(TEST_1);

            //Confirm output is the system page size
            Assert.IsTrue(pageSize == Environment.SystemPageSize);

            //Test over 1 page
            nint TEST_2 = Environment.SystemPageSize + 1;

            pageSize = MemoryUtil.NearestPage(TEST_2);

            //Should be 2 pages
            Assert.IsTrue(pageSize == 2 * Environment.SystemPageSize);

            //Exactly one page
            pageSize = MemoryUtil.NearestPage(Environment.SystemPageSize);

            Assert.IsTrue(pageSize == Environment.SystemPageSize);
        }


        [TestMethod()]
        public void AllocNearestPage()
        {
            //Simple alloc test

            const int TEST_1 = 1;

            //Unsafe byte test
            using (UnsafeMemoryHandle<byte> byteBuffer = MemoryUtil.UnsafeAllocNearestPage<byte>(TEST_1, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(byteBuffer);

                //Confirm byte size is working also
                Assert.IsTrue(byteSize == byteBuffer.Length);

                //Should be the same as the page size
                Assert.IsTrue(byteSize == (nuint)Environment.SystemPageSize);
            }

            using(IMemoryHandle<byte> safeByteBuffer = MemoryUtil.SafeAllocNearestPage<byte>(TEST_1, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(safeByteBuffer);

                //Confirm byte size is working also
                Assert.IsTrue(byteSize == safeByteBuffer.Length);

                //Should be the same as the page size
                Assert.IsTrue(byteSize == (nuint)Environment.SystemPageSize);
            }

            /*
             * When using the Int32 a page size of 4096 would yield a space of 1024 Int32,
             * so allocating 1025 int32s should cause an overflow to the next page size
             */
            const int TEST_2 = 1025;

            //Test for different types
            using (UnsafeMemoryHandle<int> byteBuffer = MemoryUtil.UnsafeAllocNearestPage<int>(TEST_2, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(byteBuffer);

                //Confirm byte size is working also
                Assert.IsTrue(byteSize != byteBuffer.Length);

                //Should be the same as the page size
                Assert.IsTrue(byteSize == (nuint)(Environment.SystemPageSize * 2));
            }

            using (IMemoryHandle<int> safeByteBuffer = MemoryUtil.SafeAllocNearestPage<int>(TEST_2, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(safeByteBuffer);

                //Confirm byte size is working also
                Assert.IsTrue(byteSize != safeByteBuffer.Length);

                //Should be the same as the page size
                Assert.IsTrue(byteSize == (nuint)(Environment.SystemPageSize * 2));
            }
        }
    }
}