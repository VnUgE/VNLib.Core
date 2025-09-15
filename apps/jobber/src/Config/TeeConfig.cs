/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: jobber
* File: TeeConfig.cs
*
* TeeConfig.cs is part of jobber which is part of the larger 
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

using System.Text.Json.Serialization;

namespace Jobber.Config
{
    internal sealed class TeeConfig : IJsonOnDeserialized
    {
        [JsonPropertyName("stdout")]
        public string? StdOutPath { get; set; }

        [JsonPropertyName("stderr")]
        public string? StdErrPath { get; set; }

        [JsonPropertyName("append")]
        public bool Append { get; set; } = true;

        public void OnDeserialized() => this.Validate();

        public void Validate()
        {
            // no-op; add path validation if required
        }
    }
}