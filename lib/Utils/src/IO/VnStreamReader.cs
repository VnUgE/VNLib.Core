/*
* Copyright (c) 2023 Vaughn Nugent
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

        ///<inheritdoc/>
        public Span<byte> BufferedDataWindow => _buffer.Accumulated;

        /// <summary>
        /// Creates a new <see cref="TextReader"/> that reads encoded data from the base stream
        /// and allocates a new buffer of the specified size from the shared <see cref="ArrayPool{T}"/>
        /// </summary>
        /// <param name="baseStream">The underlying stream to read data from</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when reading from the stream</param>
        /// <param name="bufferSize">The size of the internal binary buffer</param>
        /// <exception cref="ArgumentNullException"></exception>
        public VnStreamReader(Stream baseStream, Encoding encoding, int bufferSize)
            : this(baseStream, encoding, bufferSize, ArrayPoolStreamBuffer<byte>.Shared)
        {
        }

        /// <summary>
        /// Creates a new <see cref="TextReader"/> that reads encoded data from the base stream
        /// and allocates a new buffer of the specified size from the supplied buffer factory.
        /// </summary>
        /// <param name="baseStream">The underlying stream to read data from</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when reading from the stream</param>
        /// <param name="bufferSize">The size of the internal binary buffer</param>
        /// <param name="bufferFactory">The buffer factory to create the buffer from</param>
        /// <exception cref="ArgumentNullException"></exception>
        public VnStreamReader(Stream baseStream, Encoding encoding, int bufferSize, IStreamBufferFactory<byte> bufferFactory)
            :this(baseStream, encoding, bufferFactory?.CreateBuffer(bufferSize)!)
        {
        }

        /// <summary>
        /// Creates a new <see cref="TextReader"/> that reads encoded data from the base stream 
        /// and uses the specified buffer.
        /// </summary>
        /// <param name="baseStream">The underlying stream to read data from</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when reading from the stream</param>
        /// <param name="buffer">The internal <see cref="ISlindingWindowBuffer{T}"/> to use</param>
        /// <exception cref="ArgumentNullException"></exception>
        public VnStreamReader(Stream baseStream, Encoding encoding, ISlindingWindowBuffer<byte> buffer)
        {
            BaseStream = baseStream ?? throw new ArgumentNullException(nameof(buffer));
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        ///<inheritdoc/>
        public override async Task<string?> ReadLineAsync()
        {
            string? result = null;

            //If buffered data is available, check for line termination
            if (Available > 0 && GetStringFromBuffer(ref result))
            {
                return result;
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
               
                //Termination not found
                if (GetStringFromBuffer(ref result))
                {
                    return result;
                }
            }
            //Termination not found within the entire buffer, so buffer space has been exhausted

            //OOM is raised in the TextReader base class, the standard is preserved
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new OutOfMemoryException("A line termination was not found within the buffer");
#pragma warning restore CA2201 // Do not raise reserved exception types
        }

        private bool GetStringFromBuffer(ref string? result)
        {
            //Get current buffer window
            Memory<byte> buffered = _buffer.AccumulatedBuffer;

            //search for line termination in current buffer
            int term = buffered.IndexOf(LineTermination);

            //Termination found in buffer window
            if (term > -1)
            {
                //Capture the line from the beginning of the window to the termination
                Memory<byte> line = buffered[..term];

                //Shift the window to the end of the line (excluding the termination)
                _buffer.AdvanceStart(term + LineTermination.Length);

                //Decode the line to a string
                result = Encoding.GetString(line.Span);
                return true;
            }

            return false;
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

            //Convert all available data
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


        ///<inheritdoc/>
        public void Advance(int count) => _buffer.AdvanceStart(count);
        
        ///<inheritdoc/>
        public void FillBuffer() => _buffer.AccumulateData(BaseStream);
        
        ///<inheritdoc/>
        public ERRNO CompactBufferWindow() => _buffer.CompactBufferWindow();
    }
}