/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: ReusableNetworkStream.cs 
*
* ReusableNetworkStream.cs is part of VNLib.Net.Transport.Tcp which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.Tcp is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.Tcp is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

/*
 * A special stream that sits between the socket/pipeline listener
 * that marshals data between the application and the socket pipeline.
 * This stream uses a timer to cancel recv events. Because of this and 
 * pipeline aspects, it supports full duplex IO but it is not thread safe.
 * 
 * IE one thread can read and write, but not more
 */


using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;

namespace VNLib.Net.Transport.Tcp.Internal
{

    /// <summary>
    /// A reusable stream that marshals data between the socket pipeline and the application
    /// </summary>
    internal sealed class ReusableNetworkStream : Stream, IBufferWriter<byte>
    {
        #region stream basics
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override bool CanTimeout => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotImplementedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => throw new NotSupportedException("CopyToAsync is not supported");

        public override void CopyTo(Stream destination, int bufferSize) => throw new NotSupportedException("CopyTo is not supported");
        #endregion

        private int _recvTimeoutMs;
        private int _sendTimeoutMs;

        //Read timeout to use when receiving data
        public override int ReadTimeout
        {
            get => _recvTimeoutMs;
            //Allow -1 to set to infinite timeout
            set => _recvTimeoutMs = value > -2 ? value : throw new ArgumentException("Write timeout must be a 32bit signed integer larger than 0");
        }

        // Write timeout is not currently used, because the writer managed socket timeouts
        public override int WriteTimeout
        {
            get => _sendTimeoutMs;
            //Allow -1 to set to infinite timeout
            set => _sendTimeoutMs = value > -2 ? value : throw new ArgumentException("Write timeout must be a 32bit signed integer larger than -1");
        }

        //Timer used to cancel pipeline recv timeouts
        private readonly ITransportInterface _transport;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReusableNetworkStream"/> class.
        /// </summary>
        /// <param name="transport">The transport interface that backs the stream's send and receive operations.</param>
        internal ReusableNetworkStream(ITransportInterface transport) => _transport = transport;


        /*
         * Close now completes the pipeline to signal to the transport that the consumer is 
         * no longer transferring data.
         */
        ///<inheritdoc/>
        public override void Close() => _transport.Close();

        ///<inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) 
            => _transport.FlushSendAsync(_sendTimeoutMs, cancellationToken).AsTask();


        /*
         * Expose the buffer writer interface on the stream 
         * for more efficient publishing
         */

        ///<inheritdoc/>
        public void Advance(int count)
            => _transport.SendBuffer.Advance(count);

        ///<inheritdoc/>
        public Memory<byte> GetMemory(int sizeHint = 0)
            => _transport.SendBuffer.GetMemory(sizeHint);

        ///<inheritdoc/>
        public Span<byte> GetSpan(int sizeHint = 0)
            => _transport.SendBuffer.GetSpan(sizeHint);

        ///<inheritdoc/>
        public override void Flush() 
        { }

        ///<inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        ///<inheritdoc/>
        public override int Read(Span<byte> buffer) => _transport.Recv(buffer, _recvTimeoutMs);

        ///<inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
          //Since read returns a value, it isnt any cheaper not to alloc a task around the value-task
          => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        ///<inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) 
            => _transport.RecvAsync(buffer, _recvTimeoutMs, cancellationToken);

        ///<inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        ///<inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer) => _transport.Send(buffer, _sendTimeoutMs);

        ///<inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) 
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        ///<inheritdoc/>
        ///<exception cref="IOException"></exception>
        ///<exception cref="ObjectDisposedException"></exception>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default) 
            => _transport.SendAsync(buffer, _sendTimeoutMs, cancellation);

        /*
         * Override dispose to intercept base cleanup until the internal release
         * 
         * 4-24-2026:
         * Manually call close and return a default value task. This avoids the base 
         * stream logic and gc "dispose" logic. Still a hack becaus this stream is designed to be 
         * reused.
         */

        public override ValueTask DisposeAsync()
        {
            //Call close to trigger transport shutdown
            Close();
            return default;
        }

    }
}
