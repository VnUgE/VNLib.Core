/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: TcpServerLoader.cs 
*
* TcpServerLoader.cs is part of VNLib.WebServer which is part of the larger 
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

using System.Text.Json.Serialization;

namespace VNLib.WebServer.Config.Model
{
    internal sealed class TcpConfigJson
    {
        [JsonPropertyName("keepalive_sec")]
        public int TcpKeepAliveTime { get; set; } = 4;

        [JsonPropertyName("keepalive_interval_sec")]
        public int KeepaliveInterval { get; set; } = 4;

        [JsonPropertyName("max_recv_buffer")]
        public int MaxRecvBufferData { get; set; } = 10 * 64 * 1024;

        [JsonPropertyName("backlog")]
        public int BackLog { get; set; } = 1000;

        [JsonPropertyName("max_connections")]
        public long MaxConnections { get; set; } = long.MaxValue;

        [JsonPropertyName("no_delay")]
        public bool NoDelay { get; set; } = false;

        /*
         * Buffer sizes are a pain, this is a good default size for medium bandwith connections (100mbps)
         * using the BDP calculations
         * 
         *   BDP = Bandwidth * RTT  
         */

        [JsonPropertyName("tx_buffer")]
        public int TcpSendBufferSize { get; set; } = 625 * 1024;

        [JsonPropertyName("rx_buffer")]
        public int TcpRecvBufferSize { get; set; } = 625 * 1024;


        public void ValidateConfig()
        {
            Validate.EnsureRange(TcpKeepAliveTime, 0, 60);
            Validate.EnsureRange(KeepaliveInterval, 0, 60);
            Validate.EnsureRange(BackLog, 0, 10000);
            Validate.EnsureRange(MaxConnections, 0, long.MaxValue);
            Validate.EnsureRange(MaxRecvBufferData, 0, 10 * 1024 * 1024); //10MB
            Validate.EnsureRange(TcpSendBufferSize, 0, 10 * 1024 * 1024); //10MB
        }
    }
}
