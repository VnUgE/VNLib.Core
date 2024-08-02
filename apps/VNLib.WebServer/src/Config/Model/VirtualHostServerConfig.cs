/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: VirtualHostServerConfig.cs 
*
* VirtualHostServerConfig.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VNLib.WebServer.Config.Model
{
    internal sealed class VirtualHostServerConfig
    {
        [JsonPropertyName("trace")]
        public bool RequestTrace { get; set; } = false;

        [JsonPropertyName("force_port_check")]
        public bool ForcePortCheck { get; set; } = false;

        [JsonPropertyName("benchmark")]
        public BenchmarkConfig? Benchmark { get; set; }

        [JsonPropertyName("interfaces")]
        public TransportInterface[] Interfaces { get; set; } = Array.Empty<TransportInterface>();

        [JsonPropertyName("hostnames")]
        public string[]? Hostnames { get; set; } = Array.Empty<string>();

        [JsonPropertyName("hostname")]
        public string? Hostname
        {
            get => Hostnames?.FirstOrDefault();
            set
            {
                if (value != null)
                {
                    Hostnames = [value];
                }
            }
        }

        [JsonPropertyName("path")]
        public string? DirPath { get; set; } = string.Empty;

        [JsonPropertyName("downstream_servers")]
        public string[] DownstreamServers { get; set; } = Array.Empty<string>();

        [JsonPropertyName("whitelist")]
        public string[]? Whitelist { get; set; }

        [JsonPropertyName("blacklist")]
        public string[]? Blacklist { get; set; }

        [JsonPropertyName("deny_extensions")]
        public string[]? DenyExtensions { get; set; }

        [JsonPropertyName("default_files")]
        public string[]? DefaultFiles { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = [];

        [JsonPropertyName("cors")]
        public CorsSecurityConfig Cors { get; set; } = new();

        [JsonPropertyName("error_files")]
        public ErrorFileConfig[] ErrorFiles { get; set; } = Array.Empty<ErrorFileConfig>();

        [JsonPropertyName("cache_default_sec")]
        public int CacheDefaultTimeSeconds { get; set; } = 0;

        [JsonPropertyName("path_filter")]
        public string? PathFilter { get; set; }

    }
}
