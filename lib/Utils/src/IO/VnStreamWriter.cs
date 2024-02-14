/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnStreamWriter.cs 
*
* VnStreamWriter.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;

#pragma warning disable CA2215, CA2007, CS8765 // Dispose methods should call base class dispose

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Provides a memory optimized <see cref="TextWriter"/> implementation. Optimized for writing
    /// to network streams
    /// </summary>
    public class VnStreamWriter : TextWriter
    {
        private readonly Encoder Enc;

        private readonly ISlindingWindowBuffer<byte> _buffer;
       
        private bool closed;

        /// <summary>
        /// Gets the underlying stream that interfaces with the backing store
        /// </summary>
        public virtual Stream BaseStream { get; }

        ///<inheritdoc/>
        public override Encoding Encoding { get; }

        /// <summary>
        /// Line termination to use when writing lines to the output
        /// </summary>
        public ReadOnlyMemory<byte> LineTermination { get; set; }

        ///<inheritdoc/>
        public override string NewLine
        {
            get => Encoding.GetString(LineTermination.Span);
            set => LineTermination = Encoding.GetBytes(value);
        }
       

        /// <summary>
        /// Creates a new <see cref="VnStreamWriter"/> that writes encoded data to the base stream
        /// and allocates a new buffer of the specified size from the shared <see cref="ArrayPool{T}"/>
        /// </summary>
        /// <param name="baseStream">The underlying stream to write data to</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when writing to the stream</param>
        /// <param name="bufferSize">The size of the internal binary buffer</param>
        /// <exception cref="ArgumentNullException"></exception>
        public VnStreamWriter(Stream baseStream, Encoding encoding, int bufferSize)
            : this(baseStream, encoding, bufferSize, ArrayPoolStreamBuffer<byte>.Shared)
        {
        }

        /// <summary>
        /// Creates a new <see cref="VnStreamWriter"/> that writes encoded data to the base stream
        /// and allocates a new buffer of the specified size from the supplied buffer factory.
        /// </summary>
        /// <param name="baseStream">The underlying stream to write data to</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when writing to the stream</param>
        /// <param name="bufferSize">The size of the internal binary buffer</param>
        /// <param name="bufferFactory">The buffer factory to create the buffer from</param>
        /// <exception cref="ArgumentNullException"></exception>
        public VnStreamWriter(Stream baseStream, Encoding encoding, int bufferSize, IStreamBufferFactory<byte> bufferFactory)
            : this(baseStream, encoding, bufferFactory?.CreateBuffer(bufferSize)!)
        {
        }

        /// <summary>
        /// Creates a new <see cref="VnStreamWriter"/> that writes encoded data to the base stream 
        /// and uses the specified buffer.
        /// </summary>
        /// <param name="baseStream">The underlying stream to write data to</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use when writing to the stream</param>
        /// <param name="buffer">The internal <see cref="ISlindingWindowBuffer{T}"/> to use</param>
        /// <exception cref="ArgumentNullException"></exception>
        public VnStreamWriter(Stream baseStream, Encoding encoding, ISlindingWindowBuffer<byte> buffer)
        {
            BaseStream = baseStream ?? throw new ArgumentNullException(nameof(buffer));
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

            //Get an encoder
            Enc = encoding.GetEncoder();
        }

        ///<inheritdoc/>
        public void Write(byte value)
        {
            //See if there is room in the binary buffer
            if (_buffer.AccumulatedSize == 0)
            {
                //There is not enough room to store the single byte
                Flush();
            }

            //Store at the end of the window
            _buffer.Append(value);
        }

        ///<inheritdoc/>
        public override void Write(char value) => Write(new Span<char>(ref value));

        ///<inheritdoc/>
        public override void Write(object? value) => Write(value?.ToString());

        ///<inheritdoc/>
        public override void Write(string? value) => Write(value.AsSpan());

        ///<inheritdoc/>
        public override void Write(ReadOnlySpan<char> buffer)
        {
            Check();

            ForwardOnlyReader<char> reader = new(buffer);
            
            //Create a variable for a character buffer window
            bool completed;
            do
            {
                //Get an available buffer window to store characters in and convert the characters to binary
                Enc.Convert(reader.Window, _buffer.Remaining, true, out int charsUsed, out int bytesUsed, out completed);

                //Update byte position
                _buffer.Advance(bytesUsed);

                //Update char position
                reader.Advance(charsUsed);

                //Converting did not complete because the buffer was too small
                if (!completed || reader.WindowSize == 0)
                {
                    //Flush the buffer and continue
                    Flush();
                }
                
            } while (!completed);

            //Reset the encoder
            Enc.Reset();
        }

        ///<inheritdoc/>
        public override async Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            Check();
          
            ForwardOnlyMemoryReader<char> reader = new(buffer);

            //Create a variable for a character buffer window
            bool completed;
            do
            {
                //Get an available buffer window to store characters in and convert the characters to binary
                Enc.Convert(reader.Window.Span, _buffer.Remaining, true, out int charsUsed, out int bytesUsed, out completed);

                //Update byte position
                _buffer.Advance(bytesUsed);

                //Update char position
                reader.Advance(charsUsed);

                //Converting did not complete because the buffer was too small
                if (!completed || reader.WindowSize == 0)
                {
                    //Flush the buffer and continue
                    await FlushWriterAsync(cancellationToken);
                }

            } while (!completed);

            //Reset the encoder
            Enc.Reset();
        }      

        ///<inheritdoc/>
        public override void WriteLine()
        {
            Check();

            //See if there is room in the binary buffer
            if (_buffer.RemainingSize < LineTermination.Length)
            {
                //There is not enough room to store the termination, so we need to flush the buffer
                Flush();
            }

            _buffer.Append(LineTermination.Span);
        }

        ///<inheritdoc/>
        public override void WriteLine(object? value) => WriteLine(value?.ToString());

        ///<inheritdoc/>
        public override void WriteLine(string? value) => WriteLine(value.AsSpan());

        ///<inheritdoc/>
        public override void WriteLine(ReadOnlySpan<char> buffer)
        {
            //Write the value itself
            Write(buffer);
            //Write the line termination
            WriteLine();
        }

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public override void Flush()
        {
            Check();
            //If data is available to be written, write it to the base stream
            if (_buffer.AccumulatedSize > 0)
            {
                //Write all buffered data to stream
                BaseStream.Write(_buffer.Accumulated);
                //Reset the buffer
                _buffer.Reset();
            }
        }

        /// <summary>
        /// Asynchronously flushes the internal buffers to the <see cref="BaseStream"/>, and resets the internal buffer state
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous flush operation</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public async ValueTask FlushWriterAsync(CancellationToken cancellationToken = default)
        {
            Check();

            if (_buffer.AccumulatedSize > 0)
            {
                //Flush current window to the stream
                await BaseStream.WriteAsync(_buffer.AccumulatedBuffer, cancellationToken);
                //Reset the buffer
                _buffer.Reset();
            }
        }

        ///<inheritdoc/>
        public override Task FlushAsync() => FlushWriterAsync().AsTask();
      
        /// <summary>
        /// Resets internal properies for resuse
        /// </summary>
        protected void Reset()
        {
            _buffer.Reset();
            Enc.Reset();
        }

        ///<inheritdoc/>
        public override void Close()
        {
            //Only invoke close once
            if (closed)
            {
                return;
            }
            try
            {
                Flush();
            }
            finally
            {
                //Release the memory handle if its set
                _buffer.Close();
                //Set closed flag
                closed = true;
            }
        }

        ///<inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Close();
            base.Dispose(disposing);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Check() => ObjectDisposedException.ThrowIf(closed, this);

        ///<inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            //Only invoke close once
            if (closed)
            {
                return;
            }
            try
            {
                await FlushWriterAsync();
            }
            finally
            {
                //Set closed flag
                closed = true;
                //Release the memory handle if its set
                _buffer.Close();
            }
            GC.SuppressFinalize(this);
        }
    }
}