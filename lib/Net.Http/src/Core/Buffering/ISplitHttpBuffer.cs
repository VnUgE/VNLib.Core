/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ISplitHttpBuffer.cs 
*
* ISplitHttpBuffer.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http.Core.Buffering
{
    /// <summary>
    /// Represents a buffer manager that contains segments for binary and character buffers
    /// </summary>
    internal interface ISplitHttpBuffer : IHttpBuffer
    {
        /// <summary>
        /// Gets the character segment of the internal buffer as a span of chars, which may be slower than <see cref="IHttpBuffer.GetBinSpan(int)"/>
        /// but still considered a hot-path
        /// </summary>
        /// <returns>The character segment of the internal buffer</returns>
        Span<char> GetCharSpan();

        /// <summary>
        /// The size of the internal binary buffer segment
        /// </summary>
        int BinSize { get; }
    }
}
