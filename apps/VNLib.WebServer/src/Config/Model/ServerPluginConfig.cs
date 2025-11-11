/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: ServerPluginConfig.cs 
*
* ServerPluginConfig.cs is part of VNLib.WebServer which is part of 
* the larger VNLib collection of libraries and utilities.
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

using System.Text.Json.Serialization;

namespace VNLib.WebServer.Config.Model
{
    internal class ServerPluginConfig : IJsonOnDeserialized
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; } = true; //default to true if config is defined, then it can be assumed we want to load plugins unless explicitly disabled

        [JsonPropertyName("path")]
        public string Path { get; set; } = null!;

        [JsonPropertyName("config_dir")]
        public string? ConfigDir { get; set; } = null;

        [JsonPropertyName("hot_reload")]
        public bool HotReload { get; init; }

        [JsonPropertyName("reload_delay_sec")]
        public int ReloadDelaySec { get; init; } = 2;

        public void OnDeserialized()
        {
            if (!Enabled)
            {
                return;
            }

            Validate.EnsureNotNull(Path, "If plugins are enabled, you must specify a directory to load them from");
            Validate.EnsureRange(ReloadDelaySec, 0, 600, "Reload delay must be between 0 and 600 seconds");

            // Make path absolute
            Path = System.IO.Path.GetFullPath(Path);

            //In order to read files, config dir must exist
            if (ConfigDir is not null)
            {
                Validate.Assert(System.IO.Directory.Exists(ConfigDir), $"Config directory '{ConfigDir}' does not exist");

                //Make config dir absolute
                ConfigDir = System.IO.Path.GetFullPath(ConfigDir);
            }
        }
    }
}
