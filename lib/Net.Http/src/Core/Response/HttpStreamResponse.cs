/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpStreamResponse.cs 
*
* HttpStreamResponse.cs is part of VNLib.Net.Http which is part 
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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Net.Http.Core.Response
{
    internal readonly struct HttpStreamResponse(Stream stream) : IHttpStreamResponse
    {
        ///<inheritdoc/>
        public readonly void Dispose() => stream.Dispose();

        ///<inheritdoc/>
        public readonly ValueTask DisposeAsync() => stream.DisposeAsync();

        ///<inheritdoc/>
        public readonly ValueTask<int> ReadAsync(Memory<byte> buffer) => stream!.ReadAsync(buffer, CancellationToken.None);
    }
}