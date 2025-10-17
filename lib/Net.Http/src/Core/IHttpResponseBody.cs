/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpResponseBody.cs 
*
* IHttpResponseBody.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Threading.Tasks;

using VNLib.Net.Http.Core.Response;
using VNLib.Net.Http.Core.Compression;

namespace VNLib.Net.Http.Core
{
    /*
     * Optimization notes. The buffer parameters are undefined unless 
     * the BufferRequired property is true. 
     */

    /// <summary>
    /// Represents a rseponse entity body
    /// </summary>
    internal interface IHttpResponseBody
    {
        /// <summary>
        /// A value that indicates if there is data 
        /// to send to the client
        /// </summary>
        bool HasData { get; }

        /// <summary>
        /// A value that indicates if response data requires buffering
        /// </summary>
        bool BufferRequired { get; }

        /// <summary>
        /// The length of the content. Length is a required property
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Writes internal response entity data to the destination stream
        /// </summary>
        /// <param name="dest">The response stream to write data to</param>
        /// <param name="buffer">An optional buffer used to buffer responses</param>
        /// <returns>A task that resolves when the response is completed</returns>
        Task WriteEntityAsync(IDirectResponsWriter dest, Memory<byte> buffer);

        /// <summary>
        /// Writes internal response entity data to the destination stream
        /// </summary>
        /// <param name="writer">The response output writer</param>
        /// <param name="buffer">An optional buffer used to buffer responses</param>
        /// <param name="method">The compression method to use when writing the response</param>
        /// <returns>A task that resolves when the response is completed</returns>
        Task WriteEntityAsync(IResponseDataWriter writer, Memory<byte> buffer, CompressionMethod method);
    }
}