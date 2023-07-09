/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpEncodedSegment.cs 
*
* HttpEncodedSegment.cs is part of VNLib.Net.Http which is part of 
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

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// Holds a pre-encoded segment of data
    /// </summary>
    /// <param name="Buffer">The buffer containing the segment data</param>
    /// <param name="Offset">The offset in the buffer to begin the segment at</param>
    /// <param name="Length">The length of the segment</param>
    internal readonly record struct HttpEncodedSegment(byte[] Buffer, int Offset, int Length)
    {
        /// <summary>
        /// Span representation of the pre-encoded segment
        /// </summary>
        public Span<byte> Span => Buffer.AsSpan(Offset, Length);

        /// <summary>
        /// Memory representation of the pre-encoded segment
        /// </summary>
        public Memory<byte> Memory => Buffer.AsMemory(Offset, Length);
    }
}