/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IDirectResponsWriter.cs 
*
* IDirectResponsWriter.cs is part of VNLib.Net.Http which is part of 
* the larger VNLib collection of libraries and utilities.
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
    /// Represents a stream that can be written to directly (does not 
    /// buffer response data)
    /// </summary>
    internal interface IDirectResponsWriter
    {
        /// <summary>
        /// Writes the given data buffer to the client
        /// </summary>
        /// <param name="buffer">The response data to write</param>
        /// <returns>A value task that resolves when the write operation is complete</returns>
        ValueTask WriteAsync(ReadOnlyMemory<byte> buffer);
    }
}
