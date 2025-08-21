/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http.Core.Buffering;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory;

namespace VNLib.Net.Http.Core.Request
{
    /// <summary>
    /// Specialized stream to allow reading a request entity body with a fixed content length.
    /// </summary>
    internal sealed class HttpInputStream(TransportManager transport) : Stream
    {
        private StreamState _state;

        private long Remaining
        {
            get
            {
                long remaining = _state.ContentLength - _state.Position;
                Debug.Assert(remaining >= 0, "Input stream overrun. Read more data than was available for the connection");
                return remaining;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnComplete()
        {
            _state = default;
        }

        /// <summary>
        /// Prepares the input stream for reading from the transport with the specified content length
        /// and initial data buffer
        /// </summary>
        /// <param name="contentLength">The number of bytes to allow being read from the transport or initial buffer</param>
        /// <param name="remainder">The initial buffer containing data already read from the transport</param>
        internal void Prepare(long contentLength, in TransportBufferRemainder remainder)
        {
            Debug.Assert(_state.Position == 0, "Stream state was not cleared before reuse");

            _state.ContentLength = contentLength;

            /*
             * It's possible the input stream has buffered more data than specified by content-length,
             * in this case clamp the buffer size to the content length to avoid reading more data 
             * than is allowed.
             * 
             * This may be an RFC to prove through formal verification it's imposible for this to occur,
             * but for now just clamp the size to the content length.
             */
            int maxRemainder = (int)Math.Clamp(remainder.Size, 0, contentLength);

            _state.Buffer = new(remainder.Buffer, remainder.Offset, maxRemainder);

            // Some sanity checks
            Debug.Assert(BufferRemaining(in _state) == _state.Buffer.Size, "Buffer remaining size does not match remainder size");
            Debug.Assert(remainder.Size == 0 || remainder.Buffer != null, "Buffer cannot be null if size is greater than zero");
        }

        /// <summary>
        /// Not a supported method. In debug mode will cause the application to crash with a 
        /// warning to determine the cause.
        /// </summary>
        public override void Close() => Debug.Fail("A attempt to close the HTTP input stream was made. The source should be determined.");

        /// <summary>
        /// Always returns true
        /// </summary>
        public override bool CanRead => true;
        
        /// <summary>
        /// Stream is not seekable, but is always true for 
        /// stream compatibility reasons 
        /// </summary>
        public override bool CanSeek => true;

        /// <summary>
        /// Stream is never writable
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// Gets the total size of the entity body (aka Content-Length)
        /// </summary>
        public override long Length => _state.ContentLength;
        
        /// <summary>
        /// Gets the number of bytes currently read from the entity body, setting the 
        /// position is a NOOP
        /// </summary>
        public override long Position { get => _state.Position; set { } }

        /// <summary>
        /// NOOP
        /// </summary>
        public override void Flush() { }

        ///<inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <summary>
        /// Reads as much entity body data as available into the specified buffer as possible
        /// until the buffer is full or the amount of entity data is reached. 
        /// </summary>
        /// <param name="buffer">The buffer to copy entity data to</param>
        /// <returns>The number of bytes read from the transport</returns>
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
            if (BufferRemaining(in _state) > 0)
            {
                //Read as much as possible from internal buffer
                int read = ReadBufferedData(ref _state, writer.Remaining);

                //Advance writer 
                writer.Advance(read);
            }

            //See if data is still remaining to be read from transport (reamining size is also the amount of data that can be read)
            if (writer.RemainingSize > 0)
            {
                int read = transport.Stream!.Read(writer.Remaining);

                writer.Advance(read);

                _state.Position += read;
            }

            //Return number of bytes written to the buffer
            return writer.Written;
        }        

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            /*
             * Iniitally I'm calculating the amount of data that can be read into 
             * the buffer, up to the maxium input data size. This value will clamp
             * the buffer in the writer below, so it cannot read more than is 
             * available from the transport.
             */
            int bytesToRead = (int)Math.Min(buffer.Length, Remaining);

            if (bytesToRead == 0)
            {
                return 0;
            }

            //Clamp output buffer size and create buffer writer
            ForwardOnlyMemoryWriter<byte> writer = new(buffer[..bytesToRead]);

            //See if all data is internally buffered
            if (BufferRemaining(in _state) > 0)
            {
                //Read as much as possible from internal buffer
                int read = ReadBufferedData(ref _state, writer.Remaining.Span);
               
                writer.Advance(read);
            }

            //See if data is still remaining to be read from transport (reamining size is also the amount of data that can be read)
            if (writer.RemainingSize > 0)
            {                
                int read = await transport.Stream.ReadAsync(writer.Remaining, cancellationToken)
                    .ConfigureAwait(true);
                
                writer.Advance(read);

                _state.Position += read;
            }
          
            return writer.Written;
        }

        /// <summary>
        /// Asynchronously discards all remaining data in the stream 
        /// </summary>
        /// <returns>A task that represents the discard operations</returns>
        public ValueTask DiscardRemainingAsync()
        {
            long remaining = Remaining;

            if (remaining == 0)
            {
                return ValueTask.CompletedTask;
            }

            //See if all data has already been buffered
            if(_initalData.HasValue && remaining <= _initalData.Value.Remaining)
            {
                //All data has been buffred, so just clear the buffer
                _state.Position = Length;
                return ValueTask.CompletedTask;
            }
            //We must actaully disacrd data from the stream
            else
            {
                return DiscardStreamDataAsync();
            }
        }

        private async ValueTask DiscardStreamDataAsync()
        {
            DiscardInternalBuffer();

            int read, bytesToRead = (int)Math.Min(HttpServer.WriteOnlyScratchBuffer.Length, Remaining);

            while (bytesToRead > 0)
            {
                //Read data to the discard buffer until reading is completed (read == 0)
                read = await transport.Stream!.ReadAsync(HttpServer.WriteOnlyScratchBuffer.Slice(0, bytesToRead), CancellationToken.None)
                    .ConfigureAwait(true);
               
                _state.Position += read;
               
                bytesToRead = (int)Math.Min(HttpServer.WriteOnlyScratchBuffer.Length, Remaining);
            }
        }

        private void DiscardInternalBuffer()
        {
            if (_initalData.HasValue)
            {
                //Update the stream position with remaining data
                _state.Position += _initalData.Value.DiscardRemaining();
            }
        }

        /// <summary>
        /// Seek is not a supported operation, a seek request is ignored and nothin happens
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public override long Seek(long offset, SeekOrigin origin) => _state.Position;

        ///<inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        ///<inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private static int BufferRemaining(ref readonly StreamState state)
        {
            /*
             * Using a single position pointer avoids possible desynchronization of positions
             * if there is data buffered, the position pointer can point into the buffer, until
             * it reaches the end of the buffer, at which point data is read from the transport.
             * 
             * The math.max guards for the case when the position points beyond the buffer
             */
            return (int)Math.Max(0, state.Buffer.Size - state.Position);
        }

        /// <summary>
        /// Updates the specified stream state to discard the internal buffer and update the position
        /// of the stream to the end of the buffer if any data is remaining in the buffer.
        /// </summary>
        /// <param name="state">A mutable reference to the stream state to update</param>
        private static void DiscardInternalBuffer(ref StreamState state)
        {
            if (BufferRemaining(in state) > 0)
            {
                // Point position to the end of the buffer
                state.Position = state.Buffer.Size;

                Debug.Assert(state.Position <= state.ContentLength, "Position should never exceed content length");
            }
        }

        /// <summary>
        /// Reads data from the internal buffer into the specified buffer, and 
        /// updates the supplied stream state to reflect the read data.
        /// </summary>
        /// <param name="buffer">The output buffer to write stored data to</param>
        /// <param name="state">A mutable reference to the stream state to update</param>
        /// <returns>The number of bytes written to the buffer</returns>
        private static int ReadBufferedData(ref StreamState state, Span<byte> buffer)
        {
            int bytesToRead = Math.Min(buffer.Length, BufferRemaining(in state));

            if (bytesToRead == 0)
            {
                return 0;
            }

            Debug.Assert(state.Buffer.Buffer != null, "Buffer should not be null when data is in buffer");

            //Read as much as possible from internal buffer
            MemoryUtil.Memmove(
                src: in state.Buffer.GetBufferRef(),
                srcOffset: (nuint)state.Position,
                dst: ref MemoryMarshal.GetReference(buffer),
                dstOffset: 0,
                elementCount: (nuint)bytesToRead
            );

            // Update position in the stream
            state.Position += bytesToRead;

            return bytesToRead;
        }

        private struct StreamState
        {
            public long Position;
            public long ContentLength;

            public InputBufferState Buffer;
        }

        private readonly struct InputBufferState(IHttpHeaderParseBuffer buffer, int offset, int size)
        {
            /*
             * A note on variables:
             * 
             * Offset: The internal offset into the buffer where the data segment starts, 
             *   this is transparent to the caller, its an optimization to avoid data 
             *   compaction operation.
             * Size: The total size of readable data in the buffer
             */

            public readonly int Size = size;
            public readonly IHttpHeaderParseBuffer Buffer = buffer;

            /// <summary>
            /// Gets the start position of the data segment in the buffer 
            /// which takes the offset into account
            /// </summary>
            /// <returns>
            /// A reference to the start of the data segment in the buffer offset by 
            /// the current position
            /// </returns>
            public readonly ref readonly byte GetBufferRef()
                => ref Buffer.DangerousGetBinRef(offset);
        }
    }
}