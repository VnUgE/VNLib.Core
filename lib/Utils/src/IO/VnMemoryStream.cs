/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

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
        public const int DefaultBufferSize = 4096;

        private nint _position;
        private nint _length;
        private bool _isReadonly;
        
        //Memory
        private readonly IResizeableMemoryHandle<byte> _buffer;
        //Default owns handle
        private readonly bool OwnsHandle = true;

        /// <summary>
        /// Creates a new <see cref="VnMemoryStream"/> pointing to the begining of memory, and consumes the handle.
        /// </summary>
        /// <param name="handle"><see cref="IResizeableMemoryHandle{T}"/> to consume</param>
        /// <param name="length">Length of the stream</param>
        /// <param name="readOnly">Should the stream be readonly?</param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns>A <see cref="VnMemoryStream"/> wrapper to access the handle data</returns>
        public static VnMemoryStream ConsumeHandle(IResizeableMemoryHandle<byte> handle, nint length, bool readOnly) 
            => FromHandle(handle, true, length, readOnly);

        /// <summary>
        /// Creates a new <see cref="VnMemoryStream"/> from the supplied memory handle 
        /// of the initial length. This function also accepts a value that indicates if this stream 
        /// owns the memory handle, which will cause it to be disposed when the stream is disposed.
        /// </summary>
        /// <param name="handle"><see cref="IResizeableMemoryHandle{T}"/> to consume</param>
        /// <param name="length">The initial length of the stream</param>
        /// <param name="readOnly">Should the stream be readonly?</param>
        /// <param name="ownsHandle">A value that indicates if the current stream owns the memory handle</param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns>A <see cref="VnMemoryStream"/> wrapper to access the handle data</returns>
        public static VnMemoryStream FromHandle(IResizeableMemoryHandle<byte> handle, bool ownsHandle, nint length, bool readOnly)
        {
            //Check the handle
            ArgumentNullException.ThrowIfNull(handle);

            return handle.CanRealloc || readOnly
                ? new VnMemoryStream(handle, length, readOnly, ownsHandle)
                : throw new ArgumentException("The supplied memory handle must be resizable on a writable stream", nameof(handle));
        }

        /// <summary>
        /// Converts a writable <see cref="VnMemoryStream"/> to readonly to allow shallow copies
        /// </summary>
        /// <remarks>
        /// This funciton will convert the stream passed into it to a readonly stream. 
        /// The function passes through the input stream as the return value
        /// </remarks>
        /// <param name="stream">The stream to make readonly</param>
        /// <returns>A reference to the modified input stream</returns>
        public static VnMemoryStream CreateReadonly(VnMemoryStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
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
        public VnMemoryStream(IUnmangedHeap heap) : this(heap, DefaultBufferSize, false) { }

        /// <summary>
        /// Creates a new memory stream using the <see cref="MemoryUtil.Shared"/>
        /// global heap instance.
        /// </summary>
        public VnMemoryStream(nuint bufferSize, bool zero) : this(MemoryUtil.Shared, bufferSize, zero) { }

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
            ArgumentNullException.ThrowIfNull(heap);
            _buffer = heap.Alloc<byte>(bufferSize, zero);
        }
       
        /// <summary>
        /// Creates a new memory stream from the data provided
        /// </summary>
        /// <param name="heap"><see cref="Win32PrivateHeap"/> to allocate memory from</param>
        /// <param name="data">Initial data</param>
        public VnMemoryStream(IUnmangedHeap heap, ReadOnlySpan<byte> data)
        {
            ArgumentNullException.ThrowIfNull(heap);
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
            ArgumentNullException.ThrowIfNull(heap);
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
        private VnMemoryStream(IResizeableMemoryHandle<byte> buffer, nint length, bool readOnly, bool ownsHandle)
        {
            Debug.Assert(length >= 0, "Length must be positive");
            Debug.Assert(buffer.CanRealloc || readOnly, "The supplied buffer is not resizable on a writable stream");

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
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bufferSize, 0);
   
            if (!destination.CanWrite)
            {
                throw new IOException("The destinaion stream is not writeable");
            }

            while (LenToPosDiff > 0)
            {
                //Calc the remaining bytes to read no larger than the buffer size
                int bytesToRead = (int)Math.Min(LenToPosDiff, bufferSize);

                //Create a span wrapper by using the offet function to support memory handles larger than 2gb
                ReadOnlySpan<byte> span = _buffer.GetOffsetSpan(_position, bytesToRead);
                
                destination.Write(span);

                //Update position
                _position += bytesToRead;
            }
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
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bufferSize, 0);

            if (!destination.CanWrite)
            {
                throw new IOException("The destinaion stream is not writeable");
            }

            cancellationToken.ThrowIfCancellationRequested();            

            if(_length < Int32.MaxValue)
            {
                //Safe to alloc a memory manager to do copy
                using MemoryManager<byte> asMemManager = _buffer.ToMemoryManager(false);

                /*
                 * CopyTo starts at the current position, as if calling Read() 
                 * so the reader must be offset to match and the _length gives us the 
                 * actual length of the stream and therefor the segment size
                 */              

                while(LenToPosDiff > 0)
                {
                    int blockSize = Math.Min((int)LenToPosDiff, bufferSize);
                    Memory<byte> window = asMemManager.Memory.Slice((int)_position, blockSize);

                    //write async
                    await destination.WriteAsync(window, cancellationToken);

                    //Update position
                    _position+= bufferSize;
                }
            }
            else
            {
                //TODO support 64bit memory stream copy
                throw new NotSupportedException("64bit async copies are currently not supported");
            }
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
        public override int ReadByte()
        {
            if (LenToPosDiff == 0)
            {
                return -1;
            }

            //get the value at the current position
            ref byte ptr = ref _buffer.GetOffsetByteRef(_position);
            
            //Increment position
            _position++;

            //Return value
            return ptr;
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
            //gaurd for overflow, offset cannot be greater than platform pointer size
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, nint.MaxValue);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(offset, nint.MinValue);

            //safe cast to nint
            nint _offset = (nint)offset;

            switch (origin)
            {
                case SeekOrigin.Begin:

                    ArgumentOutOfRangeException.ThrowIfNegative(offset);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, _length);

                    //Length will never be greater than nint.Max so output will never exceed nint.max
                    return _position = Math.Min(_length, _offset);

                case SeekOrigin.Current:

                    if(_offset < 0)
                    {
                        ArgumentOutOfRangeException.ThrowIfLessThan(offset, -_position);
                    }
                    else
                    {
                        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, LenToPosDiff);
                    }

                    //Calc new seek position from current position
                    nint newPos = _position + _offset;
                    return _position = Math.Min(_length, newPos);

                case SeekOrigin.End:

                    //Must be negative value
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(_offset, 0, nameof(offset));
                    ArgumentOutOfRangeException.ThrowIfLessThan(_offset, -_length, nameof(offset));

                    //Calc new seek position from end of stream, should be len -1 so 0 can be specified from the end
                    nint realIndex = _length + _offset;
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

            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, nint.MaxValue);
            
            nint _value = (nint)value;

            //Resize the buffer to the specified length
            _buffer.Resize(_value);
            
            //Set length
            _length = _value;
            
            //Make sure the position is not pointing outside of the buffer after resize
            _position = Math.Min(_position, _length);
        }

        ///<inheritdoc/>
        ///<exception cref="OutOfMemoryException"></exception>
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        ///<inheritdoc/>
        ///<exception cref="OutOfMemoryException"></exception>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_isReadonly)
            {
                throw new NotSupportedException("Write operation is not allowed on readonly stream!");
            }
            //Calculate the new final position
            nint newPos = checked(_position + buffer.Length);
            //Determine if the buffer needs to be expanded
            if (buffer.Length > LenToPosDiff)
            {
                //Expand buffer if required
                _buffer.ResizeIfSmaller(newPos);
                //Update length
                _length = newPos;
            }

            //Copy the input buffer to the internal buffer
            MemoryUtil.Copy(
                source: buffer, 
                sourceOffset: 0, 
                dest: _buffer, 
                destOffset: (nuint)_position, 
                count: buffer.Length
            );
            
            //Update the position
            _position = newPos;
        }

        ///<inheritdoc/>
        ///<exception cref="OutOfMemoryException"></exception>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            //Write synchronously and return a completed task
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        ///<inheritdoc/>
        ///<exception cref="OutOfMemoryException"></exception>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            //Write synchronously and return a completed task
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        ///<inheritdoc/>
        ///<exception cref="OutOfMemoryException"></exception>
        public override void WriteByte(byte value) => Write(new Span<byte>(ref value));

        /// <summary>
        /// Allocates and copies internal buffer to new managed byte[]
        /// </summary>
        /// <returns>Copy of internal buffer</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        public byte[] ToArray()
        {
            byte[] data;

            if (_length < Int32.MaxValue)
            {
                //Alloc uninialized, since were going to overwite it anyway
                data = GC.AllocateUninitializedArray<byte>((int)_length, false);
            }
            else
            {
                //Use new opperator if larger than 32bit
                data = new byte[_length];
            }
           
            MemoryUtil.CopyArray(_buffer, 0, data, 0, (nuint)_length);
            
            return data;
        }
        
        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{T}"/> window over the data within the entire stream
        /// that is equal in length to the stream length.
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

    }
}