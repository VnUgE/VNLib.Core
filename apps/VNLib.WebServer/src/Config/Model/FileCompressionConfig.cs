/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: FileCompressionConfig.cs 
*
* FileCompressionConfig.cs is part of VNLib.WebServer which is part of the
* larger VNLib collection of libraries and utilities.
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
    internal sealed class FileCompressionConfig : IJsonOnDeserialized
    {
        /// <summary>
        /// Allows users to define a set of file types to exclude from dynamic 
        /// response compression (e.g. "jpg", "png", "zip"). This is useful for 
        /// preventing the server from attempting to compress files that are 
        /// already compressed. 
        /// </summary>
        [JsonPropertyName("disabled_file_types")]
        public string[]? DisabledFileTypes { get; init; }

        public void OnDeserialized()
        {
            if (DisabledFileTypes is not null)
            {
                foreach (string type in DisabledFileTypes)
                {
                    Validate.EnsureNotNull(type, "File type cannot be null.");
                    Validate.Assert(type[0] == '.', $"File type '{type}' must start with a dot.");
                    Validate.Assert(type.Length > 1, $"File type '{type}' is not a valid file extension");
                }
            }
        }
    }
}
