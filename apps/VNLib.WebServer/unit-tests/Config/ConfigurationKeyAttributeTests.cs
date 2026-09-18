/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServerTests
* File: ConfigurationKeyAttributeTests.cs 
*
* ConfigurationKeyAttributeTests.cs is part of VNLib.WebServerTests which is part of the larger 
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.WebServer.Config;

namespace VNLib.WebServerTests.Config
{
    [TestClass]
    public class ConfigurationKeyAttributeTests
    {
        /// <summary>
        /// Verifies that constructing a <see cref="ConfigurationKeyAttribute"/> with a null
        /// configuration key throws <see cref="ArgumentException"/>.
        /// </summary>
        [TestMethod]
        public void Constructor_NullKey_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new ConfigurationKeyAttribute(null!));
        }

        /// <summary>
        /// Verifies that constructing a <see cref="ConfigurationKeyAttribute"/> with an empty
        /// string throws <see cref="ArgumentException"/>.
        /// </summary>
        [TestMethod]
        public void Constructor_EmptyKey_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new ConfigurationKeyAttribute(""));
        }

        /// <summary>
        /// Verifies that constructing a <see cref="ConfigurationKeyAttribute"/> with a whitespace-only
        /// string throws <see cref="ArgumentException"/>.
        /// </summary>
        [TestMethod]
        public void Constructor_WhitespaceKey_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new ConfigurationKeyAttribute("   "));
        }

        /// <summary>
        /// Verifies that constructing a <see cref="ConfigurationKeyAttribute"/> with a valid
        /// string stores the key in the <see cref="ConfigurationKeyAttribute.ConfigKey"/> property.
        /// </summary>
        [TestMethod]
        public void Constructor_ValidKey_StoresConfigKey()
        {
            ConfigurationKeyAttribute attr = new("test-key");

            Assert.AreEqual("test-key", attr.ConfigKey);
        }
    }
}
