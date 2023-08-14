/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: INativeCompressionLib.cs 
*
* INativeCompressionLib.cs is part of VNLib.Net.Compression which is part of 
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
using System.Runtime.InteropServices;

using VNLib.Net.Http;

namespace VNLib.Net.Compression
{
    /// <summary>
    /// Represents a native compression library that can create native 
    /// compressor instances.
    /// </summary>
    public interface INativeCompressionLib
    {
        /// <summary>
        /// Gets the compression methods supported by the underluing library
        /// </summary>
        /// <returns>The supported compression methods</returns>
        CompressionMethod GetSupportedMethods();

        /// <summary>
        /// Allocates a new <see cref="INativeCompressor"/> implementation that allows for 
        /// compressing stream data.
        /// </summary>
        /// <param name="method">The desired <see cref="CompressionMethod"/>, must be a supported method</param>
        /// <param name="level">The desired <see cref="CompressionLevel"/> to compress blocks with</param>
        /// <returns>The new <see cref="INativeCompressor"/></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException">The the level or method are not supported by the underlying library</exception>
        INativeCompressor AllocCompressor(CompressionMethod method, CompressionLevel level);

        /// <summary>
        /// Allocates a safe compressor handle to allow native operations if preferred.
        /// </summary>
        ///<param name="method">The desired <see cref="CompressionMethod"/>, must be a supported method</param>
        /// <param name="level">The desired <see cref="CompressionLevel"/> to compress blocks with</param>
        /// <returns>A new <see cref="SafeHandle"/> that holds a pointer to the native compressor context</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException">The the level or method are not supported by the underlying library</exception>
        SafeHandle AllocSafeCompressorHandle(CompressionMethod method, CompressionLevel level);
    }
}