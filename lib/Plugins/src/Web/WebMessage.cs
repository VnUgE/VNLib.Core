/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: WebMessage.cs 
*
* WebMessage.cs is part of VNLib.Plugins which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins. If not, see http://www.gnu.org/licenses/.
*/

using System.Text.Json.Serialization;

namespace VNLib.Plugins
{
    /// <summary>
    /// Represents a unified JSON client-server response message containing 
    /// an upgrade response token and result information for client communication
    /// </summary>
    public class WebMessage
    {
        /// <summary>
        /// The encrypted access token for the client to use after a login request
        /// </summary>
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        /// <summary>
        /// The result of the REST operation to send to client
        /// </summary>
        [JsonPropertyName("result")]
        public object? Result { get; set; }

        /// <summary>
        /// A status flag/result of the REST operation
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// A collection of error messages to send to clients
        /// </summary>
        [JsonPropertyName("errors")]
        public object? Errors { get; set; }
    }
}
