/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ChunkedStream.cs 
*
* ChunkedStream.cs is part of VNLib.Net.Http which is part of the larger 
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
* Provides a Chunked data-encoding stream for writing data-chunks to 
* the transport using the basic chunked encoding format from MDN
* https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Transfer-Encoding#directives
* 
* This stream will buffer entire chunks to avoid multiple writes to the 
* transport which can block or at minium cause overhead in context switching
* which should be mostly avoided but cause overhead in copying. Time profiling
* showed nearly equivalent performance for small chunks for synchronous writes.
* 
*/

using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core.Response
{

#pragma warning disable CA2215 // Dispose methods should call base class dispose
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

    /// <summary>
    /// Writes chunked HTTP message bodies to an underlying streamwriter 
    /// </summary>
    internal sealed class ChunkedStream : ReusableResponseStream
    {
        private readonly ChunkDataAccumulator ChunckAccumulator;
        
        private bool HadError;

        internal ChunkedStream(IChunkAccumulatorBuffer buffer, IHttpContextInformation context)
        {
            //Init accumulator
            ChunckAccumulator = new(buffer, context);
        }
       
        public override void Write(ReadOnlySpan<byte> chunk)
        {
            //Only write non-zero chunks
            if (chunk.Length <= 0)
            {
                return;
            }

            //Init reader
            ForwardOnlyReader<byte> reader = new(chunk);
            try
            {
                do
                {
                    //try to accumulate the chunk data
                    ERRNO written = ChunckAccumulator.TryBufferChunk(reader.Window);

                    //Not all data was buffered
                    if (written < reader.WindowSize)
                    {
                        //Advance reader
                        reader.Advance(written);

                        //Flush accumulator
                        Memory<byte> accChunk = ChunckAccumulator.GetChunkData();

                        //Reset the chunk accumulator
                        ChunckAccumulator.Reset();

                        //Write chunk data
                        transport!.Write(accChunk.Span);

                        //Continue to buffer / flush as needed
                        continue;
                    }
                    break;
                }
                while (true);
            }
            catch
            {
                HadError = true;
                throw;
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken = default)
        {
            //Only write non-zero chunks
            if (chunk.Length <= 0)
            {
                return;
            }

            try
            {
                //Init reader
                ForwardOnlyMemoryReader<byte> reader = new(chunk);

                do
                {
                    //try to accumulate the chunk data
                    ERRNO written = ChunckAccumulator.TryBufferChunk(reader.Window.Span);

                    //Not all data was buffered
                    if (written < reader.WindowSize)
                    {
                        //Advance reader
                        reader.Advance(written);

                        //Flush accumulator
                        Memory<byte> accChunk = ChunckAccumulator.GetChunkData();

                        //Reset the chunk accumulator
                        ChunckAccumulator.Reset();

                        //Flush accumulator async
                        await transport!.WriteAsync(accChunk, cancellationToken);

                        //Continue to buffer / flush as needed
                        continue;
                    }

                    break;
                }
                while (true);
            }
            catch
            {
                HadError = true;
                throw;
            }
        }

        public override async ValueTask DisposeAsync()
        {
            //If write error occured, then do not write the last chunk
            if (HadError)
            {
                return;
            }

            //Complete the last chunk
            Memory<byte> chunkData = ChunckAccumulator.GetFinalChunkData();

            //Reset the accumulator
            ChunckAccumulator.Reset();

            //Write remaining data to stream
            await transport!.WriteAsync(chunkData, CancellationToken.None);

            //Flush base stream
            await transport!.FlushAsync(CancellationToken.None);
        }

        public override void Close()
        {
            //If write error occured, then do not write the last chunk
            if (HadError)
            {
                return;
            }

            //Complete the last chunk
            Memory<byte> chunkData = ChunckAccumulator.GetFinalChunkData();

            //Reset the accumulator
            ChunckAccumulator.Reset();

            //Write chunk data
            transport!.Write(chunkData.Span);

            //Flush base stream
            transport!.Flush();
        }

        #region Hooks

        public void OnNewRequest()
        {
            ChunckAccumulator.OnNewRequest();
        }

        public void OnComplete()
        {
            ChunckAccumulator.OnComplete();

            //Clear error flag
            HadError = false;
        }

        #endregion
    }
}