/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServerTests
* File: JsonServerConfigTests.cs 
*
* JsonServerConfigTests.cs is part of VNLib.WebServerTests which is part of the larger 
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
    public class JsonServerConfigTests
    {
        private static string GetTestDataPath(string filename)
        {
            string baseDir = AppContext.BaseDirectory;
            return Path.Combine(baseDir, "ConfigTestData", filename);
        }

        #region File Loading Tests

        /// <summary>
        /// Verifies that valid JSON configuration files are loaded and parsed successfully.
        /// </summary>
        [TestMethod]
        public void FromFile_ValidJsonFile_LoadsSuccessfully()
        {
            string configPath = GetTestDataPath("valid-config.json");
            
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);
        }

        /// <summary>
        /// Verifies that valid YAML configuration files are loaded and parsed successfully.
        /// </summary>
        [TestMethod]
        public void FromFile_ValidYamlFile_LoadsSuccessfully()
        {
            string configPath = GetTestDataPath("valid-config.yaml");
            
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);
        }

        /// <summary>
        /// Verifies that .yml file extension is recognized and loaded as YAML.
        /// </summary>
        [TestMethod]
        public void FromFile_ValidYmlExtension_LoadsSuccessfully()
        {
            string tempFile = GetTestDataPath("temp-config.yml");
            File.Copy(GetTestDataPath("valid-config.yaml"), tempFile, overwrite: true);

            try
            {
                JsonServerConfig? config = JsonServerConfig.FromFile(tempFile);
                Assert.IsNotNull(config);
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
        /// Verifies that loading a non-existent file returns null without throwing an exception.
        /// </summary>
        [TestMethod]
        public void FromFile_NonExistentFile_ReturnsNull()
        {
            string configPath = GetTestDataPath("does-not-exist.json");
            
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNull(config);
        }

        /// <summary>
        /// Verifies that malformed JSON is gracefully handled and returns null.
        /// </summary>
        [TestMethod]
        public void FromFile_MalformedJson_ReturnsNull()
        {
            string tempFile = GetTestDataPath("temp-malformed.json");
            File.WriteAllText(tempFile, "{ \"key\": invalid json }");

            try
            {
                JsonServerConfig? config = JsonServerConfig.FromFile(tempFile);
                Assert.IsNull(config);
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
        /// Verifies that unsupported file extensions are rejected and return null.
        /// </summary>
        [TestMethod]
        public void FromFile_UnknownExtension_ReturnsNull()
        {
            string configPath = GetTestDataPath("unknown-file.txt");
            File.WriteAllText(configPath, "{ \"test\": \"value\" }");

            try
            {
                JsonServerConfig? config = JsonServerConfig.FromFile(configPath);
                Assert.IsNull(config);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        #endregion

        #region Property Access Tests

        /// <summary>
        /// Verifies that simple top-level string properties are correctly retrieved.
        /// </summary>
        [TestMethod]
        public void GetConfigProperty_SimpleStringProperty_ReturnsValue()
        {
            string configPath = GetTestDataPath("nested-properties.json");
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);

            string? value = config.GetConfigProperty<string>("simple");

            Assert.AreEqual("value", value);
        }

        /// <summary>
        /// Verifies that nested properties can be accessed using the :: path separator.
        /// </summary>
        [TestMethod]
        public void GetConfigProperty_NestedPropertyWithSeparator_ReturnsValue()
        {
            string configPath = GetTestDataPath("nested-properties.json");
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);

            string? value = config.GetConfigProperty<string>("level1::level2::level3::value");

            Assert.AreEqual("deeply-nested", value);
        }

        /// <summary>
        /// Verifies that integer properties are correctly deserialized and converted.
        /// </summary>
        [TestMethod]
        public void GetConfigProperty_IntegerProperty_DeserializesCorrectly()
        {
            string configPath = GetTestDataPath("nested-properties.json");
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);

            int value = config.GetConfigProperty<int>("number");

            Assert.AreEqual(42, value);
        }

        /// <summary>
        /// Verifies that boolean properties are correctly deserialized and converted.
        /// </summary>
        [TestMethod]
        public void GetConfigProperty_BooleanProperty_DeserializesCorrectly()
        {
            string configPath = GetTestDataPath("nested-properties.json");
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);

            bool value = config.GetConfigProperty<bool>("boolean");

            Assert.IsTrue(value);
        }

        /// <summary>
        /// Verifies that requesting a non-existent property returns the default value for the type.
        /// </summary>
        [TestMethod]
        public void GetConfigProperty_NonExistentProperty_ReturnsDefault()
        {
            string configPath = GetTestDataPath("nested-properties.json");
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);

            string? value = config.GetConfigProperty<string>("does_not_exist");

            Assert.IsNull(value);
        }

        /// <summary>
        /// Verifies that array properties are correctly deserialized with proper element access.
        /// </summary>
        [TestMethod]
        public void GetConfigProperty_ArrayProperty_DeserializesCorrectly()
        {
            string configPath = GetTestDataPath("nested-properties.json");
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);

            int[]? array = config.GetConfigProperty<int[]>("array");

            Assert.IsNotNull(array);
            Assert.AreEqual(3, array.Length);
            Assert.AreEqual(1, array[0]);
            Assert.AreEqual(2, array[1]);
            Assert.AreEqual(3, array[2]);
        }

        #endregion

        #region YAML-Specific Tests

        /// <summary>
        /// Verifies that YAML integer values are correctly parsed as numeric types.
        /// </summary>
        [TestMethod]
        public void YamlConfig_IntegerValues_ParsedAsIntegers()
        {
            string configPath = GetTestDataPath("valid-config.yaml");
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);

            int timeout = config.GetConfigProperty<int>("http::timeout_ms");
            int maxConn = config.GetConfigProperty<int>("http::max_connections");

            Assert.AreEqual(30000, timeout);
            Assert.AreEqual(1000, maxConn);
        }

        /// <summary>
        /// Verifies that YAML boolean values are correctly parsed as boolean types.
        /// </summary>
        [TestMethod]
        public void YamlConfig_BooleanValues_ParsedAsBooleans()
        {
            string configPath = GetTestDataPath("valid-config.yaml");
            JsonServerConfig? config = JsonServerConfig.FromFile(configPath);

            Assert.IsNotNull(config);          

            Assert.IsTrue(config.GetConfigProperty<bool>("plugins::enabled"));
        }

        #endregion
    }
}
