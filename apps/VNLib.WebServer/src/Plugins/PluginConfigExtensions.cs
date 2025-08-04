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
using System.Text;
using System.Text.Json;

using VNLib.Plugins.Runtime;
using VNLib.Utils.IO;
using VNLib.WebServer.Config;

namespace VNLib.WebServer.Plugins
{
    internal static class PluginConfigExtensions
    {           
        private static readonly byte[] EmptyConfig = Encoding.UTF8.GetBytes("{}");

        /// <summary>
        /// Defines how the plugin configuration should be applied to the plugin stack. This function
        /// allows for using the <see cref="JsonServerConfig"/> loading mechanism to support the same
        /// loading mechanism as the webserver itself.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="hostConfig">A nullable host element to pass to the configuration loader</param>
        public static void WithPluginConfig(this PluginStackBuilder builder, in JsonElement? hostConfig)
        {
            _ = builder.WithJsonConfig(in hostConfig, static (asmConfig, output) =>
            {
                // Probe the plugin assembly directory for configuration files
                foreach (string ext in JsonServerConfig.SupportedFileExtensions)
                {
                    // To see if a config file with .yaml or .json exists
                    string fileName = Path.ChangeExtension(asmConfig.AssemblyFile, ext);

                    if (ReadConfigFileIfExists(fileName, output))
                    {
                        return;
                    }
                }

                output.Write(EmptyConfig);
            });           
        }

        /// <summary>
        /// Defines how the plugin configuration should be applied to the plugin stack. This function
        /// allows for using the <see cref="JsonServerConfig"/> loading mechanism to support the same
        /// loading mechanism as the webserver itself. This overload allows for specifying a custom
        /// configuration directory to search for configuration files.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="hostConfig">A nullable host element to pass to the configuration loader</param>
        /// <param name="configDir">The directory to search for configuration files</param>
        public static void WithPluginConfig(this PluginStackBuilder builder, in JsonElement? hostConfig, string configDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configDir);

            _ = builder.WithJsonConfig(in hostConfig, (asmConfig, output) =>
            {
                // Probe the config directory for configuration files
                foreach (string ext in JsonServerConfig.SupportedFileExtensions)
                {                   
                    string fileName = Path.ChangeExtension(asmConfig.AssemblyFile, ext);

                    //Change file path to the config directory
                    fileName = Path.Combine(configDir, Path.GetFileName(fileName));

                    if (ReadConfigFileIfExists(fileName, output))
                    {
                        return;
                    }
                }

                output.Write(EmptyConfig);
            });
        }

        private static bool ReadConfigFileIfExists(string filePath, Stream output)
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

            // Write the json document to the output stream
            using Utf8JsonWriter writer = new(output);

            jdo.RootElement.WriteTo(writer);

            return true;
        }
    }
}
