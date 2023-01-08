/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnStreamReader.cs 
*
* VnStreamReader.cs is part of VNLib.Utils which is part of the larger 
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
using System.Text;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Binary based buffered text reader, optimized for reading network streams
    /// </summary>
    public class VnStreamReader : TextReader, IVnTextReader
    {
        private bool disposedValue;

        private readonly ISlindingWindowBuffer<byte> _buffer;
        ///<inheritdoc/>
        public virtual Stream BaseStream { get; }
        ///<inheritdoc/>
        public Encoding Encoding { get; }

        /// <summary>
        /// Number of available bytes of buffered data within the current buffer window
        /// </summary>
        public int Available => _buffer.AccumulatedSize;
        /// <summary>
        /// Gets or sets the line termination used to deliminate a line of data
        /// </summary>
        public ReadOnlyMemory<byte> LineTermination { get; set; }
        Span<byte> IVnTextReader.BufferedDataWindow => _buffer.Accumulated;

        /// <summary>
        /// Creates a new <see cref="TextReader"/> that reads encoded data from the base.
        /// Internal buffers will be alloced from <see cref="ArrayPool{T}.Shared"/>
        /// </summary>
        /// <param name="baseStream">The underlying stream to read data from</param>
        /// <param name="enc">The <see cref="Encoding"/> to use when reading from the stream</param>
        /// <param name="bufferSize">The size of the internal binary buffer</param>
        public VnStreamReader(Stream baseStream, Encoding enc, int bufferSize)
        {
            BaseStream = baseStream;
            Encoding = enc;
            //Init a new buffer
            _buffer = InitializeBuffer(bufferSize);
        }

        /// <summary>
        /// Invoked by the constuctor method to allocte the internal buffer with the specified buffer size.
        /// </summary>
        /// <param name="bufferSize">The requested size of the buffer to alloc</param>
        /// <remarks>By default requests the buffer from the <see cref="ArrayPool{T}.Shared"/> instance</remarks>
        protected virtual ISlindingWindowBuffer<byte> InitializeBuffer(int bufferSize) => new ArrayPoolStreamBuffer<byte>(ArrayPool<byte>.Shared, bufferSize);

        ///<inheritdoc/>
        public override async Task<string?> ReadLineAsync()
        {
            //If buffered data is available, check for line termination
            if (Available > 0)
            {
                //Get current buffer window
                Memory<byte> buffered = _buffer.AccumulatedBuffer;
                //search for line termination in current buffer
                int term = buffered.IndexOf(LineTermination);
                //Termination found in buffer window
                if (term > -1)
                {
                    //Capture the line from the begining of the window to the termination
                    Memory<byte> line = buffered[..term];
                    //Shift the window to the end of the line (excluding the termination)
                    _buffer.AdvanceStart(term + LineTermination.Length);
                    //Decode the line to a string
                    return Encoding.GetString(line.Span);
                }
                //Termination not found
            }
            //Compact the buffer window and see if space is avialble to buffer more data
            if (_buffer.CompactBufferWindow())
            {
                //There is room, so buffer more data
                await _buffer.AccumulateDataAsync(BaseStream, CancellationToken.None);
                //Check again to see if more data is buffered
                if (Available <= 0)
                {
                    //No string found
                    return null;
                }
                //Get current buffer window
                Memory<byte> buffered = _buffer.AccumulatedBuffer;
                //search for line termination in current buffer
                int term = buffered.IndexOf(LineTermination);
                //Termination found in buffer window
                if (term > -1)
                {
                    //Capture the line from the begining of the window to the termination
                    Memory<byte> line = buffered[..term];
                    //Shift the window to the end of the line (excluding the termination)
                    _buffer.AdvanceStart(term + LineTermination.Length);
                    //Decode the line to a string
                    return Encoding.GetString(line.Span);
                }
            }
            //Termination not found within the entire buffer, so buffer space has been exhausted

            //OOM is raised in the TextReader base class, the standard is preserved
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new OutOfMemoryException("A line termination was not found within the buffer");
#pragma warning restore CA2201 // Do not raise reserved exception types
        }
       
        ///<inheritdoc/>
        public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));
        ///<inheritdoc/>
        public override int Read(Span<char> buffer)
        {
            if (Available <= 0)
            {
                return 0;
            }
            //Get current buffer window
            Span<byte> buffered = _buffer.Accumulated;
            //Convert all avialable data
            int encoded = Encoding.GetChars(buffered, buffer);
            //Shift buffer window to the end of the converted data
            _buffer.AdvanceStart(encoded);
            //return the number of chars written
            return Encoding.GetCharCount(buffered);
        }
        ///<inheritdoc/>
        public override void Close() => _buffer.Close();
        ///<inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Close();
                disposedValue = true;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Resets the internal buffer window
        /// </summary>
        protected void ClearBuffer()
        {
            _buffer.Reset();
        }

        void IVnTextReader.Advance(int count) => _buffer.AdvanceStart(count);
        void IVnTextReader.FillBuffer() => _buffer.AccumulateData(BaseStream);
        ERRNO IVnTextReader.CompactBufferWindow() => _buffer.CompactBufferWindow();
    }
}