/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: CoreBufferHelpers.cs 
*
* CoreBufferHelpers.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Buffers;

using VNLib.Utils.IO;

namespace VNLib.Net.Http.Core
{

    /// <summary>
    /// Provides memory pools and an internal heap for allocations.
    /// </summary>
    internal static class CoreBufferHelpers
    {
        /// <summary>
        /// An internal HTTP character binary pool for HTTP specific internal buffers
        /// </summary>
        public static ArrayPool<byte> HttpBinBufferPool { get; } = ArrayPool<byte>.Create();

        /// <summary>
        /// Gets the remaining data in the reader buffer and prepares a 
        /// sliding window buffer to read data from
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="maxContentLength">Maximum content size to clamp the remaining buffer window to</param>
        /// <returns></returns>
        public static InitDataBuffer? GetReminaingData<T>(this ref T reader, long maxContentLength) where T: struct, IVnTextReader
        {
            //clamp max available to max content length
            int available = Math.Clamp(reader.Available, 0, (int)maxContentLength);
            if (available <= 0)
            {
                return null;
            }

            //Creates the new initial data buffer
            InitDataBuffer buf = InitDataBuffer.AllocBuffer(HttpBinBufferPool, available);

            //Read remaining data into the buffer's data segment
            _ = reader.ReadRemaining(buf.DataSegment);
           
            return buf;
        }
        
    }
}
