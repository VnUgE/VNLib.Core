/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.PluginBase
* File: FileLogConfigJson.cs 
*
* FileLogConfigJson.cs is part of VNLib.Plugins.PluginBase which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.PluginBase is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.PluginBase is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.PluginBase. If not, see http://www.gnu.org/licenses/.
*/

using System.Text.Json.Serialization;

namespace VNLib.Plugins
{
    /*
     * Config matches VNLib.Webserver.Config.Model.LogConfig
     * Must remain up-to-date with server type and provide 
     * backwards compatible defaults
     */

    internal sealed class FileLogConfigJson
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("template")]
        public string? Template { get; set; }

        [JsonPropertyName("flush_sec")]
        public int FlushIntervalSeconds { get; set; } = 10;

        [JsonPropertyName("retained_files")]
        public int RetainedFiles { get; set; } = 31;

        [JsonPropertyName("file_size_limit")]
        public int FileSizeLimit { get; set; } = 500 * 1000 * 1024;

        [JsonPropertyName("interval")]
        public string Interval { get; set; } = "infinite";
    }
}