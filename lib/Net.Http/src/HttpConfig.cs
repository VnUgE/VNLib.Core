/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpConfig.cs 
*
* HttpConfig.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Text;
using System.IO.Compression;

using VNLib.Utils.Logging;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents configration variables used to create the instance and manage http connections
    /// </summary>
    /// <param name="ServerLog">
    /// A log provider that all server related log entiries will be written to
    /// </param>
    /// <param name="MemoryPool">
    /// Server memory pool to use for allocating buffers
    /// </param>
    public readonly record struct HttpConfig(ILogProvider ServerLog, IHttpMemoryPool MemoryPool)
    {
        
        /// <summary>
        /// The absolute request entity body size limit in bytes
        /// </summary>
        public readonly int MaxUploadSize { get; init; } = 5 * 1000 * 1024;

        /// <summary>
        /// The maximum size in bytes allowed for an MIME form-data content type upload
        /// </summary>
        /// <remarks>Set to 0 to disabled mulit-part/form-data uploads</remarks>
        public readonly int MaxFormDataUploadSize { get; init; } = 40 * 1024;  
        
        /// <summary>
        /// The maximum response entity size in bytes for which the library will allow compresssing response data
        /// </summary>
        /// <remarks>Set this value to 0 to disable response compression</remarks>
        public readonly int CompressionLimit { get; init; } = 1000 * 1024;

        /// <summary>
        /// The minimum size (in bytes) of respones data that will be compressed
        /// </summary>
        public readonly int CompressionMinimum { get; init; } = 4096;

        /// <summary>
        /// The maximum amount of time to listen for data from a connected, but inactive transport connection
        /// before closing them
        /// </summary>
        public readonly TimeSpan ConnectionKeepAlive { get; init; } = TimeSpan.FromSeconds(100);

        /// <summary>
        /// The encoding to use when sending and receiving HTTP data
        /// </summary>
        public readonly Encoding HttpEncoding { get; init; } = Encoding.UTF8;

        /// <summary>
        /// Sets the compression level for response entity streams of all supported types when
        /// compression is used.
        /// </summary>
        public readonly CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;

        /// <summary>
        /// Sets the default Http version for responses when the client version cannot be parsed from the request 
        /// </summary>
        public readonly HttpVersion DefaultHttpVersion { get; init; } = HttpVersion.Http11;

        /// <summary>
        /// The amount of time (in milliseconds) to wait for data on a connection that is in a receive
        /// state, aka active receive.
        /// </summary>
        public readonly int ActiveConnectionRecvTimeout { get; init; } = 5000;

        /// <summary>
        /// The amount of time (in milliseconds) to wait for data to be send to the client before 
        /// the connection is closed
        /// </summary>
        public readonly int SendTimeout { get; init; } = 5000;

        /// <summary>
        /// The maximum number of request headers allowed per request
        /// </summary>
        public readonly int MaxRequestHeaderCount { get; init; } = 100;

        /// <summary>
        /// The maximum number of open transport connections, before 503 errors
        /// will be returned and new connections closed.
        /// </summary>
        /// <remarks>Set to 0 to disable request processing. Causes perminant 503 results</remarks>
        public readonly int MaxOpenConnections { get; init; } = int.MaxValue;      

        /// <summary>
        /// An <see cref="ILogProvider"/> for writing verbose request logs. Set to <c>null</c> 
        /// to disable verbose request logging
        /// </summary>
        public readonly ILogProvider? RequestDebugLog { get; init; } = null;

        /// <summary>
        /// The buffer configuration for the server
        /// </summary>
        public readonly HttpBufferConfig BufferConfig { get; init; } = new();
    }
}