/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMBuffer.cs 
*
* FBMBuffer.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using VNLib.Utils.IO;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// Represents a shared internal character and bianry buffer for
    /// </summary>
    internal sealed class FBMBuffer : IDisposable
    {
        private readonly IMemoryOwner<byte> Handle;

        private readonly BufferWriter _writer;
        private readonly BinaryRequestAccumulator _requestAccumulator;


        internal FBMBuffer(IMemoryOwner<byte> handle)
        {
            Handle = handle;
            _writer = new(this);
            _requestAccumulator = new(handle.Memory);
        }


        /// <summary>
        /// Gets the internal request data accumulator
        /// </summary>
        public IDataAccumulator<byte> RequestBuffer => _requestAccumulator;

        /// <summary>
        /// Gets the accumulated request data for reading
        /// </summary>
        /// <returns>The accumulated request data as memory</returns>
        public ReadOnlyMemory<byte> RequestData => _requestAccumulator.AccumulatedMemory;

        /// <summary>
        /// Completes the header segment and prepares the body writer
        /// </summary>
        /// <returns>A <see cref="IBufferWriter{T}"/> for writing an FBM message body to</returns>
        public IBufferWriter<byte> GetBodyWriter()
        {
            //complete the header segments by writing an empty line
            Helpers.WriteTermination(RequestBuffer);

            //Return the internal writer
            return _writer;
        }

        /// <summary>
        /// Gets the buffer manager for managing response headers
        /// </summary>
        /// <returns>The <see cref="FBMHeaderBuffer"/> for managing response header buffers</returns>
        public FBMHeaderBuffer GetResponseHeaderBuffer()
        {
            //Get a buffer wrapper around the memory handle
            return new FBMHeaderBuffer(Handle.Memory);
        }

        public void Dispose()
        {
            //Dispose handle
            Handle.Dispose();
        }

        /// <summary>
        /// Resets the request accumulator and writes the initial message id
        /// </summary>
        /// <param name="messageId">The message id</param>
        public void Reset(int messageId)
        {
            //Reset request header accumulator when complete
            _requestAccumulator.Reset();

            //Write message id to accumulator, it should already be reset
            Helpers.WriteMessageid(RequestBuffer, messageId);
        }

        private sealed class BinaryRequestAccumulator : IDataAccumulator<byte>
        {
            private readonly int Size;
            private readonly Memory<byte> Buffer;

            internal BinaryRequestAccumulator(Memory<byte> buffer)
            {
                Buffer = buffer;
                Size = buffer.Length;
            }

            ///<inheritdoc/>
            public int AccumulatedSize { get; private set; }

            ///<inheritdoc/>
            public int RemainingSize => Size - AccumulatedSize;

            ///<inheritdoc/>
            public Span<byte> Remaining => Buffer.Span.Slice(AccumulatedSize, RemainingSize);
            ///<inheritdoc/>
            public Span<byte> Accumulated => Buffer.Span[..AccumulatedSize];

            /// <summary>
            /// Gets the accumulated data as a memory segment
            /// </summary>
            public Memory<byte> AccumulatedMemory => Buffer[..AccumulatedSize];

            /// <summary>
            /// Gets the remaining buffer segment as a memory segment
            /// </summary>
            public Memory<byte> RemainingMemory => Buffer.Slice(AccumulatedSize, RemainingSize);

            ///<inheritdoc/>
            public void Advance(int count) => AccumulatedSize += count;
            ///<inheritdoc/>
            public void Reset() => AccumulatedSize = 0;
        }

        private sealed class BufferWriter : IBufferWriter<byte>
        {
            private readonly FBMBuffer Buffer;

            public BufferWriter(FBMBuffer buffer)
            {
                Buffer = buffer;
            }

            public void Advance(int count)
            {
                //Advance the writer
                Buffer.RequestBuffer.Advance(count);
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                //Get the remaining memory segment
                return Buffer._requestAccumulator.RemainingMemory;
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                //Get the remaining data segment
                return Buffer.RequestBuffer.Remaining;
            }
        }
    }
}
