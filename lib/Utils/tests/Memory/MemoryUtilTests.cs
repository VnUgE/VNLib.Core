using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Extensions;

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
    }
}