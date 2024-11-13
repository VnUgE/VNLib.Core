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

        [TestMethod()]
        public void InitializeNewHeapForProcessTest()
        {

            //Initialize the heap
            using IUnmangedHeap heap = MemoryUtil.InitializeNewHeapForProcess();

            //Test alloc
            IntPtr block = heap.Alloc(1, 1, false);

            //Free block
            heap.Free(ref block);

            //TODO verify the heap type by loading a dynamic heap dll
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
            byte* ptr = (byte*)MemoryUtil.Shared.Alloc(blockSize, sizeof(byte), false);

            //Fill with random data
            RandomNumberGenerator.Fill(new Span<byte>(ptr, blockSize));

            //Make sure the block is not all zero
            Assert.IsFalse(AllZero(new ReadOnlySpan<byte>(ptr, blockSize)));

            //Zero the block
            MemoryUtil.InitializeBlock(ptr, blockSize);

            //Confrim all zero
            Assert.IsTrue(AllZero(new ReadOnlySpan<byte>(ptr, blockSize)));
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
                        Assert.IsTrue(ptr == pinned.Pointer);
                    }
                }

                //Test negative pin
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

                //Test pinned outsie handle size
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(1024));
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
                        Assert.IsTrue(offset == pinned.Pointer);
                    }
                }

                //Test negative pin
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

                //Test pinned outsie handle size
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(1024));
            }

            //Use the byte only overload
            using (UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAlloc(1024))
            { }

            //test against negative number
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.UnsafeAlloc<byte>(-1));

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
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = empty.Pin(0));
            }

            //Negative value
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = MemoryUtil.UnsafeAlloc<byte>(-1));


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
                        Assert.IsTrue(ptr == pinned.Pointer);
                    }
                }

                //Test references are equal
                ref byte spanRef = ref MemoryMarshal.GetReference(handle.Span);
                ref byte handleRef = ref handle.GetReference();
                Assert.IsTrue(Unsafe.AreSame(ref spanRef, ref handleRef));

                //Test negative pin
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(-1));

                //Test pinned outsie handle size
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = handle.Pin(1024));
            }

            //Negative value
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = MemoryUtil.SafeAlloc<byte>(-1));

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
            Assert.AreEqual<string?>("1", Environment.GetEnvironmentVariable(MemoryUtil.SHARED_HEAP_ENABLE_DIAGNOISTICS_ENV));

            //Get current stats
            HeapStatistics preTest = MemoryUtil.GetSharedHeapStats();

            //Alloc block
            using IMemoryHandle<byte> handle = MemoryUtil.Shared.Alloc<byte>(1024);

            //Get stats
            HeapStatistics postTest = MemoryUtil.GetSharedHeapStats();

            Assert.IsFalse(postTest == default);
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
            using TrackedHeapWrapper wrapper = new(heap, true);

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
        public void SimpleCopyTest()
        {
            const int blockSize = 10 * 1024;

            using IMemoryHandle<byte> dest = MemoryUtil.SafeAlloc<byte>(blockSize, false);
            using IMemoryHandle<byte> src = MemoryUtil.SafeAlloc<byte>(blockSize, false);

            Assert.IsTrue(src.Length == dest.Length);

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
                Assert.IsTrue(byteSize == byteBuffer.Length);

                //Should be the same as the page size
                Assert.IsTrue(byteSize == (nuint)Environment.SystemPageSize);
            }

            using (IMemoryHandle<byte> safeByteBuffer = MemoryUtil.SafeAllocNearestPage(TEST_1, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(safeByteBuffer);

                //Confirm byte size is working also
                Assert.IsTrue(byteSize == safeByteBuffer.Length);

                //Should be the same as the page size
                Assert.IsTrue(byteSize == (nuint)Environment.SystemPageSize);
            }

            //Unsafe byte test with generics
            using (UnsafeMemoryHandle<byte> byteBuffer = MemoryUtil.UnsafeAllocNearestPage<byte>(TEST_1, false))
            {
                nuint byteSize = MemoryUtil.ByteSize(byteBuffer);

                //Confirm byte size is working also
                Assert.IsTrue(byteSize == byteBuffer.Length);

                //Should be the same as the page size
                Assert.IsTrue(byteSize == (nuint)Environment.SystemPageSize);
            }

            using (IMemoryHandle<byte> safeByteBuffer = MemoryUtil.SafeAllocNearestPage<byte>(TEST_1, false))
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
            Assert.IsTrue(ptr->X == 899);
            Assert.IsTrue(ptr->Y == 458);

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
            Assert.IsTrue(test1.X == 0);
            Assert.IsTrue(test1.Y == 0);

            test1.X = 899;
            test1.Y = 458;

            //Test 2 blongs on the stack
            TestStruct test2 = default;
            MemoryUtil.CloneStruct(ref test1, ref test2);

            Assert.IsTrue(test1.X == test2.X);
            Assert.IsTrue(test1.Y == test2.Y);

            //Zero out test 1
            MemoryUtil.ZeroStruct(ref test1);

            //Confirm test 1 is zeroed
            Assert.IsTrue(test1.X == 0);
            Assert.IsTrue(test1.Y == 0);

            //Confirm test 2 is unmodified
            Assert.IsTrue(test2.X == 899);
            Assert.IsTrue(test2.Y == 458);

            //Alloc buffer to write struct data to
            Span<byte> buffer = stackalloc byte[sizeof(TestStruct)];
            MemoryUtil.CopyStruct(ref test2, buffer);

            //Read the x value as the first integer within the buffer
            int xCoord = MemoryMarshal.Read<int>(buffer);
            Assert.IsTrue(xCoord == 899);

            //Clone to test 3 (stack alloc struct)
            TestStruct test3 = default;
            MemoryUtil.CopyStruct(buffer, ref test3);

            //Check that test2 and test3 are the same
            Assert.IsTrue(test2.X == test3.X);
            Assert.IsTrue(test2.Y == test3.Y);

            //Copy back to test 1 
            MemoryUtil.CopyStruct(buffer, ref test1);

            //Check that test1 and test2 are now the same
            Assert.IsTrue(test1.X == test2.X);
            Assert.IsTrue(test1.Y == test2.Y);

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

            Assert.ThrowsException<ArgumentNullException>(() => MemoryUtil.Copy((IMemoryHandle<byte>)null, 0, testHandle, 0, 1));

            Assert.ThrowsException<ArgumentNullException>(() => MemoryUtil.Copy(testHandle, 0, (IMemoryHandle<byte>)null, 0, 1));

            Assert.ThrowsException<ArgumentNullException>(() => MemoryUtil.Copy(ReadOnlyMemory<byte>.Empty, 0, null, 0, 1));
            Assert.ThrowsException<ArgumentNullException>(() => MemoryUtil.Copy(ReadOnlySpan<byte>.Empty, 0, null, 0, 1));

            Assert.ThrowsException<ArgumentNullException>(() => MemoryUtil.CopyArray((IMemoryHandle<byte>)null, 0, testArray, 0, 1));
            Assert.ThrowsException<ArgumentNullException>(() => MemoryUtil.CopyArray(testHandle, 0, null, 0, 1));

            Assert.ThrowsException<ArgumentNullException>(() => MemoryUtil.CopyArray(null, 0, testHandle, 0, 1));
            Assert.ThrowsException<ArgumentNullException>(() => MemoryUtil.CopyArray(testArray, 0, (byte[])null, 0, 1));

#pragma warning restore CS8625, CS8600 // Cannot convert null literal to non-nullable reference type.


            /*
             * Test out of range values for empty blocks
             */
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, emptyHandle, 0, 1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(emptyHandle, 0, testHandle, 0, 1));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, Memory<byte>.Empty, 0, 1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(ReadOnlyMemory<byte>.Empty, 0, testHandle, 0, 1));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, Span<byte>.Empty, 0, 1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(ReadOnlySpan<byte>.Empty, 0, testHandle, 0, 1));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(Array.Empty<byte>(), 0, testHandle, 0, 1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testHandle, 0, Array.Empty<byte>(), 0, 1));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(Array.Empty<byte>(), 0, Array.Empty<byte>(), 0, 1));



            /*
             * Check for out of range with valid handles
             */
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testHandle, 0, 17));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testHandle, 1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 1, testHandle, 0, 16));

            //Test with real values using memory
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2, 0, 17));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2, 1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 1, testMem2, 0, 16));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, 0, testHandle, 0, 17));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, 0, testHandle, 1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, 1, testHandle, 0, 16));

            //Test with real values using span
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2.Span, 0, 17));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2.Span, 1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 1, testMem2.Span, 0, 16));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, 0, testHandle, 0, 17));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, 0, testHandle, 1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, 1, testHandle, 0, 16));

            //Test with real values using array
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testHandle, 0, testArray, 0, 17));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testHandle, 0, testArray, 1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testHandle, 1, testArray, 0, 16));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testArray, 0, testHandle, 0, 17));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testArray, 0, testHandle, 1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testArray, 1, testHandle, 0, 16));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(testArray, 0, new byte[16], 0, 17));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(new byte[16], 0, testArray, 1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.CopyArray(new byte[16], 1, testArray, 0, 16));


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

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, -1, testMem2, 0, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2, -1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2, 0, -1));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, -1, testHandle, 0, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem, 0, testHandle, 0, -1));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, -1, testMem2.Span, 0, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2.Span, -1, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testHandle, 0, testMem2.Span, 0, -1));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, -1, testHandle, 0, 16));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MemoryUtil.Copy(testMem.Span, 0, testHandle, 0, -1));
        }

        [TestMethod]
        public unsafe void ByteSizeTest()
        {
            Assert.AreEqual(
                MemoryUtil.ByteCount<byte>(16),
                actual: 16
            );

            Assert.AreEqual(
                MemoryUtil.ByteCount<int>(16),
                actual: 16 * sizeof(int)
             );

            Assert.AreEqual(
               MemoryUtil.ByteCount<long>(16),
               actual: 16 * sizeof(long)
            );

            Assert.AreEqual(
                MemoryUtil.ByteCount<float>(16),
                actual: 16 * sizeof(float)
            );

            Assert.AreEqual(
                MemoryUtil.ByteCount<double>(16),
                actual: 16 * sizeof(double)
            );

            Assert.AreEqual(
                MemoryUtil.ByteCount<nint>(16),
                actual: 16 * sizeof(nint)
            );

            Assert.AreEqual(
                MemoryUtil.ByteCount<TestStruct>(16),
                actual: 16 * sizeof(TestStruct)
            );

            Assert.AreEqual(
                MemoryUtil.ByteCount<TestStruct>(0),
                actual: 0
            );
        }
    }
}