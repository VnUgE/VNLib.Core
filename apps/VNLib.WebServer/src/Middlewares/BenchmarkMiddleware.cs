/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: BenchmarkMiddleware.cs 
*
* BenchmarkMiddleware.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/


using System;
using System.Net;
using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.Cryptography;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Net.Http;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Middleware;

using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer.Middlewares
{
    /*
     * This is a cheatsy little syntethic benchmark middleware that will 
     * return a fixed size memory response for every request to simulate 
     * a file response for synthetic benchmarking.
     * 
     * The buffer may optionally be filled with random data to put a 
     * load on the compressor instead of a zero filled buffer
     */

    internal sealed class BenchmarkMiddleware(BenchmarkConfig config) : IHttpMiddleware
    {
        private readonly MemoryManager<byte> data = AllocBuffer(config.Size, config.Random);

        public ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity)
        {
            entity.CloseResponse(
                HttpStatusCode.OK,
                ContentType.Binary,
                new BenchmarkResponseData(data.Memory)
            );

            return ValueTask.FromResult(FileProcessArgs.VirtualSkip);
        }

        private static MemoryManager<byte> AllocBuffer(int size, bool random)
        {
            /*
             * Even though this is testing, the buffer is zeroed to avoid leaking 
             * any data that may be in heap memory after allocation.
             */
            MemoryManager<byte> man = MemoryUtil.Shared.DirectAlloc<byte>(size, true);

            if (random)
            {
                RandomNumberGenerator.Fill(man.GetSpan());
            }

            return man;
        }

        private sealed class BenchmarkResponseData(ReadOnlyMemory<byte> data) : IMemoryResponseReader
        {
            int read;
            readonly int len = data.Length;

            public int Remaining => len - read;

            public void Advance(int written)
            {
                read += written;
                Debug.Assert(Remaining >= 0);
            }

            public void Close()
            { }

            public ReadOnlyMemory<byte> GetMemory() => data.Slice(read, Remaining);
        }
    }
}