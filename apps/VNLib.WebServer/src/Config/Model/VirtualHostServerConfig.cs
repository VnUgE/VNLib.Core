/*
* Copyright (c) 2026 Vaughn Nugent
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
using System.Text.RegularExpressions;

namespace VNLib.WebServer.Config.Model
{
    internal sealed class VirtualHostServerConfig : IJsonOnDeserialized
    {
        /// <summary>
        /// Whether this virtual host is enabled and should handle requests
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Enables request tracing and diagnostic logging for this virtual host
        /// </summary>
        [JsonPropertyName("trace")]
        public bool RequestTrace { get; set; } = false;

        /// <summary>
        /// Ensures the port written into the http HOST header matches the
        /// port of the interface the connection was actually received on.
        /// <para>
        /// When enabled, often breaks reverse proxies that forward requests
        /// to the server that's listening on a different port than this server.
        /// </para>
        /// </summary>
        [JsonPropertyName("force_port_check")]
        public bool ForcePortCheck { get; set; } = false;

        /// <summary>
        /// Optional benchmarking configuration to enable performance metrics collection
        /// </summary>
        [JsonPropertyName("benchmark")]
        public BenchmarkConfig? Benchmark { get; set; }

        /// <summary>
        /// The network interfaces this virtual host listens on. Each interface 
        /// specifies an IP address, port, and optional TLS configuration.
        /// </summary>
        [JsonPropertyName("interfaces")]
        public TransportInterface[] Interfaces { get; set; } = [];

        /// <summary>
        /// The hostnames this virtual host responds to. Incoming requests with 
        /// a Host header matching one of these values will be routed to this virtual host.
        /// </summary>
        [JsonPropertyName("hostnames")]
        public string[]? Hostnames { get; set; } = [];

        [Obsolete("Prefer 'hostname' array")]
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

        /// <summary>
        /// The directory that this virtual host will serve files from
        /// </summary>
        [JsonPropertyName("path")]
        public string? DirPath { get; set; } = string.Empty;

        /// <summary>
        /// An array of IP addresses to trust when the server is behind a
        /// reverse proxy. These downstream servers will be trusted
        /// to provide accurate client IP information in the X-Forwarded-For header.
        /// </summary>
        [JsonPropertyName("downstream_servers")]
        public string[] DownstreamServers { get; set; } = [];

        /// <summary>
        /// An array of IP addresses that are allowed to access this virtual host. 
        /// If this array is empty, all IPs are allowed by default.
        /// </summary>
        [JsonPropertyName("whitelist")]
        public string[]? Whitelist { get; set; }

        /// <summary>
        /// An array of IP addresses that are denied access to this virtual host.
        /// </summary>
        [JsonPropertyName("blacklist")]
        public string[]? Blacklist { get; set; }

        /// <summary>
        /// An array of file extensions that should be denied by the static file server.
        /// Requests for files with these extensions will be blocked.
        /// </summary>
        [JsonPropertyName("deny_extensions")]
        public string[]? DenyExtensions { get; set; }

        /// <summary>
        /// An array of default file names to serve when a directory is requested.
        /// Files are checked in order, and the first match is served (e.g., ["index.html", "index.htm"]).
        /// </summary>
        [JsonPropertyName("default_files")]
        public string[]? DefaultFiles { get; set; }

        /// <summary>
        /// Custom HTTP headers to add to all responses from this virtual host.
        /// The key is the header name, and the value is the header value.
        /// </summary>
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = [];

        /// <summary>
        /// Cross-Origin Resource Sharing (CORS) security configuration for this virtual host
        /// </summary>
        [JsonPropertyName("cors")]
        public CorsSecurityConfig Cors { get; set; } = new();

        /// <summary>
        /// Custom error page configuration that maps HTTP status codes to static files
        /// </summary>
        [JsonPropertyName("error_files")]
        public ErrorFileConfig[] ErrorFiles { get; set; } = [];

        /// <summary>
        /// Default cache time in seconds for static files served by this virtual host.
        /// This value is used when no file-specific cache time is configured.
        /// </summary>
        [JsonPropertyName("cache_default_sec")]
        public int CacheDefaultTimeSeconds { get; set; } = 0;

        /// <summary>
        /// Optional regex pattern to filter or validate incoming request paths.
        /// Requests that do not match this pattern will be rejected.
        /// </summary>
        [JsonPropertyName("path_filter")]
        public string? PathFilter { get; set; }

        /// <summary>
        /// Maximum execution time in milliseconds for request processing on this virtual host.
        /// Requests exceeding this limit will be terminated to prevent resource exhaustion.
        /// </summary>
        [JsonPropertyName("max_execution_time_ms")]
        public int MaxExecutionTimeMs { get; set; } = 20000;

        /// <summary>
        /// Maps file extensions to HTTP Cache-Control max-age values in seconds.
        /// The key is the file extension (e.g., ".js", ".css"), and the value is the max-age in seconds.
        /// </summary>
        [JsonPropertyName("file_http_max_age")]
        public Dictionary<string, int> FileHttpCacheMaxAge { get; set; } = [];

        /// <summary>
        /// Allows users to control automatic http response compression for file serving
        /// </summary>
        [JsonPropertyName("file_compression")]
        public FileCompressionConfig? FileCompressionConfig { get; init; }

        public void OnDeserialized()
        {
            if (!Enabled)
            {
                return;
            }

            Validate.EnsureNotNull(DirPath, "A virtual host was defined without a root directory property: 'DirPath'");

            {
                Validate.EnsureNotNull(Hostnames, "A virtual host was defined without a hostname property: 'Hostnames'");
                Validate.Assert(Hostnames.Length > 0, $"You must define at least one hostname for the host");

                foreach (string hostname in Hostnames)
                {
                    Validate.EnsureNotNull(hostname, "Hostname is null, all hostnames must be defined");
                }
            }

            {
                Validate.EnsureNotNull(Interfaces, "An interface configuration is required for every virtual host");
                Validate.Assert(Interfaces.Length > 0, $"You must define at least one interface for the host");

                // Validate each interface
                for (int i = 0; i < Interfaces.Length; i++)
                {
                    TransportInterface iFace = Interfaces[i];

                    Validate.EnsureNotNull(iFace, $"Virtual host interface [{i}] is undefined");

                    Validate.EnsureNotNull(iFace.Address, $"The interface IP address is required for interface [{i}]");
                    Validate.EnsureValidIp(iFace.Address, $"The interface IP address is invalid for interface [{i}]");
                    Validate.EnsureRange(iFace.Port, 1, 65535, "Interface port");
                }
            }

            if (Whitelist?.Length > 0)
            {
                foreach (string ip in Whitelist)
                {
                    Validate.EnsureNotNull(ip, "Whitelist IP address is null, all entries must be defined");
                    Validate.EnsureValidIp(ip, $"Whitelist IP address is invalid: {ip}");
                }
            }

            if (Blacklist?.Length > 0)
            {
                foreach (string ip in Blacklist)
                {
                    Validate.EnsureNotNull(ip, "Blacklist IP address is null, all entries must be defined");
                    Validate.EnsureValidIp(ip, $"Blacklist IP address is invalid: {ip}");
                }
            }

            if (DownstreamServers?.Length > 0)
            {
                foreach (string server in DownstreamServers)
                {
                    Validate.EnsureNotNull(server, "Downstream server address is null, all entries must be defined");
                    Validate.EnsureValidIp(server, $"Downstream server address is invalid: {server}");
                }
            }

            if (DefaultFiles?.Length > 0)
            {
                foreach (string file in DefaultFiles)
                {
                    Validate.EnsureNotNull(file, "Default file name is null, all entries must be defined");
                    //Ensure the format looks like a plain file name with an extension.
                    //This rejects path separators, traversal sequences, and extensionless names.
                    Validate.Assert(
                        Regex.IsMatch(file, @"^(?!.*\.\.)[a-zA-Z0-9_.-]+\.[a-zA-Z]{2,}$"),
                        $"The file path: {file} is not a valid file path format"
                    );
                }
            }

            foreach (KeyValuePair<string, string> header in Headers)
            {
                Validate.EnsureNotNull(header.Key, "Custom header name cannot be null");
                Validate.EnsureNotNull(header.Value, $"Custom header value cannot be null for header: {header.Key}");
            }

            foreach (KeyValuePair<string, int> cacheEntry in FileHttpCacheMaxAge)
            {
                var (k, v) = cacheEntry;

                Validate.EnsureNotNull(k, "File extension for HTTP cache max-age cannot be null");
                Validate.Assert(k[0] == '.', $"File extension must start with a '.' character for {k}");
                Validate.Assert(k.Length > 1, $"File extension must have at least one character after the '.' for {k}");

                Validate.EnsureRange(v, 0, int.MaxValue, $"HTTP cache max-age must be non-negative for extension: {k}");
            }
        }
    }
}
