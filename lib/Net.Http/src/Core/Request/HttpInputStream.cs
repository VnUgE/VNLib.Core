/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpInputStream.cs 
*
* HttpInputStream.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.IO;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http.Core
{
    /// <summary>
    /// Specialized stream to allow reading a request entity body with a fixed content length.
    /// </summary>
    internal sealed class HttpInputStream : Stream
    {
        private readonly Func<Stream> GetTransport;

        private long ContentLength;
        private Stream? InputStream;
        private long _position;

        private InitDataBuffer? _initalData;     

        public HttpInputStream(Func<Stream> getTransport) => GetTransport = getTransport;

        internal void OnComplete()
        {
            //Dispose the initial data buffer if set
            if (_initalData.HasValue)
            {
                _initalData.Value.Release();
                _initalData = null;
            }

            //Remove stream cache copy
            InputStream = null;
            //Reset position
            _position = 0;
            //reset content length
            ContentLength = 0;
        }

        /// <summary>
        /// Creates a new input stream object configured to allow reading of the specified content length
        /// bytes from the stream and consumes the initial buffer to read data from on initial read calls
        /// </summary>
        /// <param name="contentLength">The number of bytes to allow being read from the transport or initial buffer</param>
        /// <param name="initial">Entity body data captured on initial read</param>
        internal void Prepare(long contentLength, in InitDataBuffer? initial)
        {
            ContentLength = contentLength;
            _initalData = initial;
            
            //Cache transport
            InputStream = GetTransport();
        }

        public override void Close() => throw new NotSupportedException("The HTTP input stream should never be closed!");
        private long Remaining => Math.Max(ContentLength - _position, 0);
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => ContentLength;
        public override long Position { get => _position; set { } }

        public override void Flush(){}

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            //Calculate the amount of data that can be read into the buffer
            int bytesToRead = (int)Math.Min(buffer.Length, Remaining);
            if (bytesToRead == 0)
            {
                return 0;
            }

            //Clamp output buffer size and create buffer writer
            ForwardOnlyWriter<byte> writer = new(buffer[..bytesToRead]);

            //See if all data is internally buffered
            if (_initalData.HasValue && _initalData.Value.Remaining > 0)
            {
                //Read as much as possible from internal buffer
                ERRNO read = _initalData.Value.Read(writer.Remaining);

                //Advance writer 
                writer.Advance(read);

                //Update position
                _position += read;
            }

            //See if data is still remaining to be read from transport (reamining size is also the amount of data that can be read)
            if (writer.RemainingSize > 0)
            {
                //Read from transport
                ERRNO read = InputStream!.Read(writer.Remaining);

                //Update writer position
                writer.Advance(read);

                _position += read;
            }

            //Return number of bytes written to the buffer
            return writer.Written;
        }       

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            //Calculate the amount of data that can be read into the buffer
            int bytesToRead = (int)Math.Min(buffer.Length, Remaining);
            if (bytesToRead == 0)
            {
                return 0;
            }

            //Clamp output buffer size and create buffer writer
            ForwardOnlyMemoryWriter<byte> writer = new(buffer[..bytesToRead]);

            //See if all data is internally buffered
            if (_initalData.HasValue && _initalData.Value.Remaining > 0)
            {
                //Read as much as possible from internal buffer
                ERRNO read = _initalData.Value.Read(writer.Remaining.Span);

                //Advance writer 
                writer.Advance(read);

                //Update position
                _position += read;
            }

            //See if data is still remaining to be read from transport (reamining size is also the amount of data that can be read)
            if (writer.RemainingSize > 0)
            {
                //Read from transport
                ERRNO read = await InputStream!.ReadAsync(writer.Remaining, cancellationToken).ConfigureAwait(false);

                //Update writer position
                writer.Advance(read);

                _position += read;
            }

            //Return number of bytes written to the buffer
            return writer.Written;
        }

        /// <summary>
        /// Asynchronously discards all remaining data in the stream 
        /// </summary>
        /// <param name="maxBufferSize">The maxium size of the buffer to allocate</param>
        /// <returns>A task that represents the discard operations</returns>
        public async ValueTask DiscardRemainingAsync(int maxBufferSize)
        {
            long remaining = Remaining;

            if(remaining == 0)
            {
                return;
            }

            //See if all data has already been buffered
            if(_initalData.HasValue && remaining <= _initalData.Value.Remaining)
            {
                //All data has been buffred, so just clear the buffer
                _position = Length;
            }
            //We must actaully disacrd data from the stream
            else
            {
                //Calcuate a buffer size to allocate (will never be larger than an int)
                int bufferSize = (int)Math.Min(remaining, maxBufferSize);
                //Alloc a discard buffer to reset the transport 
                using IMemoryOwner<byte> discardBuffer = CoreBufferHelpers.GetMemory(bufferSize, false);
                int read = 0;
                do
                {
                    //Read data to the discard buffer until reading is completed
                    read = await ReadAsync(discardBuffer.Memory, CancellationToken.None).ConfigureAwait(true);

                } while (read != 0);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            //Ignore seek control
            return _position;
        }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}