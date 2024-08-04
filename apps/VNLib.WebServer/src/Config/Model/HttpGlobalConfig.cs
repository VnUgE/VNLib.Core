/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: HttpGlobalConfig.cs 
*
* HttpGlobalConfig.cs is part of VNLib.WebServer which is part of the larger 
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
    internal class HttpGlobalConfig
    {

        [JsonPropertyName("default_version")]
        public string DefaultHttpVersion { get; set; } = "HTTP/1.1";

        /// <summary>
        /// The maximum size of a request entity that can be sent to the server.
        /// </summary>
        [JsonPropertyName("max_entity_size")]
        public long MaxEntitySize { get; set; } = long.MaxValue;

        /// <summary>
        /// The maximum size of a multipart form data upload.
        /// </summary>
        [JsonPropertyName("multipart_max_size")]
        public int MultipartMaxSize { get; set; } = 1048576;    //1MB

        /// <summary>
        /// The time in milliseconds for an HTTP/1.1 connection to remain open
        /// before the server closes it.
        /// </summary>
        [JsonPropertyName("keepalive_ms")]
        public int KeepAliveMs { get; set; } = 60000;           //60 seconds

        /// <summary>
        /// The time in milliseconds to wait for data on an active connection.
        /// IE: A connection that has been established and has signaled that 
        /// it is ready to transfer data.
        /// </summary>
        [JsonPropertyName("recv_timeout_ms")]
        public int RecvTimeoutMs { get; set; } = 5000;          //5 seconds

        /// <summary>
        /// The time in milliseconds to wait for data to be sent on a connection.
        /// </summary>
        [JsonPropertyName("send_timeout_ms")]
        public int SendTimeoutMs { get; set; } = 60000;         //60 seconds

        /// <summary>
        /// The maximum number of headers that can be sent in a request.
        /// </summary>
        [JsonPropertyName("max_request_header_count")]
        public int MaxRequestHeaderCount { get; set; } = 32;

        /// <summary>
        /// The maximum number of open connections that can be made to the server, before
        /// the server starts rejecting new connections.
        /// </summary>
        [JsonPropertyName("max_connections")]
        public int MaxConnections { get; set; } = int.MaxValue;

        /// <summary>
        /// The maximum number of uploads that can be made in a single request. If 
        /// this value is exceeded, the request will be rejected.
        /// </summary>
        [JsonPropertyName("max_uploads_per_request")]
        public ushort MaxUploadsPerRequest { get; set; } = 10;

        /// <summary>
        /// The size of the buffer used to store request headers.
        /// </summary>
        [JsonPropertyName("header_buf_size")]
        public int HeaderBufSize { get; set; }

        /// <summary>
        /// The size of the buffer used to store response headers.
        /// </summary>
        [JsonPropertyName("response_header_buf_size")]
        public int ResponseHeaderBufSize { get; set; }

        /// <summary>
        /// The size of the buffer used to store form data.
        /// </summary>
        [JsonPropertyName("multipart_max_buf_size")]
        public int MultipartMaxBufSize { get; set; }

        /// <summary>
        /// The configuration for the HTTP compression settings.
        /// </summary>
        [JsonPropertyName("compression")]
        public HttpCompressorConfig? Compression { get; set; } = new();

        public void ValidateConfig()
        {
            Validate.EnsureNotNull(DefaultHttpVersion, "Default HTTP version is required");

            Validate.EnsureRange(MaxEntitySize, 0, long.MaxValue);
            Validate.EnsureRange(MultipartMaxSize, -1, int.MaxValue);
            Validate.EnsureRange(KeepAliveMs, -1, int.MaxValue);

            //Timeouts may be disabled by setting 0 or -1. Both are allowed for readability
            Validate.EnsureRange(RecvTimeoutMs, -2, int.MaxValue);
            Validate.EnsureRange(SendTimeoutMs, -2, int.MaxValue);

            Validate.EnsureRange(MaxRequestHeaderCount, 0, 1024);
            Validate.EnsureRange(MaxConnections, 0, int.MaxValue);
            Validate.EnsureRange(MaxUploadsPerRequest, 0, 1024);
            Validate.EnsureRange(HeaderBufSize, 0, int.MaxValue);
            Validate.EnsureRange(ResponseHeaderBufSize, 0, int.MaxValue);
            Validate.EnsureRange(MultipartMaxBufSize, 0, int.MaxValue);

            //Validate compression config
            Validate.EnsureNotNull(Compression, "Compression configuration should not be set to null. Comment to enable defaults");
            Validate.EnsureRange(Compression.CompressionMax, -1, long.MaxValue);
            Validate.EnsureRange(Compression.CompressionMin, -1, int.MaxValue);
        }
    }
}
