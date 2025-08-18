/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: FileOperationsTests.cs 
*
* FileOperationsTests.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.IO;

namespace VNLib.Utils.IO.Tests
{
    [TestClass()]
    public class FileOperationsTests
    {
        // Get a random file path for testing
        private string _tempFilePath = Path.GetTempFileName();

        [TestInitialize()]
        public void Setup()
        {
            // Write some test data to the test file
            File.WriteAllText(_tempFilePath, "test");
        }

        [TestCleanup()]
        public void Cleanup()
        {
            // Remove the test file if it exists
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [TestMethod()]
        public void FileExistsExistingFileTest()
        {
            Assert.IsTrue(
                FileOperations.FileExists(_tempFilePath), 
                message: "Existing file should be detected by FileExists"
            );
        }

        [TestMethod()]
        public void FileExistsNonExistingFileTest()
        {
            string nonExistent = Path.GetRandomFileName();

            Assert.IsFalse(
                FileOperations.FileExists(nonExistent), 
                message: "Non-existent file should return false"
            );
        }

        [TestMethod()]
        public void FileExistsThrowsOnEmptyPathTest()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => FileOperations.FileExists(string.Empty), 
                message: "Empty path should throw ArgumentException"
            );
        }

        [TestMethod()]
        public void GetAttributesReturnsAttributesForExistingFileTest()
        {
            FileAttributes attrs = FileOperations.GetAttributes(_tempFilePath);
            FileAttributes expected = File.GetAttributes(_tempFilePath);

            Assert.IsTrue(
                (attrs & FileAttributes.Normal) == FileAttributes.Normal || (attrs & FileAttributes.Archive) == FileAttributes.Archive,
                message: "Expected Normal or Archive attribute on test file"
            );

            // Test against native File.GetAttributes result
            Assert.AreEqual(expected, attrs, message: "File attributes should match those returned by File.GetAttributes");
        }

        [TestMethod()]
        public void GetAttributesThrowsForNonExistingFileTest()
        {
            string nonExistent = Path.GetRandomFileName();

            Assert.ThrowsExactly<FileNotFoundException>(
                () => FileOperations.GetAttributes(nonExistent), 
                message: "Getting attributes for non-existent file should throw FileNotFoundException"
            );
        }

        [TestMethod()]
        public void FileExistsWithReadWriteTest()
        {
            if (OperatingSystem.IsLinux())
            {
                Assert.IsTrue(FileOperations.CanAccess(_tempFilePath, FileAccess.Read), message: "Read access should be granted");
                Assert.IsTrue(FileOperations.CanAccess(_tempFilePath, FileAccess.Write), message: "Write access should be granted");
                Assert.IsTrue(FileOperations.CanAccess(_tempFilePath, FileAccess.Read | FileAccess.Write), message: "Read|Write access should be granted");
            }
            else
            {
                Assert.ThrowsExactly<NotSupportedException>(
                    () => FileOperations.CanAccess(_tempFilePath, FileAccess.Read), 
                    message: "Non-Linux platforms should throw NotSupportedException for access checks"
                );
            }
        }

        [TestMethod()]
        public void FileExistsWithNonExistingFileTest()
        {
            if (OperatingSystem.IsLinux())
            {
                string nonExistent = Path.GetRandomFileName();

                Assert.IsFalse(
                    FileOperations.CanAccess(nonExistent, FileAccess.Read), 
                    message: "Access check on non-existent file should be false"
                );
            }
            else
            {
                Assert.ThrowsExactly<NotSupportedException>(
                    () => FileOperations.CanAccess(_tempFilePath, FileAccess.Read),
                    message: "Non-Linux platforms should throw NotSupportedException for access checks"
                );
            }
        }

        /*
         * Tests known Linux system files with standard permissions to verify 
         * that CanAccess correctly interprets permission bits.
         */

        [TestMethod()]
        public void CanAccessProtectedFilesTest()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("Test only runs on Linux");
                return;
            }

            // Try setting readonly access to a file
            string testFile = Path.GetTempFileName();

            try
            {
                File.WriteAllText(testFile, "test");
                FileAttributes attrs = File.GetAttributes(testFile);

                // Check that before setting readonly, we had read/write access
                Assert.IsTrue(
                    (attrs & FileAttributes.Normal) > 0 || (attrs & FileAttributes.Archive) > 0,
                    message: "Test access file should exist and have Normal or Archive attribute"
                );

                File.SetAttributes(testFile, attrs | FileAttributes.ReadOnly);

                // Read should be allowed since the file is readonly
                Assert.IsTrue(
                    FileOperations.CanAccess(testFile, FileAccess.Read),
                    message: "CanAccess should report read access for test file"
                );

                // Write is not allowed since the file has been made readonly
                Assert.IsFalse(
                    FileOperations.CanAccess(testFile, FileAccess.Write),
                    message: "CanAccess should report no write access for test file for unprivileged user"
                );

                // Write + read is not allowed since the file has been made readonly
                Assert.IsFalse(
                    FileOperations.CanAccess(testFile, FileAccess.ReadWrite),
                    message: "CanAccess should report no r+w access for test file for unprivileged user"
                );
            }
            finally
            {
                if (File.Exists(testFile))
                {
                    File.Delete(testFile);
                }
            }
        }

        /*
         * Tests that calls to access() are based on permission bits and not affected by 
         * open file handles and their sharing modes.
         */

        [TestMethod()]
        public void CanAccessOpenFileTest()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("Test only runs on Linux");
                return;
            }

            string tempPath = Path.GetTempFileName();

            try
            {
                // Ensure the file exists and current process can read/write
                File.WriteAllText(tempPath, "test");

                Assert.IsTrue(
                    FileOperations.CanAccess(tempPath, FileAccess.ReadWrite),
                    message: "Temp file should be readable and writable by creator"
                );

                // Open the file with restrictive FileShare (FileShare.None) and limited access.
                // On Linux this should not change what access() reports (permission bits remain same).
                using FileStream fs = File.Open(tempPath, FileMode.Open, FileAccess.Read, FileShare.None);

                // While open with read-only access, CanAccess should still report write access based on permission bits
                Assert.IsTrue(
                    FileOperations.CanAccess(tempPath, FileAccess.Write),
                    message: "CanAccess should be based on permission bits and not affected by open handle share on Linux"
                );
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
