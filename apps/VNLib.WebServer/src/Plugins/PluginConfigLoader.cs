/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: PluginConfigLoader.cs 
*
* PluginConfigLoader.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Text.Json;

using VNLib.Utils.IO;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Runtime.Batteries;

using VNLib.WebServer.Config;

namespace VNLib.WebServer.Plugins
{
    internal static class PluginConfigLoader
    {
        /*
         * Must match the host element name in the server configuration. For PluginBase
         * this is "host" by default.
         */
        internal const string HostConfigElementName = "host";

        /*
         * Must match the plugin element name in the server configuration. For PluginBase
         * this is "plugin" by default.
         */
        internal const string PluginConfigElementName = "plugin";

        /// <summary>
        /// Creates and returns a new <see cref="IPluginConfigReader"/> that reads plugin 
        /// configuration data from the server configuration and optional config directory.
        /// </summary>
        /// <param name="hostConfig">A host element to pass to the configuration reader</param>
        /// <param name="configDir">An optional config directory to search for configuration files</param>
        public static IPluginConfigReader CreateConfigReader(JsonElement hostConfig, string? configDir) 
            => new JsonConfigReader(hostConfig, configDir);

        /*
         * Right now, for compatibility, all vnlib plugins using PluginBase are expecting a json object passed
         * in utf8 binary. This class is designed to read configuration files from disk, merge them with
         * the host configuration, and output the resulting json to a stream. Host configuration or plugin
         * configuration can be any type supported by JsonServerConfig class which can read files into json.
         */

        private sealed class JsonConfigReader(JsonElement hostConfig, string? configDir) : IPluginConfigReader
        {
            private readonly string? _configDir = configDir;

            /// <inheritdoc/>
            public void ReadPluginConfigData(IPluginAssemblyLoadConfig config, Stream outputStream)
            {
                ArgumentNullException.ThrowIfNull(config);
                ArgumentNullException.ThrowIfNull(outputStream);

                // Use assembly directory if no config directory specified
                string? configSearchDir = string.IsNullOrWhiteSpace(_configDir) 
                    ? Path.GetDirectoryName(config.AssemblyFile)
                    : _configDir;

                if (!string.IsNullOrWhiteSpace(configSearchDir))
                {
                    // Probe the config directory for configuration files
                    foreach (string ext in JsonServerConfig.SupportedFileExtensions)
                    {
                        /*
                         * Searches for a file name that the assembly file name with a supported
                         * file extension for configuration files. 
                         * 
                         * - MyPlugin.dll -> MyPlugin.json, MyPlugin.yaml, etc.
                         */
                        string fileName = Path.Combine(
                            configSearchDir, 
                            Path.GetFileName(
                                Path.ChangeExtension(config.AssemblyFile, ext)
                            )
                        );

                        if (ReadConfigFileIfExists(fileName, outputStream))
                        {
                            return;
                        }
                    }
                }

                using JsonDocument emptyConfig = JsonDocument.Parse("{}");

                // No configuration file found, output empty config merged with host config
                MergeConfigs(hostConfig, emptyConfig.RootElement, outputStream);
            }

            private bool ReadConfigFileIfExists(string filePath, Stream output)
            {
                if (!FileOperations.FileExists(filePath))
                {
                    return false;
                }

                // Open the file for reading
                using FileStream pluginConfFileData = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                // Read the file into a json document
                using JsonDocument? jdo = JsonServerConfig.ReadConfigFileToJson(pluginConfFileData);

                if (jdo is null)
                {
                    return false;
                }

                MergeConfigs(hostConfig, jdo.RootElement, output);

                return true;
            }

            private static void MergeConfigs(JsonElement hostConfig, JsonElement pluginConfig, Stream output)
            {
                using JsonDocument mergedConfig = hostConfig.Merge(
                    other: in pluginConfig,
                    initalName: HostConfigElementName,
                    secondName: PluginConfigElementName
                );

                // Write the json document to the output stream
                using Utf8JsonWriter writer = new(output);

                mergedConfig.RootElement.WriteTo(writer);
            }
        }
    }
}
