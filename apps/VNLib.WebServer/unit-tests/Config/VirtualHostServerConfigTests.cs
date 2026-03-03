/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServerTests
* File: VirtualHostServerConfigTests.cs 
*
* VirtualHostServerConfigTests.cs is part of VNLib.WebServerTests which is part of the larger 
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

using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.WebServer.Config;
using VNLib.WebServer.Config.Model;

namespace VNLib.WebServerTests.Config
{
    [TestClass]
    public class VirtualHostServerConfigTests
    {
        #region Helper Methods

        private static VirtualHostServerConfig CreateValidConfig()
        {
            return new VirtualHostServerConfig
            {
                Enabled             = true,
                DirPath             = "/var/www/html",
                Hostnames           = ["example.com", "www.example.com"],
                Interfaces          = 
                [
                    new TransportInterface
                    {
                        Address = "127.0.0.1",
                        Port    = 8080
                    }
                ]
            };
        }

        #endregion

        #region Valid Configuration Tests

        /// <summary>
        /// Verifies that a valid configuration with all required fields passes validation successfully.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ValidConfiguration_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a valid configuration with multiple interfaces passes validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_MultipleInterfaces_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "127.0.0.1", Port = 8080 },
                new TransportInterface { Address = "192.168.1.100", Port = 8443 }
            ];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a valid configuration with IPv6 addresses passes validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_Ipv6Address_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "::1", Port = 8080 }
            ];

            config.OnDeserialized();
        }

        #endregion

        #region Disabled Host Tests

        /// <summary>
        /// Verifies that disabled virtual hosts skip all validation checks.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DisabledHost_SkipsValidation()
        {
            VirtualHostServerConfig config = new()
            {
                Enabled = false
            };

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that disabled hosts with invalid configuration do not throw exceptions.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DisabledHostWithInvalidConfig_SkipsValidation()
        {
            VirtualHostServerConfig config = new()
            {
                Enabled     = false,
                DirPath     = null,
                Hostnames   = null,
                Interfaces  = []
            };

            config.OnDeserialized();
        }

        #endregion

        #region Required Fields Tests

        /// <summary>
        /// Verifies that missing DirPath throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_MissingDirPath_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DirPath = null;

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that empty DirPath throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyDirPath_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DirPath = string.Empty;

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that whitespace-only DirPath throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_WhitespaceDirPath_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DirPath = "   ";

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that null Hostnames array throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullHostnames_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Hostnames = null;

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that null Interfaces array throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullInterfaces_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = null!;

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        #endregion

        #region Hostname Validation Tests

        /// <summary>
        /// Verifies that a null entry in the hostnames array throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullHostnameEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Hostnames = ["example.com", null!, "www.example.com"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty string hostname entry throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyHostnameEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Hostnames = ["example.com", string.Empty];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a whitespace-only hostname entry throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_WhitespaceHostnameEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Hostnames = ["example.com", "   "];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty hostnames array throws ServerConfigurationException.
        /// At least one hostname is required for a virtual host to route any requests.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyHostnamesArray_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Hostnames = [];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        #endregion

        #region Interface Validation Tests

        /// <summary>
        /// Verifies that an empty interfaces array throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyInterfacesArray_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = [];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a null interface entry in the array throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullInterfaceEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = [null!];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a null interface address throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullInterfaceAddress_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = null!, Port = 8080 }
            ];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty interface address throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyInterfaceAddress_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = string.Empty, Port = 8080 }
            ];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an invalid IP address format throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_InvalidInterfaceIpAddress_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "invalid-ip-address", Port = 8080 }
            ];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an out-of-range IP address throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_OutOfRangeIpAddress_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "999.999.999.999", Port = 8080 }
            ];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a port value of zero throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ZeroPort_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "127.0.0.1", Port = 0 }
            ];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a negative port value throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NegativePort_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "127.0.0.1", Port = -1 }
            ];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a port value exceeding 65535 throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_PortAboveMaximum_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "127.0.0.1", Port = 65536 }
            ];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that port value 1 (minimum valid port) passes validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_MinimumBoundaryPort_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "127.0.0.1", Port = 1 }
            ];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that port value 65535 (maximum valid port) passes validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_MaximumBoundaryPort_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Interfaces = 
            [
                new TransportInterface { Address = "127.0.0.1", Port = 65535 }
            ];

            config.OnDeserialized();
        }

        #endregion

        #region IP Whitelist Validation Tests

        /// <summary>
        /// Verifies that a valid whitelist with multiple IP addresses passes validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ValidWhitelist_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Whitelist = ["192.168.1.1", "192.168.1.2", "10.0.0.1"];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a null entry in the whitelist throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullWhitelistEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Whitelist = ["192.168.1.1", null!, "10.0.0.1"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty string in the whitelist throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyWhitelistEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Whitelist = ["192.168.1.1", string.Empty];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an invalid IP address in the whitelist throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_InvalidWhitelistIpAddress_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Whitelist = ["192.168.1.1", "not-an-ip"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty whitelist array does not trigger validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyWhitelist_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Whitelist = [];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a null whitelist array is handled correctly.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullWhitelist_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Whitelist = null;

            config.OnDeserialized();
        }

        #endregion

        #region IP Blacklist Validation Tests

        /// <summary>
        /// Verifies that a valid blacklist with multiple IP addresses passes validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ValidBlacklist_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Blacklist = ["192.168.1.100", "192.168.1.101", "10.0.0.50"];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a null entry in the blacklist throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullBlacklistEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Blacklist = ["192.168.1.100", null!, "10.0.0.50"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty string in the blacklist throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyBlacklistEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Blacklist = ["192.168.1.100", string.Empty];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an invalid IP address in the blacklist throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_InvalidBlacklistIpAddress_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Blacklist = ["192.168.1.100", "invalid-ip"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty blacklist array does not trigger validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyBlacklist_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Blacklist = [];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a null blacklist array is handled correctly.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullBlacklist_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Blacklist = null;

            config.OnDeserialized();
        }

        #endregion

        #region Downstream Servers Validation Tests

        /// <summary>
        /// Verifies that valid downstream server addresses pass validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ValidDownstreamServers_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DownstreamServers = ["192.168.1.1", "10.0.0.1"];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a null entry in downstream servers throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullDownstreamServerEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DownstreamServers = ["192.168.1.1", null!];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty string in downstream servers throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyDownstreamServerEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DownstreamServers = ["192.168.1.1", string.Empty];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an invalid IP address in downstream servers throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_InvalidDownstreamServerIpAddress_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DownstreamServers = ["192.168.1.1", "not-an-ip-address"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty downstream servers array does not trigger validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyDownstreamServers_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DownstreamServers = [];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a null downstream servers array is handled correctly.
        /// The property defaults to an empty array, but null can be assigned directly.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullDownstreamServers_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DownstreamServers = null!;

            config.OnDeserialized();
        }

        #endregion

        #region Default Files Validation Tests

        /// <summary>
        /// Verifies that valid default file names pass validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ValidDefaultFiles_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["index.html", "index.htm", "default.html"];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a null entry in default files throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullDefaultFileEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["index.html", null!];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty string in default files throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyDefaultFileEntry_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["index.html", string.Empty];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that default file names containing forward slashes throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DefaultFileWithForwardSlash_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["index.html", "subdir/index.html"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty default files array does not trigger validation errors.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyDefaultFiles_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = [];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a null default files array is handled correctly.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullDefaultFiles_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = null;

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a default file name containing a backslash throws ServerConfigurationException.
        /// Backslashes are not valid in plain file names and indicate a path separator on Windows.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DefaultFileWithBackslash_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["sub\\index.html"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a default file name with no extension throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DefaultFileWithoutExtension_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["index"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a default file name with a single-character extension throws ServerConfigurationException.
        /// Extensions must be at least two characters (e.g., "js", "cs").
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DefaultFileWithSingleCharExtension_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["index.h"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that a directory traversal sequence in a default file name throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DefaultFileWithPathTraversal_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["../evil.html"];

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that valid hyphenated and underscored file names pass validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_DefaultFileHyphenatedAndUnderscore_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.DefaultFiles = ["my-page.html", "my_file.js", "index.min.js"];

            config.OnDeserialized();
        }

        #endregion

        #region Headers Dictionary Validation Tests

        /// <summary>
        /// Verifies that valid custom headers pass validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ValidHeaders_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", "value1" },
                { "X-Another-Header", "value2" }
            };

            config.OnDeserialized();
        }     

        /// <summary>
        /// Verifies that a null header value throws ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_NullHeaderValue_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", null! }
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that empty header keys throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyHeaderKey_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Headers = new Dictionary<string, string>
            {
                { string.Empty, "value" }
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that empty header values throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyHeaderValue_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", string.Empty }
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that an empty headers dictionary is valid.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyHeaders_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.Headers = [];

            config.OnDeserialized();
        }

        #endregion

        #region Cache Max-Age Validation Tests

        /// <summary>
        /// Verifies that valid file extensions with dot prefix and positive max-age values pass validation.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_ValidCacheMaxAge_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.FileHttpCacheMaxAge = new Dictionary<string, int>
            {
                { ".js", 3600 },
                { ".css", 7200 },
                { ".png", 86400 }
            };

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that file extensions without dot prefix throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_CacheMaxAgeExtensionWithoutDot_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.FileHttpCacheMaxAge = new Dictionary<string, int>
            {
                { "js", 3600 }
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }       

        /// <summary>
        /// Verifies that negative max-age values throw ServerConfigurationException.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_CacheMaxAgeNegativeValue_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.FileHttpCacheMaxAge = new Dictionary<string, int>
            {
                { ".js", -1 }
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        /// <summary>
        /// Verifies that zero max-age value is valid.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_CacheMaxAgeZeroValue_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.FileHttpCacheMaxAge = new Dictionary<string, int>
            {
                { ".html", 0 }
            };

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that an empty cache max-age dictionary is valid.
        /// </summary>
        [TestMethod]
        public void OnDeserialized_EmptyCacheMaxAge_Success()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.FileHttpCacheMaxAge = [];

            config.OnDeserialized();
        }

        /// <summary>
        /// Verifies that a bare dot key "." (no extension characters after the dot)
        /// throws ServerConfigurationException. A valid extension must have at least
        /// one character after the dot (e.g., ".js" not ".").
        /// </summary>
        [TestMethod]
        public void OnDeserialized_CacheMaxAgeBareDotKeyOnly_ThrowsException()
        {
            VirtualHostServerConfig config = CreateValidConfig();
            config.FileHttpCacheMaxAge = new Dictionary<string, int>
            {
                { ".", 3600 }
            };

            Assert.ThrowsExactly<ServerConfigurationException>(() =>
                config.OnDeserialized()
            );
        }

        #endregion
    }
}
