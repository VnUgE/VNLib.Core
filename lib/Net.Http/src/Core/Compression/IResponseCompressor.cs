/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IResponseCompressor.cs 
*
* IResponseCompressor.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.IO;
using System.Threading.Tasks;

namespace VNLib.Net.Http.Core.Compression
{
    /// <summary>
    /// Represents a per-context compressor
    /// </summary>
    internal interface IResponseCompressor 
    {
        /// <summary>
        /// The desired block size for the compressor. This is an optimization feature.
        /// If the block size is unlimited, this should return 0. This value is only read
        /// after initialization
        /// </summary>
        int BlockSize { get; }

        /// <summary>
        /// Frees the resources used by the compressor on a compression operation
        /// </summary>
        void Free();

        /// <summary>
        /// Initializes the compressor for a compression operation
        /// </summary>
        /// <param name="compMethod">The compression mode to use</param>
        /// <param name="output">The stream to write compressed data to</param>
        void Init(Stream output, CompressionMethod compMethod);      

        /// <summary>
        /// Compresses a block of data and writes it to the output stream
        /// </summary>
        /// <param name="buffer">The block of memory to write to compress</param>
        /// <param name="finalBlock">A value that indicates if this block is the final block</param>
        /// <returns>A task that represents the compression operation</returns>
        ValueTask CompressBlockAsync(ReadOnlyMemory<byte> buffer, bool finalBlock);
    }
}