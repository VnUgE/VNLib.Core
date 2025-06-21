/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMReusableRequestBuffer.cs 
*
* FBMReusableRequestBuffer.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Client
{
    internal sealed class FBMReusableRequestBuffer(IFBMMemoryManager manager, int bufferSize) : 
        Stream,
        IDataAccumulator<byte>,
        IBufferWriter<byte>,
        IFBMHeaderBuffer,
        IReusable        
    {       
        
        private readonly int _length = bufferSize;
        private int _written;
        private bool _allocated;

        #region Buffer management

        private readonly IFBMMemoryHandle _handle = manager.InitHandle();

        ///<inheritdoc/>
        public void Prepare()
        {
            manager.AllocBuffer(_handle, _length);
            _allocated = true;
        }

        ///<inheritdoc/>
        public bool Release()
        {
            // Free buffer back to pool
            if (_allocated)
            {
                manager.FreeBuffer(_handle);
                _allocated = false;
            }

            return true;
        }

        #endregion     

        /// <summary>
        /// Gets the internal memory handle that contains the written data.
        /// </summary>
        /// <returns>A memory wrapper around the data written to the internal buffer</returns>
        internal Memory<byte> GetWrittenMemory()
            => _handle.GetMemory()[.._written];      

        ///<inheritdoc/>
        public int RemainingSize
        {
            get
            {
                int remaining = _length - _written;
                Debug.Assert(remaining > 0);
                return remaining;
            }
        }

        ///<inheritdoc/>
        public int AccumulatedSize => _written;

        #region Stream Implementation

        /// <summary>
        /// Gets a value that indicates if the stream can be read from.
        /// </summary>
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        /// <summary>
        /// Gets the length of the stream, which is the total number of bytes written to it.
        /// </summary>
        public override long Length => _written;

        /// <summary>
        /// Gets the current position in the stream, which is the number of bytes written so far.
        /// Setting the position is not supported and will throw an exception.
        /// </summary>
        public override long Position
        {
            get => _written;
            set => throw new InvalidOperationException("This stream does not support seeking.");
        }

        /// <summary>
        /// This operation is not supported, and will always throw a 
        /// <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override int Read(Span<byte> buffer) 
            => throw new NotSupportedException("This stream does not support reading.");

        /// <summary>
        /// Writes specified buffer to the internal buffer. If the internal buffer is 
        /// undersized or the buffer exceeds the remaining size, an exception will be thrown.
        /// </summary>
        /// <param name="buffer"></param>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return;
            }

            // Must not write more than the remaining buffer size
            ArgumentOutOfRangeException.ThrowIfGreaterThan(buffer.Length, RemainingSize);

            ref byte baseRef = ref MemoryMarshal.GetReference(_handle.GetSpan());

            // It's assumed that we’re going to be writing relatively small amounts of data for now,
            // so small-memmove is preferred for performance reasons.

            MemoryUtil.SmallMemmove(
                src: in MemoryMarshal.AsRef<byte>(buffer),
                srcOffset: 0,
                dst: ref baseRef,
                dstOffset: (uint)_written,
                elementCount: (ushort)buffer.Length
            );

            Advance(buffer.Length);
        }

        /// <summary>
        /// Writes the specified buffer to the internal buffer. This function always runs synchronously,
        /// and returns a completed <see cref="ValueTask"/>. The buffer must not exceed the remaining size
        /// of the internal buffer, otherwise an <see cref="ArgumentOutOfRangeException"/> will be thrown.
        /// </summary>
        /// <param name="buffer">A readonly memory buffer to write to the internal buffer</param>
        /// <param name="cancellationToken">Is never read</param>
        /// <returns>
        /// <see cref="ValueTask.CompletedTask"/>If the operation was successful, otherwise a completed 
        /// <see cref="ValueTask"/> with an exception.
        /// </returns>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                Write(buffer.Span);
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        /// <summary>
        /// Writes the specified buffer to the internal buffer. This function always runs synchronously,
        /// and returns a completed <see cref="Task"/>. The buffer must not exceed the remaining size
        /// of the internal buffer, otherwise an <see cref="ArgumentOutOfRangeException"/> will be thrown.
        /// </summary>
        /// <param name="buffer"> A byte array to write to the internal buffer</param>
        /// <param name="count"> The number of bytes to write from the buffer</param>
        /// <param name="offset"> The offset in the buffer to start writing from</param>
        /// <param name="cancellationToken">Is never read</param>
        /// <returns>
        /// <see cref="Task.CompletedTask"/>If the operation was successful, otherwise a completed 
        /// <see cref="Task"/> with an exception.
        /// </returns>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                Write(buffer.AsSpan(offset, count));
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void Close()
        {
            // Since this request is reusable, we do not want to close the stream
            // so do nothing here.
        }

        /// <summary>
        /// This is a no-op method, as the FBM protocol does not require flushing
        /// </summary>
        public override void Flush()
        { }

        public override long Seek(long offset, SeekOrigin origin) 
            => throw new NotSupportedException("This stream does not support seeking.");

        public override void SetLength(long value) 
            => throw new NotSupportedException("This stream does not support setting length.");

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override void Write(byte[] buffer, int offset, int count)
          => Write(buffer.AsSpan(offset, count));

        #endregion

        #region IBufferWriter<byte> Implementation

        /// <summary>
        /// Advances the internal write position by the specified count.
        /// </summary>
        /// <param name="count">The number of bytes written to the output</param>
        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, RemainingSize);
            _written += count;
        }

        /// <summary>
        /// Gets a memory segment of the internal buffer to write to.
        /// </summary>
        /// <param name="sizeHint">The minimum desired size of the buffer to return</param>
        /// <returns>A memory segment of the internal buffer</returns>
        /// <exception cref="NotImplementedException"></exception>
        Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(sizeHint, RemainingSize);

            Memory<byte> memory = _handle.GetMemory();

            // Always return the remaining segment of the buffer regardless of sizeHint
            // although if sizeHint is specified, users assumed a buffer size that is
            // too large to commit
            return memory.Slice(_written, RemainingSize);
        }

        /// <summary>
        /// Gets the remaining segment of the internal buffer as a span, regarless
        /// of the size hint provided. The size hint is ignored in this case. If the 
        /// size hint is greater than the remaining size, an exception will be thrown.
        /// </summary>
        /// <param name="sizeHint">The minimum desired size of the buffer to return</param>
        /// <returns>A span of the internal buffer</returns>
        Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(sizeHint, RemainingSize);

            // Get the span of the internal buffer
            Span<byte> span = _handle.GetSpan();

            // Always return the remaining segment of the buffer regardless of sizeHint
            // although if sizeHint is specified, users assumed a buffer size that is
            // too large to commit
            return span.Slice(_written, RemainingSize);
        }

        #endregion

        #region IDataAccumulator<byte> Implementation

        /// <summary>
        /// Resets the internal state of the accumulator, clearing any written data.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Reset() => _written = 0;      

        ///<inheritdoc/>
        Span<byte> IDataAccumulator<byte>.Remaining
        {
            get
            {
                // Get the remaining segment of the internal buffer
                Span<byte> span = _handle.GetSpan();
                return span.Slice(_written, RemainingSize);
            }
        }

        ///<inheritdoc/>
        Span<byte> IDataAccumulator<byte>.Accumulated
        {
            get
            {
                // Get the accumulated segment of the internal buffer
                Span<byte> span = _handle.GetSpan();
                return span[.._written];
            }
        }

        #endregion

        #region FMB Response header buffer implementation

        /*
         * So this is a slightly funky setup. When these methods are accessed,
         * its during a response operation. Data is accumulated in the internal
         * buffer then accessed by response objects to read headers while in use.
         * 
         * This is the 3rd job of this buffer. This is done, because message headers 
         * are stored in encded text, so we need a place to buffer them.
         */

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Span<char> IFBMHeaderBuffer.GetSpan(int offset, int count)
        {
            //Get the character span
            Span<char> span = MemoryMarshal.Cast<byte, char>(_handle.GetSpan());
            return span.Slice(offset, count);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Span<char> IFBMHeaderBuffer.GetSpan()
            => MemoryMarshal.Cast<byte, char>(_handle.GetSpan());

        #endregion
    }
}
