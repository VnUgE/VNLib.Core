/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: INativeCompressor.cs 
*
* INativeCompressor.cs is part of VNLib.Net.Compression which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Compression is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Net.Compression is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Net.Compression. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO.Compression;

using VNLib.Net.Http;

namespace VNLib.Net.Compression
{
    /// <summary>
    /// Represents a native compressor instance
    /// </summary>
    public interface INativeCompressor : IDisposable
    {
        /// <summary>
        /// Gets the underlying compressor type
        /// </summary>
        /// <returns>The underlying compressor type</returns>
        CompressionMethod GetCompressionMethod();

        /// <summary>
        /// Gets the underlying compressor's compression level
        /// </summary>
        /// <returns>The configured <see cref="CompressionLevel"/> of the current compressor</returns>
        CompressionLevel GetCompressionLevel();

        /// <summary>
        /// Flushes all remaining data in the compressor to the output buffer
        /// </summary>
        /// <param name="buffer">The output buffer to write flushed compressor data to</param>
        /// <returns>The number of bytes written to the output buffer</returns>
        int Flush(Memory<byte> buffer);

        /// <summary>
        /// Flushes all remaining data in the compressor to the output buffer
        /// </summary>
        /// <param name="buffer">The output buffer to write flushed compressor data to</param>
        /// <returns>The number of bytes written to the output buffer</returns>
        int Flush(Span<byte> buffer);

        /// <summary>
        /// Compresses the input block and writes the compressed data to the output block
        /// </summary>
        /// <param name="input">The input buffer to compress</param>
        /// <param name="output">The output buffer to write compressed data to</param>
        CompressionResult Compress(ReadOnlyMemory<byte> input, Memory<byte> output);

        /// <summary>
        /// Compresses the input block and writes the compressed data to the output block
        /// </summary>
        /// <param name="input">The input buffer to compress</param>
        /// <param name="output">The output buffer to write compressed data to</param>
        CompressionResult Compress(ReadOnlySpan<byte> input, Span<byte> output);

        /// <summary>
        /// Gets the compressor block size if configured
        /// </summary>
        /// <returns>The ideal input buffer size for compressing blocks, or <![CDATA[<1]]> if block size is unlimited</returns>
        uint GetBlockSize();

        /// <summary>
        /// Determines the maximum number of output bytes for the given number of input bytes 
        /// specified by the size parameter
        /// </summary>
        /// <param name="size">The number of bytes to get the compressed size of</param>
        /// <returns>The maxium size of the compressed data</returns>
        /// <exception cref="OverflowException"></exception>
        uint GetCompressedSize(uint size);
    }
}