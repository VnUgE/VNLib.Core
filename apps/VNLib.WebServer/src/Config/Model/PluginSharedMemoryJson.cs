/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: PluginSharedMemoryJson.cs 
*
* PluginSharedMemoryJson.cs is part of VNLib.WebServer which is part of 
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

using System.Diagnostics;
using System.Text.Json.Serialization;

using VNLib.Utils.Memory;
using VNLib.Plugins.Essentials.ServiceStack.Plugins.Ipc;

namespace VNLib.WebServer.Config.Model
{
    internal class PluginSharedMemoryJson : IJsonOnDeserialized
    {

        /// <summary>
        /// A boolean that enables or disables the plugin ipc shared memory system
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; } = true;

        /// <summary>
        /// Maximum region block allocation size in bytes
        /// </summary>
        [JsonPropertyName("max_region_size")]
        public int MaxRegionSize { get; init; } = 64 * 1024;

        /// <summary>
        /// Minimum region block allocation size in bytes. Blocks smaller than this size will
        /// be rejected.
        /// </summary>
        [JsonPropertyName("min_region_size")]
        public int MinRegionSize { get; init; } = 1;

        /// <summary>
        /// Gets the <see cref="PluginSharedMemoryConfig"/> from the deserialized properties.
        /// This creates a new configuration object on invocation. Check <see cref="Enabled"/> before
        /// calling this function. Asserts in debug mode.
        /// </summary>
        /// <returns>The configured <see cref="PluginSharedMemoryConfig"/> from the current values</returns>
        public PluginSharedMemoryConfig GetConfig()
        {
            Debug.Assert(Enabled);

            IUnmanagedHeap heap = MemoryUtil.Shared;

            return new PluginSharedMemoryConfig
            {
                Allocator = new PluginSharedMemoryAllocator(heap, zeroAllocations: true),
                MaxRegionSize = MaxRegionSize,
                MinRegionSize = MinRegionSize,
            };
        }

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (!Enabled)
            {
                return;
            }

            Validate.EnsureRange(MaxRegionSize, MinRegionSize, int.MaxValue, "shared_memory.max_region_size");
            Validate.EnsureRange(MinRegionSize, 1, MaxRegionSize, "shared_memory.min_region_size");
        }
    }
}
