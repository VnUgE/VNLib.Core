/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpBufferManager.cs 
*
* IHttpBufferManager.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http.Core.Buffering
{

    /// <summary>
    /// <para>
    /// Represents an internal http buffer manager which manages the allocation and deallocation of internal buffers
    /// for specific http operations.
    /// </para>
    /// <para>
    /// Methods are considered on-demand and should be called only when the buffer is needed.
    /// </para>
    /// <para>
    /// Properties are considered persistent, however properties and method return values 
    /// are considered on-demand.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This abstraction assumes that the buffer manager is used in a single-threaded context.
    /// </remarks>
    internal interface IHttpBufferManager
    {
        /// <summary>
        /// Gets the independent buffer block used to buffer response data
        /// </summary>
        /// <returns>The memory block used for buffering application response data</returns>
        Memory<byte> GetResponseDataBuffer();

        /// <summary>
        /// Gets the independent buffer used to discard data request data
        /// </summary>
        /// <returns>The memory block used for discarding request data</returns>
        Memory<byte> GetDiscardBuffer();

        /// <summary>
        /// Gets a buffer used for buffering form-data
        /// </summary>
        /// <returns>The memory block</returns>
        Memory<byte> GetFormDataBuffer();

        /// <summary>
        /// Gets the request header parsing buffer element
        /// </summary>
        IHttpHeaderParseBuffer RequestHeaderParseBuffer { get; }

        /// <summary>
        /// Gets the response header accumulator buffer element
        /// </summary>
        IResponseHeaderAccBuffer ResponseHeaderBuffer { get; }

        /// <summary>
        /// Gets the chunk accumulator buffer element
        /// </summary>
        IChunkAccumulatorBuffer ChunkAccumulatorBuffer { get; }

        /// <summary>
        /// Alloctes internal buffers from the given <see cref="IHttpMemoryPool"/>
        /// </summary>
        /// <param name="allocator">The pool to allocate memory from</param>
        void AllocateBuffer(IHttpMemoryPool allocator);

        /// <summary>
        /// Zeros all internal buffers
        /// </summary>
        void ZeroAll();

        /// <summary>
        /// Frees all internal buffers
        /// </summary>
        void FreeAll();
    }
}
