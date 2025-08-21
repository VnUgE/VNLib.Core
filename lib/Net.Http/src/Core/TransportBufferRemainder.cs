/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: TransportBufferRemainder.cs 
*
* TransportBufferRemainder.cs is part of VNLib.Net.Http which is part of the larger 
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

using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// Structure that represents the remainder of data in the transport buffer
    /// </summary>
    internal readonly ref struct TransportBufferRemainder(IHttpHeaderParseBuffer buffer, int offset, int size)
    {
        /// <summary>
        /// The size of the buffered data
        /// </summary>
        public readonly int Size = size;

        /// <summary>
        /// The offset into the buffer where the data starts
        /// </summary>
        public readonly int Offset = offset;

        /// <summary>
        /// The buffer that contains the data
        /// </summary>
        public readonly IHttpHeaderParseBuffer Buffer = buffer;
    }
}
