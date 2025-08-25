/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpRange.cs 
*
* HttpRange.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// A structure that represents a range of bytes in an HTTP request
    /// </summary>
    /// <param name="Start">The offset from the start of the resource</param>
    /// <param name="End">The ending byte range</param>
    /// <param name="RangeType">Specifies the type of content range to observe</param>
    public readonly record struct HttpRange(ulong Start, ulong End, HttpRangeType RangeType)
    {
        /// <summary>
        /// Gets a value indicating if the range is valid. A range is valid if
        /// the start is less than or equal to the end.
        /// </summary>
        /// <param name="start">The starting range value</param>
        /// <param name="end">The ending range value</param>
        /// <returns>True if the range values are valid, false otherwise</returns>
        public static bool IsValidRangeValue(ulong start, ulong end) => start <= end;


        internal static HttpRange FromStart(ulong start) => new(start, 0, HttpRangeType.FromStart);

        internal static HttpRange FromEnd(ulong end) => new(0, end, HttpRangeType.FromEnd);

        internal static HttpRange FullRange(ulong start, ulong end) => new(start, end, HttpRangeType.FullRange);
    }
}