/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ServerPreEncodedSegments.cs 
*
* ServerPreEncodedSegments.cs is part of VNLib.Net.Http which
* is part of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// Holds pre-encoded buffer segments for http request/responses
    /// </summary>
    /// <param name="Buffer">
    /// Holds ref to internal buffer
    /// </param>
    internal readonly record struct ServerPreEncodedSegments(byte[] Buffer)
    {
        /// <summary>
        /// Holds a pre-encoded segment for all crlf (line termination) bytes
        /// </summary>
        public readonly HttpEncodedSegment CrlfBytes { get; init; } = default;

        /// <summary>
        /// Holds a pre-encoded segment for the final chunk termination
        /// in http chuncked encoding
        /// </summary>
        public readonly HttpEncodedSegment FinalChunkTermination { get; init; } = default;
    }
}