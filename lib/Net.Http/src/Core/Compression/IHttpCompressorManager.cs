﻿/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpCompressorManager.cs 
*
* IHttpCompressorManager.cs is part of VNLib.Net.Http which is part 
* of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Net.Http
{

    /// <summary>
    /// Represents an http compressor manager that creates compressor state instances and processes
    /// compression operations.
    /// </summary>
    /// <remarks>
    /// All method calls must be thread-safe. Method calls on a given compressor state are guarunteed
    /// to be thread-safe, but method calls on different compressor states are not.
    /// </remarks>
    public interface IHttpCompressorManager
    {
        /// <summary>
        /// Gets the supported compression methods for this compressor manager
        /// </summary>
        /// <returns>The supported compression methods</returns>
        /// <remarks>
        /// Called when the server starts to cache the value. All supported methods must be returned
        /// before constructing the server.
        /// </remarks>
        CompressionMethod GetSupportedMethods();

        /// <summary>
        /// Allocates a new compressor state object that will be used for compression operations.
        /// </summary>
        /// <returns>The compressor state</returns>
        object AllocCompressor();

        /// <summary>
        /// Compresses a block of data using the compressor state. The input block size is 
        /// guarunteed to be smaller than the block size returned by <see cref="InitCompressor(object, CompressionMethod)"/>
        /// or smaller.
        /// </summary>
        /// <param name="compressorState">The compressor state instance</param>
        /// <param name="input">The input buffer to compress</param>
        /// <param name="output">The output buffer to write the compressed data to</param>
        /// <returns>The result of the stream operation</returns>
        CompressionResult CompressBlock(object compressorState, ReadOnlyMemory<byte> input, Memory<byte> output);

        /// <summary>
        /// Flushes any stored compressor data that still needs to be sent to the client.
        /// </summary>
        /// <param name="compressorState">The compressor state instance</param>
        /// <param name="output">The output buffer</param>
        /// <returns>The number of bytes flushed to the output buffer</returns>
        int Flush(object compressorState, Memory<byte> output);

        /// <summary>
        /// Initializes the compressor state for a compression operation
        /// </summary>
        /// <param name="compressorState">The user-defined compression state</param>
        /// <param name="compMethod">The compression method</param>
        /// <returns>The block size of the compressor, or <![CDATA[ <= 0 ]]> if block size is irrelavant </returns>
        int InitCompressor(object compressorState, CompressionMethod compMethod);

        /// <summary>
        /// Deinitializes the compressor state. This method is guarnteed to be called after 
        /// a call to <see cref="InitCompressor(object, CompressionMethod)"/> regardless of 
        /// the success of the operation involoving the compressor state
        /// </summary>
        /// <param name="compressorState">The initialized compressor state</param>
        void DeinitCompressor(object compressorState);
    }
}
