/*
* Copyright (c) 2022 Vaughn Nugent
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
    public readonly struct HttpConfig
    {
        public HttpConfig(ILogProvider log)
        {
            ConnectionKeepAlive = TimeSpan.FromSeconds(100);
            ServerLog = log;
        }

        /// <summary>
        /// A log provider that all server related log entiries will be written to
        /// </summary>
        public readonly ILogProvider ServerLog { get; }
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
        /// The maximum buffer size to use when parsing Multi-part/Form-data file uploads
        /// </summary>
        /// <remarks>
        /// This value is used to create the buffer used to read data from the input stream
        /// into memory for parsing. Form-data uploads must be parsed in memory because
        /// the data is not delimited by a content length.
        /// </remarks>
        public readonly int FormDataBufferSize { get; init; } = 8192;
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
        public readonly TimeSpan ConnectionKeepAlive { get; init; }
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
        /// The buffer size used to read HTTP headers from the transport. 
        /// </summary>
        /// <remarks>
        /// Setting this value too low will result in header parsing failures
        /// and 400 Bad Request responses. Setting it too high can result in
        /// resource abuse or high memory usage. 8k is usually a good value.
        /// </remarks>
        public readonly int HeaderBufferSize { get; init; } = 8192;
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
        /// The size (in bytes) of the http response header accumulator buffer.
        /// </summary>
        /// <remarks>
        /// Http responses use an internal accumulator to buffer all response headers
        /// before writing them to the transport in on write operation. If this value
        /// is too low, the response will fail to write. If it is too high, it 
        /// may cause resource exhaustion or high memory usage.
        /// </remarks>
        public readonly int ResponseHeaderBufferSize { get; init; } = 16 * 1024;
        /// <summary>
        /// The size (in bytes) of the buffer to use to discard unread request entity bodies
        /// </summary>
        public readonly int DiscardBufferSize { get; init; } = 64 * 1024;
        /// <summary>
        /// The size of the buffer to use when writing response data to the transport
        /// </summary>
        /// <remarks>
        /// This value is the size of the buffer used to copy data from the response 
        /// entity stream, to the transport stream.
        /// </remarks>
        public readonly int ResponseBufferSize { get; init; } = 32 * 1024;
        /// <summary>
        /// The size of the buffer used to accumulate chunked response data before writing to the transport
        /// </summary>
        public readonly int ChunkedResponseAccumulatorSize { get; init; } = 64 * 1024;
        /// <summary>
        /// An <see cref="ILogProvider"/> for writing verbose request logs. Set to <c>null</c> 
        /// to disable verbose request logging
        /// </summary>
        public readonly ILogProvider? RequestDebugLog { get; init; } = null;
    }
}