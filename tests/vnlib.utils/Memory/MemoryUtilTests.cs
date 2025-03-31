using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        const int ZERO_TEST_LOOP_ITERATIONS = 100000;
        const int ZERO_TEST_MAX_BUFFER_SIZE = 10 * 1024;

        [TestMethod]
        public void VerifySharedHeapTest()
        {
            IUnmangedHeap sharedHeap = MemoryUtil.Shared;

            //Ensure that this heap is a shared heap
            Assert.IsTrue(sharedHeap.CreationFlags.HasFlag(HeapCreation.Shared));

            /*
             * All other flags are library specific usually. The library can choose to set 
             * or clear flags like globalzero, synchronization, and realloc support.
             */
        }

        [TestMethod()]
        public void InitializeNewHeapForProcessTest()
        {
            //Test default private heap allocation
            using (IUnmangedHeap heap = MemoryUtil.InitializeNewHeapForProcess())
            {
                //Ensure that this heap is a private heap (not shared) and global zero is not set
                Assert.IsFalse(heap.CreationFlags.HasFlag(HeapCreation.Shared));
                Assert.IsFalse(heap.CreationFlags.HasFlag(HeapCreation.GlobalZero));

                //Test alloc
                IntPtr block = heap.Alloc(1, 1, zero: false);

                //Ensure the block was allocated should always happen, but catch it just in case
                Assert.AreNotEqual(IntPtr.Zero, block);

                //Free block
                heap.Free(ref block);
            }

            /*
             * Test with global zero set
             * 
             * This test is fairly inconclusive since this test cant guaruntee that the heap 
             * will have dirty memory since tests are so short. It's also not reasonable to 
             * alloc a bunch of blocks, dirty them and return it to the heap because it can't 
             * be guarunteed the heap imp will return blocks from the dirty area. 
             */
            using (IUnmangedHeap heap = MemoryUtil.InitializeNewHeapForProcess(globalZero: true))
            {
                //Ensure that this heap is a private heap (not shared) and global zero is set
                Assert.IsFalse(heap.CreationFlags.HasFlag(HeapCreation.Shared));
                Assert.IsTrue(heap.CreationFlags.HasFlag(HeapCreation.GlobalZero));

                //Test alloc with zero flag unset
                IntPtr block = heap.Alloc((nuint)Environment.SystemPageSize, sizeof(byte), zero: false);

                //Ensure the block was allocated should always happen, but catch it just in case
                Assert.AreNotEqual(IntPtr.Zero, block);

                //Ensure block is zeroed even when zero is false
                Span<byte> blockSpan = MemoryUtil.GetSpan<byte>(block, Environment.SystemPageSize * sizeof(byte));
                Assert.IsTrue(AllZero(blockSpan));

                //Free block
                heap.Free(ref block);
            }
        }

        private static bool AllZero<T>(Span<T> span) where T : struct
            => AllZero((ReadOnlySpan<T>)span);

        private static bool AllZero<T>(ReadOnlySpan<T> span)
            where T : struct
        {
            ReadOnlySpan<byte> asBytes = MemoryMarshal.Cast<T, byte>(span);

            for (int i = 0; i < asBytes.Length; i++)
            {
                if (asBytes[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        [TestMethod()]
        public void UnsafeZeroMemoryTest()
        {
            TestZeroWithDataType<byte>();
            TestZeroWithDataType<sbyte>();
            TestZeroWithDataType<short>();
            TestZeroWithDataType<ushort>();
            TestZeroWithDataType<int>();
            TestZeroWithDataType<uint>();
            TestZeroWithDataType<long>();
            TestZeroWithDataType<ulong>();
            TestZeroWithDataType<float>();
            TestZeroWithDataType<double>();
            TestZeroWithDataType<decimal>();
            TestZeroWithDataType<char>();
            TestZeroWithDataType<bool>();
            TestZeroWithDataType<nint>();
            TestZeroWithDataType<nuint>();
            TestZeroWithDataType<TestStruct>();
        }


        private static void TestZeroWithDataType<T>()
            where T : struct
        {
            Trace.WriteLine($"Testing unsafe zero with data type {typeof(T).Name}");

            //Get a random buffer that is known to not be all zeros of a given data type
            ReadOnlySpan<T> buffer = MemoryMarshal.Cast<byte, T>(RandomNumberGenerator.GetBytes(1024));

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
        public unsafe void InitializeBlockPointerTest()
        {
            const int blockSize = 64;

            //We want to zero a block of memory by its pointer

            //Heap alloc a block of memory
            IntPtr ptr = MemoryUtil.Shared.Alloc(blockSize, sizeof(byte), false);

            Span<byte> block = MemoryUtil.GetSpan<byte>(ptr, blockSize);

            //Fill with random data
            RandomNumberGenerator.Fill(block);

            //Make sure the block is not all zero
            Assert.IsFalse(AllZero(block));

            //Zero the block using the pointer overloads (this will call the typed pointer overload)
            MemoryUtil.InitializeBlock<byte>(ptr, blockSize);

            //Confrim all zero
            Assert.IsTrue(AllZero(block));
        }

        unsafe struct BigStruct
        {
            public fixed byte Data[1000];
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

                //Test references are equal
                ref byte spanRef = ref MemoryMarshal.GetReference(handle.Span);
                ref byte handleRef = ref handle.GetReference();
                Assert.IsTrue(Unsafe.AreSame(ref spanRef, ref handleRef));

                //Test span pointer against pinned handle
                using (MemoryHandle pinned = handle.Pin(0))
                {
                    fixed (void* ptr = &spanRef)
                    {
                        Assert.IsTrue(pinned.Pointer == ptr);
                    }
                }

                //Test negative pin
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

                //Test pinned outsie handle size
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.Pin(1024));
            }

            using (UnsafeMemoryHandle<BigStruct> handle = MemoryUtil.UnsafeAlloc<BigStruct>(1024))
            {
                _ = handle.Span;
                _ = handle.Length;
                _ = handle.IntLength;
                ref BigStruct handleRef = ref handle.GetReference();
                ref BigStruct spanRef = ref MemoryMarshal.GetReference(handle.Span);

                //Test references are equal
                Assert.IsTrue(Unsafe.AreSame(ref spanRef, ref handleRef));

                //Test span pointer against pinned handle
                using (MemoryHandle pinned = handle.Pin(11))
                {
                    fixed (BigStruct* ptr = &spanRef)
                    {
                        void* offset = ptr + 11;
                        Assert.IsTrue(pinned.Pointer == offset);
                    }
                }

                //Test negative pin
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

                //Test pinned outsie handle size
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.Pin(1024));
            }

            //Use the byte only overload
            using (UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAlloc(1024))
            { }

            //test against negative number
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = MemoryUtil.UnsafeAlloc<byte>(-1));

            //Alloc large block test (100mb)
            const int largTestSize = 100000 * 1024;
            //Alloc super small block
            const int smallTestSize = 5;

            using (UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(largTestSize, false))
            {
                Assert.AreEqual(largTestSize, buffer.IntLength);
                Assert.AreEqual(largTestSize, buffer.Span.Length);

                buffer.Span[0] = 254;
                Assert.AreEqual(254, buffer.Span[0]);
            }

            using (UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc<byte>(smallTestSize, false))
            {
                Assert.AreEqual(smallTestSize, buffer.IntLength);
                Assert.AreEqual(smallTestSize, buffer.Span.Length);

                buffer.Span[0] = 254;
                Assert.AreEqual(254, buffer.Span[0]);
            }

            //Different data type
            using (UnsafeMemoryHandle<long> buffer = MemoryUtil.UnsafeAlloc<long>(largTestSize, false))
            {
                Assert.AreEqual(largTestSize, buffer.IntLength);
                Assert.AreEqual(largTestSize, buffer.Span.Length);

                buffer.Span[0] = long.MaxValue;
                Assert.AreEqual(long.MaxValue, buffer.Span[0]);
            }

            using (UnsafeMemoryHandle<long> buffer = MemoryUtil.UnsafeAlloc<long>(smallTestSize, false))
            {
                Assert.AreEqual(smallTestSize, buffer.IntLength);
                Assert.AreEqual(smallTestSize, buffer.Span.Length);

                buffer.Span[0] = long.MaxValue;
                Assert.AreEqual(long.MaxValue, buffer.Span[0]);
            }

            //Test empty handle
            using (UnsafeMemoryHandle<byte> empty = new())
            {
                Assert.AreEqual(0ul, empty.Length);
                Assert.AreEqual(0, empty.IntLength);

                //Test pinning while empty
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = empty.Pin(0));
            }

            //Negative value
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = MemoryUtil.UnsafeAlloc<byte>(-1));


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
                _ = handle.GetReference();

                //Test span pointer against pinned handle
                using (MemoryHandle pinned = handle.Pin(0))
                {
                    fixed (void* ptr = &MemoryMarshal.GetReference(handle.Span))
                    {
                        Assert.IsTrue(pinned.Pointer == ptr);
                    }
                }

                //Test references are equal
                ref byte spanRef = ref MemoryMarshal.GetReference(handle.Span);
                ref byte handleRef = ref handle.GetReference();
                Assert.IsTrue(Unsafe.AreSame(ref spanRef, ref handleRef));

                //Test negative pin
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

                //Test pinned outsie handle size
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = handle.Pin(1024));
            }

            //Negative value
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = MemoryUtil.SafeAlloc<byte>(-1));

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
            Assert.AreEqual(10, s->X);
            Assert.AreEqual(20, s->Y);
            //zero struct
            MemoryUtil.ZeroStruct(s);
            //Verify data was zeroed
            Assert.AreEqual(0, s->X);
            Assert.AreEqual(0, s->Y);
            //Free struct
            MemoryUtil.Shared.StructFree(s);
        }

        [TestMethod()]
        public unsafe void ZeroStructAsVoidPointerTest()
        {
            TestStruct* s = MemoryUtil.Shared.StructAlloc<TestStruct>();
            s->X = 10;
            s->Y = 20;
            Assert.AreEqual(10, s->X);
            Assert.AreEqual(20, s->Y);
            //zero struct
            MemoryUtil.ZeroStruct<TestStruct>((void*)s);
            //Verify data was zeroed
            Assert.AreEqual(0, s->X);
            Assert.AreEqual(0, s->Y);
            //Free struct
            MemoryUtil.Shared.StructFree(s);
        }

        [TestMethod()]
        public unsafe void ZeroStructAsIntPtrTest()
        {
            TestStruct* s = MemoryUtil.Shared.StructAlloc<TestStruct>();
            s->X = 10;
            s->Y = 20;
            Assert.AreEqual(10, s->X);
            Assert.AreEqual(20, s->Y);
            //zero struct
            MemoryUtil.ZeroStruct<TestStruct>((IntPtr)s);
            //Verify data was zeroed
            Assert.AreEqual(0, s->X);
            Assert.AreEqual(0, s->Y);
            //Free struct
            MemoryUtil.Shared.StructFree(s);
        }

        [TestMethod()]
        public void GetSharedHeapStatsTest()
        {
            //Confirm heap diagnostics are enabled
            Assert.AreEqual<string?>("1", Environment.GetEnvironmentVariable(MemoryUtil.SHARED_HEAP_ENABLE_DIAGNOISTICS_ENV));

            //Get current stats
            HeapStatistics preTest = MemoryUtil.GetSharedHeapStats();

            //Alloc block
            using IMemoryHandle<byte> handle = MemoryUtil.Shared.Alloc<byte>(1024);

            //Get stats
            HeapStatistics postTest = MemoryUtil.GetSharedHeapStats();

            Assert.IsFalse(postTest == default);
            Assert.AreEqual(preTest.AllocatedBytes + 1024, postTest.AllocatedBytes);
            Assert.AreEqual(preTest.AllocatedBlocks + 1, postTest.AllocatedBlocks);

            //Free block
            handle.Dispose();

            //Get stats
            HeapStatistics postFree = MemoryUtil.GetSharedHeapStats();

            //Confirm stats are back to pre test
            Assert.AreEqual(postFree.AllocatedBytes, preTest.AllocatedBytes);
            Assert.AreEqual(postFree.AllocatedBlocks, preTest.AllocatedBlocks);
        }

        [TestMethod()]
        public void DiagnosticsHeapWraperTest()
        {
            //Get a fresh heap
            IUnmangedHeap heap = MemoryUtil.InitializeNewHeapForProcess();

            //Init wrapper and dispose
            using TrackedHeapWrapper wrapper = new(heap, true);

            //Confirm 0 stats
            HeapStatistics preTest = wrapper.GetCurrentStats();

            Assert.AreEqual(0ul, preTest.AllocatedBytes);
            Assert.AreEqual(0ul, preTest.AllocatedBlocks);
            Assert.AreEqual(0ul, preTest.MaxHeapSize);
            Assert.AreEqual(0ul, preTest.MaxBlockSize);
            Assert.AreEqual(ulong.MaxValue, preTest.MinBlockSize);

            //Alloc a test block
            using IMemoryHandle<byte> handle = wrapper.Alloc<byte>(1024);

            //Get stats
            HeapStatistics postTest = wrapper.GetCurrentStats();

            //Confirm stats represent a single block
            Assert.AreEqual(1024ul, postTest.AllocatedBytes);
            Assert.AreEqual(1ul, postTest.AllocatedBlocks);
            Assert.AreEqual(1024ul, postTest.MaxHeapSize);
            Assert.AreEqual(1024ul, postTest.MaxBlockSize);
            Assert.AreEqual(1024ul, postTest.MinBlockSize);

            //Free the block
            handle.Dispose();

            //Get stats
            HeapStatistics postFree = wrapper.GetCurrentStats();

            //Confirm stats are back to 0, or represent the single block
            Assert.AreEqual(0ul, postFree.AllocatedBytes);
            Assert.AreEqual(0ul, postFree.AllocatedBlocks);
            Assert.AreEqual(1024ul, postFree.MaxHeapSize);
            Assert.AreEqual(1024ul, postFree.MaxBlockSize);
            Assert.AreEqual(1024ul, postFree.MinBlockSize);
        }

        [TestMethod()]
        public void NearestPageTest()
        {
            //Test less than 1 page
            const nint TEST_1 = 458;

            nint pageSize = MemoryUtil.NearestPage(TEST_1);

            //Confirm output is the system page size
            Assert.AreEqual(Environment.SystemPageSize, pageSize);

            //Test over 1 page
            nint TEST_2 = Environment.SystemPageSize + 1;

            pageSize = MemoryUtil.NearestPage(TEST_2);

            //Should be 2 pages
            Assert.AreEqual(2 * Environment.SystemPageSize, pageSize);

            //Exactly one page
            pageSize = MemoryUtil.NearestPage(Environment.SystemPageSize);

            Assert.AreEqual(Environment.SystemPageSize, pageSize);
        }


        [TestMethod()]
        public void SimpleCopyTest()
        {
            const int blockSize = 10 * 1024;

            using IMemoryHandle<byte> dest = MemoryUtil.SafeAlloc<byte>(blockSize, false);
            using IMemoryHandle<byte> src = MemoryUtil.SafeAlloc<byte>(blockSize, false);

            Assert.AreEqual(dest.Length, src.Length);

            //Fill source with random data
            RandomNumberGenerator.Fill(src.Span);

            //Copy
            MemoryUtil.Copy(src, 0, dest, 0, blockSize);

            //Confirm data is the same
            Assert.IsTrue(src.Span.SequenceEqual(dest.Span));

            //try with array
            byte[] destArray = new byte[blockSize];
            MemoryUtil.CopyArray(src, 0, destArray, 0, blockSize);

            //Confirm data is the same
            Assert.IsTrue(src.Span.SequenceEqual(destArray));
        }

        [TestMethod()]
        public void AllocNearestPage()
        {
            //Simple alloc test

            const int TEST_1 = 1;

            //Unsafe byte test
            using (UnsafeMemoryHandle<byte> byteBuffer = MemoryUtil.UnsafeAllocNearestPage(TEST_1, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(byteBuffer);

                //Confirm byte size is working also
                Assert.AreEqual(byteBuffer.Length, byteSize);

                //Should be the same as the page size
                Assert.AreEqual((nuint)Environment.SystemPageSize, byteSize);
            }

            using (IMemoryHandle<byte> safeByteBuffer = MemoryUtil.SafeAllocNearestPage(TEST_1, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(safeByteBuffer);

                //Confirm byte size is working also
                Assert.AreEqual(safeByteBuffer.Length, byteSize);

                //Should be the same as the page size
                Assert.AreEqual((nuint)Environment.SystemPageSize, byteSize);
            }

            //Unsafe byte test with generics
            using (UnsafeMemoryHandle<byte> byteBuffer = MemoryUtil.UnsafeAllocNearestPage<byte>(TEST_1, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(byteBuffer);

                //Confirm byte size is working also
                Assert.AreEqual(byteBuffer.Length, byteSize);

                //Should be the same as the page size
                Assert.AreEqual((nuint)Environment.SystemPageSize, byteSize);
            }

            using (IMemoryHandle<byte> safeByteBuffer = MemoryUtil.SafeAllocNearestPage<byte>(TEST_1, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(safeByteBuffer);

                //Confirm byte size is working also
                Assert.AreEqual(safeByteBuffer.Length, byteSize);

                //Should be the same as the page size
                Assert.AreEqual((nuint)Environment.SystemPageSize, byteSize);
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
                Assert.AreNotEqual(byteBuffer.Length, byteSize);

                //Should be the same as the page size
                Assert.AreEqual((nuint)(Environment.SystemPageSize * 2), byteSize);
            }

            using (IMemoryHandle<int> safeByteBuffer = MemoryUtil.SafeAllocNearestPage<int>(TEST_2, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(safeByteBuffer);

                //Confirm byte size is working also
                Assert.AreNotEqual(safeByteBuffer.Length, byteSize);

                //Should be the same as the page size
                Assert.AreEqual((nuint)(Environment.SystemPageSize * 2), byteSize);
            }
        }

        [TestMethod]
        public unsafe void HeapAllocStructureRefTest()
        {
            //Alloc a structure as a reference
            ref TestStruct str = ref MemoryUtil.StructAllocRef<TestStruct>(MemoryUtil.Shared, true);

            str.X = 899;
            str.Y = 458;

            //recover a pointer from the reference
            TestStruct* ptr = (TestStruct*)Unsafe.AsPointer(ref str);

            //Confirm the values modified on the ref are reflected in the pointer deref
            Assert.AreEqual(899, ptr->X);
            Assert.AreEqual(458, ptr->Y);

            //We can confirm the references are the same 
            Assert.IsTrue(Unsafe.AreSame(ref (*ptr), ref str));

            //free the structure
            MemoryUtil.StructFreeRef(MemoryUtil.Shared, ref str);
        }

        [TestMethod]
        public unsafe void StructCopyTest()
        {
            //Alloc a structure as a reference
            ref TestStruct test1 = ref MemoryUtil.Shared.StructAllocRef<TestStruct>(true);

            //Confirm test 1 is zeroed
            Assert.AreEqual(0, test1.X);
            Assert.AreEqual(0, test1.Y);

            test1.X = 899;
            test1.Y = 458;

            //Test 2 blongs on the stack
            TestStruct test2 = default;
            MemoryUtil.CloneStruct(ref test1, ref test2);

            Assert.AreEqual(test2.X, test1.X);
            Assert.AreEqual(test2.Y, test1.Y);

            //Zero out test 1
            MemoryUtil.ZeroStruct(ref test1);

            //Confirm test 1 is zeroed
            Assert.AreEqual(0, test1.X);
            Assert.AreEqual(0, test1.Y);

            //Confirm test 2 is unmodified
            Assert.AreEqual(899, test2.X);
            Assert.AreEqual(458, test2.Y);

            //Alloc buffer to write struct data to
            Span<byte> buffer = stackalloc byte[sizeof(TestStruct)];
            MemoryUtil.CopyStruct(ref test2, buffer);

            //Read the x value as the first integer within the buffer
            int xCoord = MemoryMarshal.Read<int>(buffer);
            Assert.AreEqual(899, xCoord);

            //Clone to test 3 (stack alloc struct)
            TestStruct test3 = default;
            MemoryUtil.CopyStruct(buffer, ref test3);

            //Check that test2 and test3 are the same
            Assert.AreEqual(test3.X, test2.X);
            Assert.AreEqual(test3.Y, test2.Y);

            //Copy back to test 1 
            MemoryUtil.CopyStruct(buffer, ref test1);

            //Check that test1 and test2 are now the same
            Assert.AreEqual(test2.X, test1.X);
            Assert.AreEqual(test2.Y, test1.Y);

            //free the structure
            MemoryUtil.StructFreeRef(MemoryUtil.Shared, ref test1);
        }

        [TestMethod]
        public void CopyArgsTest()
        {
            /*
             * Testing the MemoryUtil.Copy/CopyArray family of functions and their overloads
             */

            using IMemoryHandle<byte> testHandle = MemoryUtil.SafeAlloc<byte>(16, false);
            using IMemoryHandle<byte> emptyHandle = new MemoryHandle<byte>();
            byte[] testArray = new byte[16];

            ReadOnlyMemory<byte> testMem = testArray;
            Memory<byte> testMem2 = testArray;

            /*
             * Test null reference checks
             */

#pragma warning disable CS8625, CS8600 // Cannot convert null literal to non-nullable reference type.

            Assert.ThrowsExactly<ArgumentNullException>(() => MemoryUtil.Copy((IMemoryHandle<byte>)null, 0, testHandle, 0, 1));

            Assert.ThrowsExactly<ArgumentNullException>(() => MemoryUtil.Copy(testHandle, 0, (IMemoryHandle<byte>)null, 0, 1));

            Assert.ThrowsExactly<ArgumentNullException>(() => MemoryUtil.Copy(ReadOnlyMemory<byte>.Empty, 0, null, 0, 1));
            Assert.ThrowsExactly<ArgumentNullException>(() => MemoryUtil.Copy(ReadOnlySpan<byte>.Empty, 0, null, 0, 1));

            Assert.ThrowsExactly<ArgumentNullException>(() => MemoryUtil.CopyArray((IMemoryHandle<byte>)null, 0, testArray, 0, 1));
            Assert.ThrowsExactly<ArgumentNullException>(() => MemoryUtil.CopyArray(testHandle, 0, null, 0, 1));

            Assert.ThrowsExactly<ArgumentNullException>(() => MemoryUtil.CopyArray(null, 0, testHandle, 0, 1));
            Assert.ThrowsExactly<ArgumentNullException>(() => MemoryUtil.CopyArray(testArray, 0, (byte[])null, 0, 1));

#pragma warning restore CS8625, CS8600 // Cannot convert null literal to non-nullable reference type.


            /*
             * Test out of range values for empty blocks
             */
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, emptyHandle, 0, 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(emptyHandle, 0, testHandle, 0, 1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, Memory<byte>.Empty, 0, 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(ReadOnlyMemory<byte>.Empty, 0, testHandle, 0, 1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, Span<byte>.Empty, 0, 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(ReadOnlySpan<byte>.Empty, 0, testHandle, 0, 1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(Array.Empty<byte>(), 0, testHandle, 0, 1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testHandle, 0, Array.Empty<byte>(), 0, 1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(Array.Empty<byte>(), 0, Array.Empty<byte>(), 0, 1));



            /*
             * Check for out of range with valid handles
             */
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testHandle, 0, 17));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testHandle, 1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 1, testHandle, 0, 16));

            //Test with real values using memory
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2, 0, 17));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2, 1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 1, testMem2, 0, 16));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, 0, testHandle, 0, 17));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, 0, testHandle, 1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, 1, testHandle, 0, 16));

            //Test with real values using span
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2.Span, 0, 17));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2.Span, 1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 1, testMem2.Span, 0, 16));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, 0, testHandle, 0, 17));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, 0, testHandle, 1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, 1, testHandle, 0, 16));

            //Test with real values using array
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testHandle, 0, testArray, 0, 17));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testHandle, 0, testArray, 1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testHandle, 1, testArray, 0, 16));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testArray, 0, testHandle, 0, 17));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testArray, 0, testHandle, 1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testArray, 1, testHandle, 0, 16));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testArray, 0, new byte[16], 0, 17));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(new byte[16], 0, testArray, 1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(new byte[16], 1, testArray, 0, 16));


            /*
             * Test inbounds test values
             */
            MemoryUtil.Copy(testHandle, 0, testHandle, 0, 16);
            MemoryUtil.Copy(testHandle, 0, testHandle, 1, 15);
            MemoryUtil.Copy(testHandle, 1, testHandle, 0, 15);

            //Memory-inbounds
            MemoryUtil.Copy(testHandle, 0, testMem2, 0, 16);
            MemoryUtil.Copy(testHandle, 0, testMem2, 1, 15);
            MemoryUtil.Copy(testHandle, 1, testMem2, 0, 15);

            MemoryUtil.Copy(testMem, 0, testHandle, 0, 16);
            MemoryUtil.Copy(testMem, 0, testHandle, 1, 15);
            MemoryUtil.Copy(testMem, 1, testHandle, 0, 15);

            //Span in-bounds
            MemoryUtil.Copy(testHandle, 0, testMem2.Span, 0, 16);
            MemoryUtil.Copy(testHandle, 0, testMem2.Span, 1, 15);
            MemoryUtil.Copy(testHandle, 1, testMem2.Span, 0, 15);

            MemoryUtil.Copy(testMem.Span, 0, testHandle, 0, 16);
            MemoryUtil.Copy(testMem.Span, 0, testHandle, 1, 15);
            MemoryUtil.Copy(testMem.Span, 1, testHandle, 0, 15);

            //Array in-bounds
            MemoryUtil.CopyArray(testHandle, 0, testArray, 0, 16);
            MemoryUtil.CopyArray(testHandle, 0, testArray, 1, 15);
            MemoryUtil.CopyArray(testHandle, 1, testArray, 0, 15);


            MemoryUtil.CopyArray(testArray, 0, testHandle, 0, 16);
            MemoryUtil.CopyArray(testArray, 0, testHandle, 1, 15);
            MemoryUtil.CopyArray(testArray, 1, testHandle, 0, 15);

            /*
             * Test nop for zero-length copies
             */

            MemoryUtil.Copy(testHandle, 0, testHandle, 0, 0);
            MemoryUtil.Copy(testHandle, 0, testMem2, 0, 0);
            MemoryUtil.Copy(testHandle, 0, testMem2.Span, 0, 0);
            MemoryUtil.Copy(testMem, 0, testHandle, 0, 0);
            MemoryUtil.Copy(testMem.Span, 0, testHandle, 0, 0);
            MemoryUtil.CopyArray(testHandle, 0, testArray, 0, 0);
            MemoryUtil.CopyArray(testArray, 0, testHandle, 0, 0);
            MemoryUtil.CopyArray(testArray, 0, [], 0, 0);

            /*
             * Test negative values for span/memory overloads that 
             * accept integers
             */

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, -1, testMem2, 0, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2, -1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2, 0, -1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, -1, testHandle, 0, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, 0, testHandle, 0, -1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, -1, testMem2.Span, 0, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2.Span, -1, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2.Span, 0, -1));

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, -1, testHandle, 0, 16));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, 0, testHandle, 0, -1));
        }

        [TestMethod]
        public unsafe void ByteSizeTest()
        {
            Assert.AreEqual(
                expected: 16,
                actual: MemoryUtil.ByteCount<byte>(16)
            );

            Assert.AreEqual(
                expected: 16 * sizeof(int),
                actual: MemoryUtil.ByteCount<int>(16)               
             );

            Assert.AreEqual(
                expected: 16 * sizeof(long),
                actual: MemoryUtil.ByteCount<long>(16)
            );

            Assert.AreEqual(
                expected: 16 * sizeof(float),
                actual: MemoryUtil.ByteCount<float>(16)
            );

            Assert.AreEqual(
                expected: 16 * sizeof(double),
                actual: MemoryUtil.ByteCount<double>(16)
            );

            Assert.AreEqual(
                actual: MemoryUtil.ByteCount<nint>(16),
                expected: 16 * sizeof(nint)
            );

            Assert.AreEqual(
                actual: MemoryUtil.ByteCount<TestStruct>(16),
                expected: 16 * sizeof(TestStruct)
            );

            Assert.AreEqual(
                expected: 0, 
                actual: MemoryUtil.ByteCount<TestStruct>(0)
            );
        }
    }
}