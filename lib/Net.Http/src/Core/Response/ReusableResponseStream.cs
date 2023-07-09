/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ReusableResponseStream.cs 
*
* ReusableResponseStream.cs is part of VNLib.Net.Http which is part 
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

#pragma warning disable CA2215 // Dispose methods should call base class dispose
#pragma warning disable CA1844 // Provide memory-based overrides of async methods when subclassing 'Stream'

    internal abstract class ReusableResponseStream : Stream
    {
        protected Stream? transport;

        /// <summary>
        /// Called when a new connection is established
        /// </summary>
        /// <param name="transport"></param>
        public virtual void OnNewConnection(Stream transport) => this.transport = transport;

        /// <summary>
        /// Called when the connection is released
        /// </summary>
        public virtual void OnRelease() => this.transport = null;


        //Block base dispose
        protected override void Dispose(bool disposing)
        { }

        //Block base close
        public override void Close()
        { }

        //block base dispose async
        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        //Block flush
        public override void Flush()
        { }

        //Block flush async
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        //Block stream basics 
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        //Reading is not enabled
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("This stream cannot be read from");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("This stream does not support seeking");
        public override void SetLength(long value) => throw new NotSupportedException("This stream does not support seeking");

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }
    }
}