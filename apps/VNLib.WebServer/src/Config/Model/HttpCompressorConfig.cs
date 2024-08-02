/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: HttpCompressorConfig.cs 
*
* HttpCompressorConfig.cs is part of VNLib.WebServer which is part of 
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
    internal sealed class HttpCompressorConfig
    {
        [JsonPropertyName("assembly")]
        public string? AssemblyPath { get; set; }

        /// <summary>
        /// If this compressor is enabled. The default is true, to use built-in
        /// compressors.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("max_size")]
        public long CompressionMax { get; set; } = 104857600;       //100MB

        [JsonPropertyName("min_size")]
        public int CompressionMin { get; set; } = 256;
    }
}
