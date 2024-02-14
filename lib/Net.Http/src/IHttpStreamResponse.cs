/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpStreamResponse.cs 
*
* IHttpStreamResponse.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents a stream of data to be sent to a client as an HTTP response.
    /// </summary>
    public interface IHttpStreamResponse : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Reads data from the stream into the buffer asynchronously
        /// and returns the number of bytes read, or 0 if the end of the stream 
        /// has been reached.
        /// </summary>
        /// <param name="buffer">The output buffer to write data into</param>
        /// <returns>The number of bytes read into the output buffer</returns>
        ValueTask<int> ReadAsync(Memory<byte> buffer);
    }
}