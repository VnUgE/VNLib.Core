/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: CompressionResult.cs 
*
* CompressionResult.cs is part of VNLib.Net.Http which is part 
* of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents the result of a block compression operation
    /// </summary>
    public readonly ref struct CompressionResult
    {
        /// <summary>
        /// The number of bytes read from the input buffer
        /// </summary>
        public readonly int BytesRead { get; init; }

        /// <summary>
        /// The number of bytes availabe in the output buffer
        /// </summary>
        public readonly int BytesWritten { get; init; }
    }
}
