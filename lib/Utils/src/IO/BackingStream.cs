/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: BackingStream.cs 
*
* BackingStream.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Provides basic stream support sync/async stream operations to a 
    /// backing stream with virtual event methods. Provides a pass-through 
    /// as best as possbile. 
    /// </summary>
    public abstract class BackingStream<T> : Stream where T: Stream
    {
        /// <summary>
        /// The backing/underlying stream operations are being performed on
        /// </summary>
        protected abstract T BaseStream { get; }

        /// <summary>
        /// A value that will cause all calls to write to throw <see cref="NotSupportedException"/>
        /// </summary>
        protected bool ForceReadOnly { get; set; }

        ///<inheritdoc/>
        public override bool CanRead => BaseStream.CanRead;

        ///<inheritdoc/>
        public override bool CanSeek => BaseStream.CanSeek;

        ///<inheritdoc/>
        public override bool CanWrite => BaseStream.CanWrite && !ForceReadOnly;

        ///<inheritdoc/>
        public override long Length => BaseStream.Length;

        ///<inheritdoc/>
        public override int WriteTimeout { get => BaseStream.WriteTimeout; set => BaseStream.WriteTimeout = value; }

        ///<inheritdoc/>
        public override int ReadTimeout { get => BaseStream.ReadTimeout; set => BaseStream.ReadTimeout = value; }

        ///<inheritdoc/>
        public override long Position { get => BaseStream.Position; set => BaseStream.Position = value; }

        ///<inheritdoc/>
        public override void Flush()
        {
            BaseStream.Flush();
            OnFlush();
        }

        ///<inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => BaseStream.Read(buffer, offset, count);

        ///<inheritdoc/>
        public override int Read(Span<byte> buffer) => BaseStream.Read(buffer);

        ///<inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => BaseStream.Seek(offset, origin);

        ///<inheritdoc/>
        public override void SetLength(long value) => BaseStream.SetLength(value);

        ///<inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) 
        {
            ThrowIfReadonly();

            BaseStream.Write(buffer, offset, count);
            
            //Call onwrite function
            OnWrite(count);
        }

        ///<inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfReadonly();

            BaseStream.Write(buffer);
            
            //Call onwrite function
            OnWrite(buffer.Length);
        }

        ///<inheritdoc/>
        public override void Close()
        {
            BaseStream.Close();
            
            //Call on close function
            OnClose();
        }

        /// <summary>
        /// Raised directly after the base stream is closed, when a call to close is made
        /// </summary>
        protected virtual void OnClose() { }
        
        /// <summary>
        /// Raised directly after the base stream is flushed, when a call to flush is made
        /// </summary>
        protected virtual void OnFlush() { }
        
        /// <summary>
        /// Raised directly after a successful write operation.
        /// </summary>
        /// <param name="count">The number of bytes written to the stream</param>
        protected virtual void OnWrite(int count) { }

        ///<inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) 
            => BaseStream.ReadAsync(buffer, offset, count, cancellationToken);

        ///<inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) 
            => BaseStream.ReadAsync(buffer, cancellationToken);

        ///<inheritdoc/>
        public override void CopyTo(Stream destination, int bufferSize) 
            => BaseStream.CopyTo(destination, bufferSize);

        ///<inheritdoc/>
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) 
            => BaseStream.CopyToAsync(destination, bufferSize, cancellationToken);
        
        ///<inheritdoc/>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfReadonly();

            //We want to maintain pass through as much as possible, so supress warning
#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'
            await BaseStream.WriteAsync(buffer, offset, count, cancellationToken);
#pragma warning restore CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'
            
            //Call on-write and pass the number of bytes written
            OnWrite(count);
        }
        
        ///<inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfReadonly();

            await BaseStream.WriteAsync(buffer, cancellationToken);

            //Call on-write and pass the length
            OnWrite(buffer.Length);
        }

        ///<inheritdoc/>
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await BaseStream.FlushAsync(cancellationToken);
        
            //Call onflush 
            OnFlush();
        }

        ///<inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            //Dispose the base stream and await it
            await BaseStream.DisposeAsync();
            
            //Call onclose
            OnClose();
            
            //Suppress finalize
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Throws a <see cref="NotSupportedException"/> if the stream is set to readonly mode  .
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        protected void ThrowIfReadonly()
        {
            if (ForceReadOnly)
            {
                throw new NotSupportedException("Stream is set to readonly mode");
            }
        }
    }
}
