/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: PluginConfigExtensions.cs 
*
* PluginConfigExtensions.cs is part of VNLib.WebServer which is part of the larger 
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

using VNLib.Plugins.Runtime;
using VNLib.Utils.Extensions;
using VNLib.Utils.IO;

using VNLib.WebServer.Config;

namespace VNLib.WebServer.Plugins
{
    internal static class PluginConfigExtensions
    {
        /*
         * Must match the host element name in the server configuration. For PluginBase
         * this is "host" by default.
         */
        public const string HostConfigElementName = "host";

        /*
         * Must match the plugin element name in the server configuration. For PluginBase
         * this is "plugin" by default.
         */
        public const string PluginConfigElementName = "plugin";

        /// <summary>
        /// Defines how the plugin configuration should be applied to the plugin stack. This function
        /// allows for using the <see cref="JsonServerConfig"/> loading mechanism to support the same
        /// loading mechanism as the webserver itself. This overload allows for specifying a custom
        /// configuration directory to search for configuration files.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="hostConfig">A nullable host element to pass to the configuration loader</param>
        /// <param name="configDir">The directory to search for configuration files</param>
        public static PluginStackBuilder WithPluginConfig(
            this PluginStackBuilder builder,
            JsonElement hostConfig,
            string? configDir
        )
            => builder.WithConfigurationReader(new JsonConfigReader(hostConfig, Path.GetDirectoryName(configDir)));

        /*
         * Right now, for compatability, all vnlib plugins using PluginBase are expecting a json object passed
         * in utf8 binary. This class is designed to read configuration files from disk, merge them with
         * the host configuration, and output the resulting json to a stream. Host configuration or plugin
         * configuration can be any type supported by JsonServerConfig class which can read files into json.
         */

        private sealed class JsonConfigReader(JsonElement hostConfig, string? configDir) : IPluginConfigReader
        {
            private static readonly JsonDocument EmptyConfig = JsonDocument.Parse("{}");

            private readonly string? _configDir = configDir;

            public void ReadPluginConfigData(IPluginAssemblyLoadConfig asmConfig, Stream outputStream)
            {
                // Use assembly directory if no config directory specified
                string configSearchDir = _configDir ?? Path.GetDirectoryName(asmConfig.AssemblyFile)!;

                // Probe the config directory for configuration files
                foreach (string ext in JsonServerConfig.SupportedFileExtensions)
                {
                    string fileName = Path.ChangeExtension(asmConfig.AssemblyFile, ext);

                    //Change file path to the config directory
                    fileName = Path.Combine(configSearchDir, Path.GetFileName(fileName));

                    if (ReadConfigFileIfExists(fileName, outputStream))
                    {
                        return;
                    }
                }

                // No configuration file found, output empty config merged with host config
                MergeConfigs(hostConfig, EmptyConfig.RootElement, outputStream);
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
