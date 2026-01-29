/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.WebServerTests
* File: PluginConfigTests.cs
*
* PluginConfigTests.cs is part of VNLib.WebServerTests which is part of the larger
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
using VNLib.WebServer.Config.Model;

namespace VNLib.WebServerTests.Config
{
    [TestClass]
    public class PluginConfigTests
    {
        /// <summary>
        /// Verifies that a valid plugin configuration with all required properties passes validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ValidConfig_Success()
        {
            ServerPluginConfig config = new()
            {
                Enabled        = true,
                Path           = "./plugins",
                ReloadDelaySec = 2
            };

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that disabled plugin configurations skip validation and allow null paths.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DisabledConfig_SkipsValidation()
        {
            ServerPluginConfig config = new()
            {
                Enabled   = false,
                Path      = null!,
                ConfigDir = null!
            };

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that enabled configurations without a path throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EnabledWithoutPath_ThrowsException()
        {
            ServerPluginConfig config = new()
            {
                Enabled = true,
                Path    = null!
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that empty path strings are rejected for enabled configurations.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyPath_ThrowsException()
        {
            ServerPluginConfig config = new()
            {
                Enabled = true,
                Path    = string.Empty
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that ReloadDelaySec values below the valid range are rejected.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NegativeReloadDelay_ThrowsException()
        {
            ServerPluginConfig config = new()
            {
                Enabled        = true,
                Path           = "./plugins",
                ReloadDelaySec = -1
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that ReloadDelaySec values above the valid range (600 seconds) are rejected.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ExcessiveReloadDelay_ThrowsException()
        {
            ServerPluginConfig config = new()
            {
                Enabled        = true,
                Path           = "./plugins",
                ReloadDelaySec = 601
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that valid boundary values for ReloadDelaySec (0 and 600) are accepted.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_BoundaryReloadDelayValues_Success()
        {
            ServerPluginConfig configMin = new()
            {
                Enabled        = true,
                Path           = "./plugins",
                ReloadDelaySec = 0
            };

            ServerPluginConfig configMax = new()
            {
                Enabled        = true,
                Path           = "./plugins",
                ReloadDelaySec = 600
            };

            configMin.OnDeserialized();
            configMax.OnDeserialized();
        }

        /// <summary>
        /// Verifies that non-existent ConfigDir paths are rejected when specified.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NonExistentConfigDir_ThrowsException()
        {
            ServerPluginConfig config = new()
            {
                Enabled   = true,
                Path      = "./plugins",
                ConfigDir = $"./does-not-exist-{Guid.NewGuid()}"
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that existing ConfigDir paths pass validation successfully.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ExistingConfigDir_Success()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                ServerPluginConfig config = new()
                {
                    Enabled   = true,
                    Path      = "./plugins",
                    ConfigDir = tempDir
                };

                config.OnDeserialized();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir);
                }
            }
        }

        /// <summary>
        /// Verifies that null ConfigDir values are allowed and skip directory validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullConfigDir_Success()
        {
            ServerPluginConfig config = new()
            {
                Enabled   = true,
                Path      = "./plugins",
                ConfigDir = null!
            };

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that HotReload property does not affect validation logic.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_HotReloadEnabled_Success()
        {
            ServerPluginConfig config = new()
            {
                Enabled        = true,
                Path           = "./plugins",
                HotReload      = true,
                ReloadDelaySec = 5
            };

            config.OnDeserialized();
        }
    }
}
