/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ResponseWriter.cs 
*
* ResponseWriter.cs is part of VNLib.Net.Http which is part of the larger 
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

/*
 * This file handles response entity processing. It handles in-memory response
 * processing, as well as stream response processing. It handles constraints
 * such as content-range limits. I tried to eliminate or reduce the amount of
 * memory copying required to process the response entity.
 */

using System.IO;

namespace VNLib.Net.Http.Core.Response
{
    internal readonly struct ResponsBodyDataState
    {
        /// <summary>
        /// A value that inidcates if the response entity has been set
        /// </summary>
        public readonly bool IsSet;
        /// <summary>
        /// A value that indicates if the response entity requires buffering
        /// </summary>
        public readonly bool BufferRequired;
        /// <summary>
        /// The length (in bytes) of the response entity
        /// </summary>
        public readonly long Legnth;

        public readonly IHttpStreamResponse? Stream;
        public readonly IMemoryResponseReader? MemResponse;
        public readonly Stream? RawStream;

        private ResponsBodyDataState(IHttpStreamResponse stream, long length)
        {
            Legnth = length;
            Stream = stream;
            MemResponse = null;
            RawStream = null;
            IsSet = true;
            BufferRequired = true;
        }

        private ResponsBodyDataState(IMemoryResponseReader reader)
        {
            Legnth = reader.Remaining;
            MemResponse = reader;
            Stream = null;
            RawStream = null;
            IsSet = true;
            BufferRequired = false;
        }

        private ResponsBodyDataState(Stream stream, long length)
        {
            Legnth = length;
            Stream = null;
            MemResponse = null;
            RawStream = stream;
            IsSet = true;
            BufferRequired = true;
        }

        internal readonly HttpStreamResponse GetRawStreamResponse() => new(RawStream!);

        internal readonly void Dispose()
        {
            Stream?.Dispose();
            MemResponse?.Close();
            RawStream?.Dispose();
        }

        public static ResponsBodyDataState FromMemory(IMemoryResponseReader stream) => new(stream);

        public static ResponsBodyDataState FromStream(IHttpStreamResponse stream, long length) => new(stream, length);

        public static ResponsBodyDataState FromRawStream(Stream stream, long length) => new(stream, length);
    }
}