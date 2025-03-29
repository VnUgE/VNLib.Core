using System;
using System.Buffers;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory.Tests
{
    [TestClass()]
    public class MemoryLockTests
    {
        private const int TEST_BLOCK_SIZE = 4096; // Typical page size

        [TestMethod]
        public void LockMemoryNintAddrTest()
        {
            // Only run on supported platforms
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("Test only runs on Windows or Linux");
                return;
            }

            // Allocate a block of memory           
            nint ptr = MemoryUtil.Shared.Alloc(TEST_BLOCK_SIZE, sizeof(byte), zero: true);
         
            // Should be able to lock on supported platforms
            Assert.IsTrue(
                MemoryUtil.LockMemory(ptr, TEST_BLOCK_SIZE), 
                message: "Failed to lock memory region"
            );

            //try to read and write
            Marshal.WriteByte(ptr, 0, 0x01);
            byte readByte = Marshal.ReadByte(ptr, 0);

            Assert.AreEqual(0x01, readByte, "Failed to write/read data to locked memory");

            // Try to unlock the memory          
            Assert.IsTrue(
                MemoryUtil.UnlockMemory(ptr, TEST_BLOCK_SIZE), 
                message: "Failed to unlock memory region"
            );
        }

        [TestMethod]
        public void LockMemoryHandleTest()
        {
            // Only run on supported platforms
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("Test only runs on Windows or Linux");
                return;
            }

            // Allocate memory using a MemoryHandle
            using MemoryHandle<byte> handle = MemoryUtil.Shared.Alloc<byte>(TEST_BLOCK_SIZE, zero: true);

            // Try to lock the memory          
            Assert.IsTrue(MemoryUtil.LockMemory(handle), "Failed to lock memory region via handle");

            //Try reading and writing data to the memory
            handle.Span[0] = 0x01;
            byte readByte = handle.Span[0];
            Assert.AreEqual(0x01, readByte, "Failed to write/read data to locked memory");

            // Try to unlock the memory
            Assert.IsTrue(MemoryUtil.UnlockMemory(handle), "Failed to unlock memory region via handle");
        }

        [TestMethod]
        public void LockMemoryHandleRefTest()
        {
            // Only run on supported platforms
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("Test only runs on Windows or Linux");
                return;
            }

            // Pin an array to get a MemoryHandle
            byte[] buffer = new byte[TEST_BLOCK_SIZE];
            using MemoryHandle memHandle = MemoryUtil.PinArrayAndGetHandle(buffer, 0);

            // Try to lock the memory using the readonly reference           
            Assert.IsTrue(
                MemoryUtil.LockMemory(in memHandle, (nuint)buffer.Length), 
                message: "Failed to lock memory region via memory handle reference"
            );

            // Try to unlock the memory with reference         
            Assert.IsTrue(
                MemoryUtil.UnlockMemory(in memHandle, (nuint)buffer.Length), 
                message: "Failed to unlock memory region that was locked by reference"
            );
        }

        [TestMethod]
        public void UnlockMemoryHandleRefTest()
        {
            // Only run on supported platforms
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("Test only runs on Windows or Linux");
                return;
            }

            // Pin an array to get a MemoryHandle
            byte[] buffer = new byte[TEST_BLOCK_SIZE];
            using MemoryHandle memHandle = MemoryUtil.PinArrayAndGetHandle(buffer, 0);

            // Lock the memory first
            Assert.IsTrue(
                MemoryUtil.LockMemory(in memHandle, (nuint)buffer.Length), 
                message: "Failed to lock memory for unlock test"
            );

            // try writing and reading data to the memory
            buffer[0] = 0x01;
            byte readByte = buffer[0];
            Assert.AreEqual(0x01, readByte, "Failed to write/read data to locked memory");

            // Try to unlock the memory using the readonly reference        
            Assert.IsTrue(
                MemoryUtil.UnlockMemory(in memHandle, (nuint)buffer.Length), 
                message: "Unlock memory operation via handle reference failed"
            );
        }
    }
}
