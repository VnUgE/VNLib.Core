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
*/

using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core.Response
{

    /// <summary>
    /// Writes chunked HTTP message bodies to an underlying streamwriter 
    /// </summary>
    internal sealed class ChunkedStream : ReusableResponseStream, IResponseDataWriter
    {
        private readonly ChunkDataAccumulator ChunckAccumulator;

        internal ChunkedStream(IChunkAccumulatorBuffer buffer, IHttpContextInformation context)
        {
            //Init accumulator
            ChunckAccumulator = new(buffer, context);
        }

        #region Hooks

        ///<inheritdoc/>
        public void OnNewRequest()
        {
            ChunckAccumulator.OnNewRequest();
        }

        ///<inheritdoc/>
        public void OnComplete()
        {
            ChunckAccumulator.OnComplete();
        }

        ///<inheritdoc/>
        public Memory<byte> GetMemory() => ChunckAccumulator.GetRemainingSegment();

        ///<inheritdoc/>
        public int Advance(int written)
        {
            //Advance the accumulator
            ChunckAccumulator.Advance(written);
            return ChunckAccumulator.GetRemainingSegmentSize();
        }

        ///<inheritdoc/>
        public ValueTask FlushAsync(bool isFinal)
        {
            /*
             * We need to know when the final chunk is being flushed so we can
             * write the final termination sequence to the transport.
             */
            
            Memory<byte> chunkData = isFinal ? ChunckAccumulator.GetFinalChunkData() : ChunckAccumulator.GetChunkData();

            //Reset the accumulator
            ChunckAccumulator.Reset();

            //Write remaining data to stream
            return transport!.WriteAsync(chunkData, CancellationToken.None);
        }

        #endregion
    }
}