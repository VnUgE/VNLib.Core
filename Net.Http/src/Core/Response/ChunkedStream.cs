/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Utils.Memory;

namespace VNLib.Net.Http.Core
{

    internal partial class HttpResponse
    {
        /// <summary>
        /// Writes chunked HTTP message bodies to an underlying streamwriter 
        /// </summary>
        private class ChunkedStream : Stream, IHttpLifeCycle
        {
            private const string LAST_CHUNK_STRING = "0\r\n\r\n";

            private readonly ReadOnlyMemory<byte> LastChunk;            
            private readonly ChunkDataAccumulator ChunckAccumulator;
            private readonly Func<Stream> GetTransport; 

            private Stream? TransportStream;
            private bool HadError;

            internal ChunkedStream(Encoding encoding, int chunkBufferSize, Func<Stream> getStream)
            {
                //Convert and store cached versions of the last chunk bytes
                LastChunk = encoding.GetBytes(LAST_CHUNK_STRING);

                //get the min buffer by rounding to the nearest page
                int actualBufSize = (chunkBufferSize / 4096 + 1) * 4096;

                //Init accumulator
                ChunckAccumulator = new(encoding, actualBufSize);

                GetTransport = getStream;
            }
           

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("This stream cannot be read from");
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("This stream does not support seeking");
            public override void SetLength(long value) => throw new NotSupportedException("This stream does not support seeking");
            public override void Flush() { }
            public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
           
            
            public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));
            public override void Write(ReadOnlySpan<byte> chunk)
            {
                //Only write non-zero chunks
                if (chunk.Length <= 0)
                {
                    return;
                }

                //Init reader
                ForwardOnlyReader<byte> reader = new(in chunk);
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
                            ChunckAccumulator.Flush(TransportStream!);
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
         

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
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
                    ForwardOnlyMemoryReader<byte> reader = new(in chunk);

                    do
                    {
                        //try to accumulate the chunk data
                        ERRNO written = ChunckAccumulator.TryBufferChunk(reader.Window.Span);

                        //Not all data was buffered
                        if (written < reader.WindowSize)
                        {
                            //Advance reader
                            reader.Advance(written);

                            //Flush accumulator async
                            await ChunckAccumulator.FlushAsync(TransportStream!, cancellationToken);
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
                
                //Write remaining data to stream
                await ChunckAccumulator.FlushAsync(TransportStream!, CancellationToken.None);

                //Write final chunk
                await TransportStream!.WriteAsync(LastChunk, CancellationToken.None);

                //Flush base stream
                await TransportStream!.FlushAsync(CancellationToken.None);
            }

            protected override void Dispose(bool disposing) => Close();

            public override void Close()
            {
                //If write error occured, then do not write the last chunk
                if (HadError)
                {
                    return;
                }
                
                //Write remaining data to stream
                ChunckAccumulator.Flush(TransportStream!);

                //Write final chunk
                TransportStream!.Write(LastChunk.Span);

                //Flush base stream
                TransportStream!.Flush();
            }
            
                
            #region Hooks

            public void OnPrepare()
            {
                ChunckAccumulator.OnPrepare();
            }

            public void OnRelease()
            {
                ChunckAccumulator.OnRelease();
            }

            public void OnNewRequest()
            {
                ChunckAccumulator.OnNewRequest();
                
                //Get transport stream even if not used
                TransportStream = GetTransport();
            }

            public void OnComplete()
            {
                ChunckAccumulator.OnComplete();
                TransportStream = null;
                
                //Clear error flag
                HadError = false;
            }
            
            #endregion
        }
    }
}