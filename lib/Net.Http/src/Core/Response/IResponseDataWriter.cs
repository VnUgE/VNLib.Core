/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IResponseDataWriter.cs 
*
* IResponseDataWriter.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http.Core.Response
{
    /// <summary>
    /// A buffered http response data writer 
    /// </summary>
    internal interface IResponseDataWriter
    {
        /// <summary>
        /// Gets the next memory segment available to buffer data to
        /// </summary>
        /// <returns>An available buffer to write response data to </returns>
        Memory<byte> GetMemory();

        /// <summary>
        /// Advances the writer by the number of bytes written and returns the 
        /// number of bytes available for writing on the next call to <see cref="GetMemory"/>
        /// </summary>
        /// <param name="written">The number of bytes written to the output buffer</param>
        /// <returns>The number of bytes remaining in the internal buffer</returns>
        int Advance(int written);

        /// <summary>
        /// Flushes the internal buffer to the underlying stream
        /// </summary>
        /// <param name="isFinal">A value that indicates that this is final call to flush</param>
        /// <returns>A valuetask that completes </returns>
        ValueTask FlushAsync(bool isFinal);
    }
}