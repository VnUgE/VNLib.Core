/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnMemoryStream.cs 
*
* VnMemoryStream.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.InteropServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Provides an unmanaged memory stream. Desigend to help reduce garbage collector load for 
    /// high frequency memory operations. Similar to <see cref="UnmanagedMemoryStream"/>
    /// </summary>
    public sealed class VnMemoryStream : Stream, ICloneable
    {
        private nint _position;
        private nint _length;
        private bool _isReadonly;
        
        //Memory
        private readonly MemoryHandle<byte> _buffer;
        //Default owns handle
        private readonly bool OwnsHandle = true;

        /// <summary>
        /// Creates a new <see cref="VnMemoryStream"/> pointing to the begining of memory, and consumes the handle.
        /// </summary>
        /// <param name="handle"><see cref="MemoryHandle{T}"/> to consume</param>
        /// <param name="length">Length of the stream</param>
        /// <param name="readOnly">Should the stream be readonly?</param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns>A <see cref="VnMemoryStream"/> wrapper to access the handle data</returns>
        public static VnMemoryStream ConsumeHandle(MemoryHandle<byte> handle, nint length, bool readOnly)
        {
            handle.ThrowIfClosed();
            return new VnMemoryStream(handle, length, readOnly, true);
        }
        
        /// <summary>
        /// Converts a writable <see cref="VnMemoryStream"/> to readonly to allow shallow copies
        /// </summary>
        /// <param name="stream">The stream to make readonly</param>
        /// <returns>The readonly stream</returns>
        public static VnMemoryStream CreateReadonly(VnMemoryStream stream)
        {
            //Set the readonly flag
            stream._isReadonly = true;
            //Return the stream
            return stream;
        }

        /// <summary>
        /// Creates a new memory stream using the <see cref="MemoryUtil.Shared"/>
        /// global heap instance.
        /// </summary>
        public VnMemoryStream() : this(MemoryUtil.Shared) { }
        
        /// <summary>
        /// Create a new memory stream where buffers will be allocated from the specified heap
        /// </summary>
        /// <param name="heap"><see cref="Win32PrivateHeap"/> to allocate memory from</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public VnMemoryStream(IUnmangedHeap heap) : this(heap, 0, false) { }
       
        /// <summary>
        /// Creates a new memory stream and pre-allocates the internal
        /// buffer of the specified size on the specified heap to avoid resizing.
        /// </summary>
        /// <param name="heap"><see cref="Win32PrivateHeap"/> to allocate memory from</param>
        /// <param name="bufferSize">The initial internal buffer size, does not effect the length/size of the stream, helps pre-alloc</param>
        /// <param name="zero">Zero memory allocations during buffer expansions</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public VnMemoryStream(IUnmangedHeap heap, nuint bufferSize, bool zero)
        {
            _ = heap ?? throw new ArgumentNullException(nameof(heap));
            _buffer = heap.Alloc<byte>(bufferSize, zero);
        }
       
        /// <summary>
        /// Creates a new memory stream from the data provided
        /// </summary>
        /// <param name="heap"><see cref="Win32PrivateHeap"/> to allocate memory from</param>
        /// <param name="data">Initial data</param>
        public VnMemoryStream(IUnmangedHeap heap, ReadOnlySpan<byte> data)
        {
            _ = heap ?? throw new ArgumentNullException(nameof(heap));
            //Alloc the internal buffer to match the data stream
            _buffer = heap.AllocAndCopy(data);
            //Set length
            _length = data.Length;
            _position = 0;
        }

        /// <summary>
        /// Creates a new memory stream from the data provided
        /// </summary>
        /// <param name="heap"><see cref="Win32PrivateHeap"/> to allocate memory from</param>
        /// <param name="data">Initial data</param>
        public VnMemoryStream(IUnmangedHeap heap, ReadOnlyMemory<byte> data)
        {
            _ = heap ?? throw new ArgumentNullException(nameof(heap));
            //Alloc the internal buffer to match the data stream
            _buffer = heap.AllocAndCopy(data);
            //Set length
            _length = data.Length;
            _position = 0;
        }

        /// <summary>
        /// WARNING: Dangerous constructor, make sure read-only and owns hanlde are set accordingly
        /// </summary>
        /// <param name="buffer">The buffer to referrence directly</param>
        /// <param name="length">The length property of the stream</param>
        /// <param name="readOnly">Is the stream readonly (should mostly be true!)</param>
        /// <param name="ownsHandle">Does the new stream own the memory -> <paramref name="buffer"/></param>
        private VnMemoryStream(MemoryHandle<byte> buffer, nint length, bool readOnly, bool ownsHandle)
        {
            OwnsHandle = ownsHandle;
            _buffer = buffer;                  //Consume the handle
            _length = length;                  //Store length of the buffer
            _isReadonly = readOnly;
        }
      
        /// <summary>
        /// UNSAFE Number of bytes between position and length. Never negative
        /// </summary>
        private nint LenToPosDiff => Math.Max(_length - _position, 0);

        /// <summary>
        /// If the current stream is a readonly stream, creates an unsafe shallow copy for reading only.
        /// </summary>
        /// <returns>New stream shallow copy of the internal stream</returns>
        /// <exception cref="NotSupportedException"></exception>
        public VnMemoryStream GetReadonlyShallowCopy()
        {
            //Create a new readonly copy (stream does not own the handle)
            return !_isReadonly
                ? throw new NotSupportedException("This stream is not readonly. Cannot create shallow copy on a mutable stream")
                : new VnMemoryStream(_buffer, _length, true, false);
        }
        
        /// <summary>
        /// Writes data directly to the destination stream from the internal buffer
        /// without allocating or copying any data.
        /// </summary>
        /// <param name="destination">The stream to write data to</param>
        /// <param name="bufferSize">The size of the chunks to write to the destination stream</param>
        /// <exception cref="IOException"></exception>
        public override void CopyTo(Stream destination, int bufferSize)
        {
            _ = destination ?? throw new ArgumentNullException(nameof(destination));
            if(bufferSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than 0");
            }
            
            if (!destination.CanWrite)
            {
                throw new IOException("The destinaion stream is not writeable");
            }
            
            do
            {
                //Calc the remaining bytes to read no larger than the buffer size
                int bytesToRead = (int)Math.Min(LenToPosDiff, bufferSize);

                //Create a span wrapper by using the offet function to support memory handles larger than 2gb
                ReadOnlySpan<byte> span = _buffer.GetOffsetSpan(_position, bytesToRead);
                
                destination.Write(span);

                //Update position
                _position += bytesToRead;
                
            } while (LenToPosDiff > 0);
        }

        /// <summary>
        /// Allocates a temporary buffer of the desired size, copies data from the internal 
        /// buffer and writes it to the destination buffer asynchronously.
        /// </summary>
        /// <param name="destination">The stream to write output data to</param>
        /// <param name="bufferSize">The size of the buffer to use when copying data</param>
        /// <param name="cancellationToken">A token to cancel the opreation</param>
        /// <returns>A task that resolves when the remaining data in the stream has been written to the destination</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            _ = destination ?? throw new ArgumentNullException(nameof(destination));
            
            if (!destination.CanWrite)
            {
                throw new IOException("The destinaion stream is not writeable");
            }

            cancellationToken.ThrowIfCancellationRequested();

            /*
             * Alloc temp copy buffer. This is a requirement because
             * the stream may be larger than an int32 so it must be 
             * copied by segment
             */

            using VnTempBuffer<byte> copyBuffer = new(bufferSize);

            do
            {
                //read from internal stream
                int read = Read(copyBuffer);

                if(read <= 0)
                {
                    break;
                }

                //write async
                await destination.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken);

            } while (true);
            
        }

        /// <summary>
        /// <inheritdoc/>
        /// <para>
        /// This property is always true
        /// </para>
        /// </summary>
        public override bool CanRead => true;
        /// <summary>
        /// <inheritdoc/>
        /// <para>
        /// This propery is always true
        /// </para>
        /// </summary>
        public override bool CanSeek => true;
        /// <summary>
        /// True unless the stream is (or has been converted to) a readonly
        /// stream.
        /// </summary>
        public override bool CanWrite => !_isReadonly;
        ///<inheritdoc/>
        public override long Length => _length;
        ///<inheritdoc/>
        public override bool CanTimeout => false;
       
        ///<inheritdoc/>
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }
        /// <summary>
        /// Closes the stream and frees the internal allocated memory blocks
        /// </summary>
        public override void Close()
        {
            //Only dispose buffer if we own it
            if (OwnsHandle)
            {
                _buffer.Dispose();
            }
        }
        ///<inheritdoc/>
        public override void Flush() { }
        // Override to reduce base class overhead
        ///<inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        ///<inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        ///<inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }
            //Number of bytes to read from memory buffer
            int bytesToRead = (int)Math.Min(LenToPosDiff, buffer.Length);
            
            //Copy bytes to buffer
            MemoryUtil.Copy(_buffer, _position, buffer, 0, bytesToRead);
            
            //Increment buffer position
            _position += bytesToRead;
            
            return bytesToRead;
        }
        ///<inheritdoc/>
        public override unsafe int ReadByte()
        {
            if (LenToPosDiff == 0)
            {
                return -1;
            }

            //get the value at the current position
            byte* ptr = _buffer.GetOffset(_position);
            
            //Increment position
            _position++;

            //Return value
            return ptr[0];
        }

        /*
         * Async reading will always run synchronously in a memory stream,
         * so overrides are just so avoid base class overhead
         */
        ///<inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            //Read synchronously and return a completed task
            int read = Read(buffer.Span);
            return ValueTask.FromResult(read);
        }
        ///<inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            //Read synchronously and return a completed task
            int read = Read(buffer.AsSpan(offset, count));
            return Task.FromResult(read);
        }
        ///<inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be less than 0");
            }
            if(offset > nint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be less than nint.MaxValue");
            }

            //safe cast to nint
            nint _offset = (nint)offset;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    //Length will never be greater than nint.Max so output will never exceed nint.max
                    return _position = Math.Min(_length, _offset);
                case SeekOrigin.Current:
                    //Calc new seek position from current position
                    nint newPos = _position + _offset;
                    return _position = Math.Min(_length, newPos);
                case SeekOrigin.End:
                    //Calc new seek position from end of stream, should be len -1 so 0 can be specified from the end
                    nint realIndex = _length - (_offset - 1);
                    return _position = Math.Min(realIndex, 0);
                default:
                    throw new ArgumentException("Stream operation is not supported on current stream");
            }
        }
        

        /// <summary>
        /// Resizes the internal buffer to the exact size (in bytes) of the 
        /// value argument. A value of 0 will free the entire buffer. A value 
        /// greater than zero will resize the buffer (and/or alloc)
        /// </summary>
        /// <param name="value">The size of the stream (and internal buffer)</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override void SetLength(long value)
        {
            if (_isReadonly)
            {
                throw new NotSupportedException("This stream is readonly");
            }
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value cannot be less than 0");
            }
            if(value > nint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value cannot be greater than nint.MaxValue");
            }
            
            nint _value = (nint)value;

            //Resize the buffer to the specified length
            _buffer.Resize(_value);
            
            //Set length
            _length = _value;
            
            //Make sure the position is not pointing outside of the buffer after resize
            _position = Math.Min(_position, _length);
        }
        ///<inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        ///<inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_isReadonly)
            {
                throw new NotSupportedException("Write operation is not allowed on readonly stream!");
            }
            //Calculate the new final position
            nint newPos = (_position + buffer.Length);
            //Determine if the buffer needs to be expanded
            if (buffer.Length > LenToPosDiff)
            {
                //Expand buffer if required
                _buffer.ResizeIfSmaller(newPos);
                //Update length
                _length = newPos;
            }
            //Copy the input buffer to the internal buffer
            MemoryUtil.Copy(buffer, _buffer, (nuint)_position);
            //Update the position
            _position = newPos;
        }
        ///<inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            //Write synchronously and return a completed task
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
        ///<inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            //Write synchronously and return a completed task
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
        ///<inheritdoc/>
        public override void WriteByte(byte value)
        {
            Span<byte> buf = MemoryMarshal.CreateSpan(ref value, 1);
            Write(buf);
        }

        /// <summary>
        /// Allocates and copies internal buffer to new managed byte[]
        /// </summary>
        /// <returns>Copy of internal buffer</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        public byte[] ToArray()
        {
            //Alloc a new array of the size of the internal buffer, may be 64 bit large block
            byte[] data = new byte[_length];
            
            //Copy the internal buffer to the new array
            MemoryUtil.Copy(_buffer, 0, data, 0, (nuint)_length);
            
            return data;
        }
        
        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{T}"/> window over the data within the entire stream
        /// </summary>
        /// <returns>A <see cref="ReadOnlySpan{T}"/> of the data within the entire stream</returns>
        /// <exception cref="OverflowException"></exception>
        public ReadOnlySpan<byte> AsSpan()
        {
            //Get 32bit length or throw
            int len = Convert.ToInt32(_length);
            //Get span with no offset
            return _buffer.AsSpan(0, len);
        }
      
        /// <summary>
        /// If the current stream is a readonly stream, creates a shallow copy for reading only.
        /// </summary>
        /// <returns>New stream shallow copy of the internal stream</returns>
        /// <exception cref="NotSupportedException"></exception>
        public object Clone() => GetReadonlyShallowCopy();

        /*
         * Override the Dispose async method to avoid the base class overhead
         * and task allocation since this will always be a syncrhonous
         * operation (freeing memory)
         */

        ///<inheritdoc/>
        public override ValueTask DisposeAsync()
        {
            //Dispose and return completed task
            base.Dispose(true);
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}