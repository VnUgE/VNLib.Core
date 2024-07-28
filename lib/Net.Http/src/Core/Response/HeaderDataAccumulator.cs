/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HeaderDataAccumulator.cs 
*
* HeaderDataAccumulator.cs is part of VNLib.Net.Http which is part of the larger 
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

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core.Response
{

    /// <summary>
    /// Specialized data accumulator for compiling response headers
    /// </summary>
    internal readonly struct HeaderDataAccumulator(IResponseHeaderAccBuffer accBuffer, IHttpContextInformation ctx)
    {
        private readonly IResponseHeaderAccBuffer _buffer = accBuffer;
        private readonly IHttpContextInformation _contextInfo = ctx;

        /// <summary>
        /// Gets the accumulated response data as its memory buffer, and resets the internal accumulator
        /// </summary>
        /// <returns>The buffer segment containing the accumulated response data</returns>
        public readonly Memory<byte> GetResponseData(int accumulatedSize) => _buffer.GetMemory()[..accumulatedSize];

        /// <summary>
        /// Initializes a new <see cref="ForwardOnlyWriter{T}"/> for buffering character header data
        /// </summary>
        /// <returns>A <see cref="ForwardOnlyWriter{T}"/> for buffering character header data</returns>
        public readonly ForwardOnlyWriter<char> GetWriter()
        {
            Span<char> chars = _buffer.GetCharSpan();
            return new ForwardOnlyWriter<char>(chars);
        }

        /// <summary>
        /// Encodes and writes the contents of the <see cref="ForwardOnlyWriter{T}"/> to the internal accumulator
        /// </summary>
        /// <param name="writer">The character buffer writer to commit data from</param>
        /// <param name="accumulatedSize">A reference to the cumulative number of bytes written to the buffer</param>
        public readonly void CommitChars(ref ForwardOnlyWriter<char> writer, ref int accumulatedSize)
        {
            if (writer.Written == 0)
            {
                return;
            }

            //Write the entire token to the buffer
            WriteToken(writer.AsSpan(), ref accumulatedSize);
        }

        /// <summary>
        /// Encodes a single token and writes it directly to the internal accumulator
        /// </summary>
        /// <param name="chars">The character sequence to accumulate</param>
        /// <param name="accumulatedSize">A reference to the cumulative number of bytes written to the buffer</param>
        public readonly void WriteToken(ReadOnlySpan<char> chars, ref int accumulatedSize)
        {
            //Get remaining buffer
            Span<byte> remaining = _buffer.GetBinSpan(accumulatedSize);

            //Commit all chars to the buffer
            accumulatedSize += _contextInfo.Encoding.GetBytes(chars, remaining);
        }

        /// <summary>
        /// Writes the http termination sequence to the internal accumulator
        /// </summary>
        public readonly void WriteTermination(ref int accumulatedSize)
            => accumulatedSize += _contextInfo.CrlfSegment.DangerousCopyTo(_buffer, accumulatedSize);

    }
}