/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: WriteOnlyBufferedStream.cs 
*
* WriteOnlyBufferedStream.cs is part of VNLib.Utils which is part of the larger 
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
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Memory;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// A basic accumulator style write buffered stream
    /// </summary>
    public class WriteOnlyBufferedStream : Stream
    {
        private readonly ISlindingWindowBuffer<byte> _buffer;
        private readonly bool LeaveOpen;

        /// <summary>
        /// Gets the underlying stream that interfaces with the backing store
        /// </summary>
        public Stream BaseStream { get; init; }

        /// <summary>
        /// Initalizes a new <see cref="WriteOnlyBufferedStream"/> using the 
        /// specified backing stream, using the specified buffer size, and 
        /// optionally leaves the stream open
        /// </summary>
        /// <param name="baseStream">The backing stream to write buffered data to</param>
        /// <param name="bufferSize">The size of the internal buffer</param>
        /// <param name="leaveOpen">A value indicating of the stream should be left open when the buffered stream is closed</param>
        public WriteOnlyBufferedStream(Stream baseStream, int bufferSize, bool leaveOpen = false)
        {
            BaseStream = baseStream;
            //Create buffer 
            _buffer = InitializeBuffer(bufferSize);
            LeaveOpen = leaveOpen;
        }
        /// <summary>
        /// Invoked by the constuctor method to allocte the internal buffer with the specified buffer size.
        /// </summary>
        /// <param name="bufferSize">The requested size of the buffer to alloc</param>
        /// <remarks>By default requests the buffer from the <see cref="ArrayPool{T}.Shared"/> instance</remarks>
        protected virtual ISlindingWindowBuffer<byte> InitializeBuffer(int bufferSize)
        {
            return new ArrayPoolStreamBuffer<byte>(ArrayPool<byte>.Shared, bufferSize);
        }

        ///<inheritdoc/>
        public override void Close()
        {
            try
            {
                //Make sure the buffer is empty
                WriteBuffer();

                if (!LeaveOpen)
                {
                    //Dispose stream
                    BaseStream.Dispose();
                }
            }
            finally
            {
                _buffer.Close();
            }
        }
        ///<inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            try
            {
                if (_buffer.AccumulatedSize > 0)
                {
                    await WriteBufferAsync(CancellationToken.None);
                }

                if (!LeaveOpen)
                {
                    //Dispose stream
                    await BaseStream.DisposeAsync();
                }

                GC.SuppressFinalize(this);
            }
            finally
            {
                _buffer.Close();
            }
        }
        
        ///<inheritdoc/>
        public override void Flush() => WriteBuffer();
        ///<inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => WriteBufferAsync(cancellationToken).AsTask();
        
        private void WriteBuffer()
        {
            //Only if data is available to write
            if (_buffer.AccumulatedSize > 0)
            {
                //Write data to stream
                BaseStream.Write(_buffer.Accumulated);
                //Reset position
                _buffer.Reset();
            }
        }

        private async ValueTask WriteBufferAsync(CancellationToken token = default)
        {
            if(_buffer.AccumulatedSize > 0)
            {
                await BaseStream.WriteAsync(_buffer.AccumulatedBuffer, token);
                _buffer.Reset();
            }
        }
        ///<inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
      
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ForwardOnlyReader<byte> reader = new(buffer);
            //Attempt to buffer/flush data until all data is sent
            do
            {
                //Try to buffer as much as possible
                ERRNO buffered = _buffer.TryAccumulate(reader.Window);
                
                if(buffered < reader.WindowSize)
                {
                    //Buffer is full and needs to be flushed
                    WriteBuffer();
                    //Advance reader and continue to buffer
                    reader.Advance(buffered);
                    continue;
                }

                break;
            }
            while (true);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ForwardOnlyMemoryReader<byte> reader = new(buffer);
            //Attempt to buffer/flush data until all data is sent
            do
            {
                //Try to buffer as much as possible
                ERRNO buffered = _buffer.TryAccumulate(reader.Window.Span);

                if (buffered < reader.WindowSize)
                {
                    //Buffer is full and needs to be flushed
                    await WriteBufferAsync(cancellationToken);
                    //Advance reader and continue to buffer
                    reader.Advance(buffered);
                    continue;
                }

                break;
            }
            while (true);
        }


        /// <summary>
        /// Always false
        /// </summary>
        public override bool CanRead => false;
        /// <summary>
        /// Always returns false
        /// </summary>
        public override bool CanSeek => false;
        /// <summary>
        /// Always true
        /// </summary>
        public override bool CanWrite => true;
        /// <summary>
        /// Returns the size of the underlying buffer
        /// </summary>
        public override long Length => _buffer.AccumulatedSize;
        /// <summary>
        /// Always throws <see cref="NotSupportedException"/>
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <summary>
        /// Always throws <see cref="NotSupportedException"/>
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("This stream is not readable");
        }

        /// <summary>
        /// Always throws <see cref="NotSupportedException"/>
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Always throws <see cref="NotSupportedException"/>
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
