/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServerTests
* File: ValidateTests.cs 
*
* ValidateTests.cs is part of VNLib.WebServerTests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServerTests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServerTests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServerTests. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.WebServer.Config;

namespace VNLib.WebServerTests.Config
{
    [TestClass]
    public class ValidateTests
    {
        /// <summary>
        /// Verifies that non-null objects pass validation without throwing an exception.
        /// </summary>
        [TestMethod]
        public void EnsureNotNull_ValidObject_Success()
        {
            object validObject = new();
            
            Validate.EnsureNotNull(validObject, "Object should not be null");
        }

        /// <summary>
        /// Verifies that non-empty, non-whitespace strings pass null validation.
        /// </summary>
        [TestMethod]
        public void EnsureNotNull_ValidString_Success()
        {
            string validString = "test value";
            
            Validate.EnsureNotNull(validString, "String should not be null");
        }

        /// <summary>
        /// Verifies that null strings are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureNotNull_NullString_ThrowsException()
        {
            string? nullString = null;

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureNotNull(nullString, "String is null")
            );
        }

        /// <summary>
        /// Verifies that empty strings are rejected as invalid.
        /// </summary>
        [TestMethod]
        public void EnsureNotNull_EmptyString_ThrowsException()
        {
            string emptyString = string.Empty;

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureNotNull(emptyString, "String is empty")
            );
        }

        /// <summary>
        /// Verifies that whitespace-only strings are rejected as invalid.
        /// </summary>
        [TestMethod]
        public void EnsureNotNull_WhitespaceString_ThrowsException()
        {
            string whitespaceString = "   ";

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureNotNull(whitespaceString, "String is whitespace")
            );
        }

        /// <summary>
        /// Verifies that true conditions pass validation without throwing an exception.
        /// </summary>
        [TestMethod]
        public void Assert_TrueCondition_Success()
        {
            Validate.Assert(true, "Condition should be true");
        }

        /// <summary>
        /// Verifies that false conditions are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void Assert_FalseCondition_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.Assert(false, "Condition is false")
            );
        }

        /// <summary>
        /// Verifies that valid IPv4 addresses are accepted without throwing an exception.
        /// </summary>
        [TestMethod]
        public void EnsureValidIp_ValidIpv4_Success()
        {
            Validate.EnsureValidIp("192.168.1.1", "IPv4 address should be valid");
        }

        /// <summary>
        /// Verifies that valid IPv6 addresses are accepted without throwing an exception.
        /// </summary>
        [TestMethod]
        public void EnsureValidIp_ValidIpv6_Success()
        {
            Validate.EnsureValidIp("2001:0db8:85a3:0000:0000:8a2e:0370:7334", "IPv6 address should be valid");
        }

        /// <summary>
        /// Verifies that localhost addresses (IPv4 and IPv6) are recognized as valid.
        /// </summary>
        [TestMethod]
        public void EnsureValidIp_Localhost_Success()
        {
            Validate.EnsureValidIp("127.0.0.1", "Localhost should be valid");
            Validate.EnsureValidIp("::1", "IPv6 localhost should be valid");
        }

        /// <summary>
        /// Verifies that invalid IPv4 addresses are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureValidIp_InvalidIp_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureValidIp("999.999.999.999", "Invalid IP address")
            );
        }

        /// <summary>
        /// Verifies that non-IP strings are rejected as invalid IP addresses.
        /// </summary>
        [TestMethod]
        public void EnsureValidIp_NonIpString_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureValidIp("not-an-ip-address", "Invalid IP format")
            );
        }

        /// <summary>
        /// Verifies that null IP strings are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureValidIp_NullString_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureValidIp(null, "IP is null")
            );
        }

        /// <summary>
        /// Verifies that different values pass the inequality validation.
        /// </summary>
        [TestMethod]
        public void EnsureNotEqual_DifferentValues_Success()
        {
            Validate.EnsureNotEqual(5, 10, "Values should not be equal");
            Validate.EnsureNotEqual("test1", "test2", "Strings should not be equal");
        }

        /// <summary>
        /// Verifies that equal numeric values are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureNotEqual_EqualValues_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureNotEqual(5, 5, "Values are equal")
            );
        }

        /// <summary>
        /// Verifies that equal strings are rejected as invalid.
        /// </summary>
        [TestMethod]
        public void EnsureNotEqual_EqualStrings_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureNotEqual("test", "test", "Strings are equal")
            );
        }

        /// <summary>
        /// Verifies that null values in equality comparisons are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureNotEqual_NullValues_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureNotEqual(null!, "test", "First value is null")
            );

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureNotEqual("test", null!, "Second value is null")
            );
        }

        /// <summary>
        /// Verifies that ulong values within the specified range pass validation.
        /// </summary>
        [TestMethod]
        public void EnsureRangeEx_ULong_ValidRange_Success()
        {
            Validate.EnsureRangeEx(5UL, 1UL, 10UL, "Value should be in range");
            Validate.EnsureRangeEx(1UL, 1UL, 10UL, "Min boundary should be valid");
            Validate.EnsureRangeEx(10UL, 1UL, 10UL, "Max boundary should be valid");
        }

        /// <summary>
        /// Verifies that ulong values below the minimum are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureRangeEx_ULong_BelowMin_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureRangeEx(0UL, 1UL, 10UL, "Value below minimum")
            );
        }

        /// <summary>
        /// Verifies that ulong values above the maximum are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureRangeEx_ULong_AboveMax_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureRangeEx(11UL, 1UL, 10UL, "Value above maximum")
            );
        }

        /// <summary>
        /// Verifies that long values within the specified range, including negative ranges, pass validation.
        /// </summary>
        [TestMethod]
        public void EnsureRangeEx_Long_ValidRange_Success()
        {
            Validate.EnsureRangeEx(5L, 1L, 10L, "Value should be in range");
            Validate.EnsureRangeEx(-5L, -10L, 0L, "Negative range should be valid");
        }

        /// <summary>
        /// Verifies that long values below the minimum are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureRangeEx_Long_BelowMin_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureRangeEx(0L, 1L, 10L, "Value below minimum")
            );
        }

        /// <summary>
        /// Verifies that long values above the maximum are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void EnsureRangeEx_Long_AboveMax_ThrowsException()
        {
            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureRangeEx(11L, 1L, 10L, "Value above maximum")
            );
        }

        /// <summary>
        /// Verifies that ulong values within range pass validation using caller expression API.
        /// </summary>
        [TestMethod]
        public void EnsureRange_ULong_WithCallerExpression_Success()
        {
            ulong testValue = 5UL;
            
            Validate.EnsureRange(testValue, 1UL, 10UL);
        }

        /// <summary>
        /// Verifies that ulong values outside range are rejected using caller expression API.
        /// </summary>
        [TestMethod]
        public void EnsureRange_ULong_WithCallerExpression_ThrowsException()
        {
            ulong testValue = 15UL;

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureRange(testValue, 1UL, 10UL)
            );
        }

        /// <summary>
        /// Verifies that long values within range pass validation using caller expression API.
        /// </summary>
        [TestMethod]
        public void EnsureRange_Long_WithCallerExpression_Success()
        {
            long testValue = 5L;
            
            Validate.EnsureRange(testValue, 1L, 10L);
        }

        /// <summary>
        /// Verifies that long values outside range are rejected using caller expression API.
        /// </summary>
        [TestMethod]
        public void EnsureRange_Long_WithCallerExpression_ThrowsException()
        {
            long testValue = 15L;

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.EnsureRange(testValue, 1L, 10L)
            );
        }

        /// <summary>
        /// Verifies that existing files pass file existence validation without throwing an exception.
        /// </summary>
        [TestMethod]
        public void FileExists_ExistingFile_Success()
        {
            // Create a temporary file
            string tempFile = Path.GetTempFileName();

            try
            {
                Validate.FileExists(tempFile);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        /// <summary>
        /// Verifies that non-existent files are rejected and throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void FileExists_NonExistentFile_ThrowsException()
        {
            string nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                Validate.FileExists(nonExistentFile)
            );
        }
    }
}
