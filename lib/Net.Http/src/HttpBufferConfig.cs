/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpBufferConfig.cs 
*
* HttpBufferConfig.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// Holds configuration constants for http protocol buffers
    /// </summary>
    public readonly record struct HttpBufferConfig()
    {
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
        /// The buffer size used to read HTTP headers from the transport. 
        /// </summary>
        /// <remarks>
        /// Setting this value too low will result in header parsing failures
        /// and 400 Bad Request responses. Setting it too high can result in
        /// resource abuse or high memory usage. 8k is usually a good value.
        /// </remarks>
        public readonly int RequestHeaderBufferSize { get; init; } = 8192;

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
    }
}