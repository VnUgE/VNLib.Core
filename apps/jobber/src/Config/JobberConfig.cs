/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: jobber
* File: JobberConfig.cs
*
* JobberConfig.cs is part of jobber which is part of the larger 
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

    internal sealed class JobberConfig : IJsonOnDeserialized
    {
        [JsonPropertyName("stop_timeout_sec")]
        public int StopTimeoutSeconds { get; set; } = 15;

        [JsonPropertyName("services")]
        public ServiceConfig[] Services { get; set; } = Array.Empty<ServiceConfig>();

        public void OnDeserialized()
        {
            Validate.EnsureRange(
                StopTimeoutSeconds,
                1,
                600,
                "stop_timeout_sec out of range (1-600)"
            );

            Validate.Assert(
                Services.Length > 0,
                "At least one service must be defined"
            );

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ServiceConfig s in Services)
            {
                Validate.EnsureNotNull(s.Name, "Service missing name");
                Validate.Assert(names.Add(s.Name!), $"Duplicate service name '{s.Name}'");
                s.Validate();
            }
        }
    }
}