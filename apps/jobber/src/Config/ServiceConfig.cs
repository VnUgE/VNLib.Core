/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: jobber
* File: ServiceConfig.cs
*
* ServiceConfig.cs is part of jobber which is part of the larger 
* VNLib collection of libraries and utilities.
*
* jobber is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* jobber is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with jobber. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jobber.Config
{

    internal sealed class ServiceConfig : IJsonOnDeserialized
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("command")]
        public string? Command { get; set; }

        [JsonPropertyName("args")]
        public string[] Args { get; set; } = Array.Empty<string>();

        [JsonPropertyName("working_dir")]
        public string? WorkingDirectory { get; set; }

        [JsonPropertyName("env")]
        public Dictionary<string, string>? Environment { get; set; }

        [JsonPropertyName("depends_on")]
        public string[] DependsOn { get; set; } = Array.Empty<string>();

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }

        [JsonPropertyName("shutdown_with_dependents")]
        public bool ShutdownWithDependents { get; set; }

        [JsonPropertyName("tee")]
        public TeeConfig? Tee { get; set; }

        [JsonPropertyName("wait_for_exit")]
        public bool WaitForExit { get; set; }

        public void OnDeserialized() => Validate();

        public void Validate()
        {
            Config.Validate.EnsureNotNull(Command, $"Service '{Name}' missing command");
            Tee?.Validate();
        }
    }
}