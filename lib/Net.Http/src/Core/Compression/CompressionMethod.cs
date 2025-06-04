/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: CompressionMethod.cs 
*
* CompressionMethod.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents a supported compression type
    /// </summary>
    [Flags]
    public enum CompressionMethod
    {
        /// <summary>
        /// No compression
        /// </summary>
        None = 0x00,
        /// <summary>
        /// GZip compression is required
        /// </summary>
        Gzip = 0x01,
        /// <summary>
        /// Deflate compression is required
        /// </summary>
        Deflate = 0x02,
        /// <summary>
        /// Brotli compression is required
        /// </summary>
        Brotli = 0x04,
        /// <summary>
        /// Zstandard compression is required
        /// </summary>
        Zstd = 0x08,
    }
}