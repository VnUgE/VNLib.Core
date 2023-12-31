/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: ReusableNetworkStream.cs 
*
* ReusableNetworkStream.cs is part of VNLib.Net.Transport.SimpleTCP which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.SimpleTCP is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.SimpleTCP is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

/*
 * A special stream that sits betnween the socket/pipeline listener
 * that marshals data between the application and the socket pipeline.
 * This stream uses a timer to cancel recv events. Because of this and 
 * pipeline aspects, it supports full duplex IO but it is not thread safe.
 * 
 * IE one thread can read and write, but not more
 */


using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;

namespace VNLib.Net.Transport.Tcp
{

    /// <summary>
    /// A reusable stream that marshals data between the socket pipeline and the application
    /// </summary>
    internal sealed class ReusableNetworkStream : Stream
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

        // Write timeout is not currently used, becasue the writer managed socket timeouts
        public override int WriteTimeout
        {
            get => _sendTimeoutMs;
            //Allow -1 to set to infinite timeout
            set => _sendTimeoutMs = value > -2 ? value : throw new ArgumentException("Write timeout must be a 32bit signed integer larger than -1");
        }

        //Timer used to cancel pipeline recv timeouts
        private readonly ITransportInterface Transport;
      
        internal ReusableNetworkStream(ITransportInterface transport)
        {
            Transport = transport;
        }

        ///<inheritdoc/>
        public override void Close() 
        { }

        ///<inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        ///<inheritdoc/>
        public override void Flush() 
        { }

        ///<inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        ///<inheritdoc/>
        public override int Read(Span<byte> buffer) => Transport.Recv(buffer, _recvTimeoutMs);

        ///<inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
          //Since read returns a value, it isnt any cheaper not to alloc a task around the value-task
          => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        ///<inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) 
            => Transport.RecvAsync(buffer, _recvTimeoutMs, cancellationToken);

        ///<inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        ///<inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer) => Transport.Send(buffer, _sendTimeoutMs);

        ///<inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) 
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        ///<inheritdoc/>
        ///<exception cref="IOException"></exception>
        ///<exception cref="ObjectDisposedException"></exception>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default) 
            => Transport.SendAsync(buffer, _sendTimeoutMs, cancellation);

        /*
         * Override dispose to intercept base cleanup until the internal release
         */

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}