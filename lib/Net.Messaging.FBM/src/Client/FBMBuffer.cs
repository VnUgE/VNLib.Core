/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils.IO;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// Represents a shared internal character and bianry buffer for
    /// </summary>
    internal sealed class FBMBuffer : IFBMHeaderBuffer, IDisposable, IReusable
    {
        private readonly IFBMMemoryHandle Handle;
        private readonly IFBMMemoryManager _memoryManager;
        private readonly int _size;

        private readonly BufferWriter _writer;
        private readonly BinaryRequestAccumulator _requestAccumulator;


        internal FBMBuffer(IFBMMemoryManager manager, int bufferSize)
        {
            _memoryManager = manager;
            Handle = manager.InitHandle();
            _writer = new(this);
            _size = bufferSize;
            _requestAccumulator = new(this, bufferSize);
        }


        /*
         * Reusable methods will alloc and free buffers during 
         * normal operation. 
         */

        ///<inheritdoc/>
        public void Prepare() => _memoryManager.AllocBuffer(Handle, _size);

        ///<inheritdoc/>
        public bool Release()
        {
            _memoryManager.FreeBuffer(Handle);
            return true;
        }

        public void Dispose() => _ = Release();

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
        /// Resets the request accumulator and writes the initial message id
        /// </summary>
        /// <param name="messageId">The message id</param>
        public void Reset(int messageId)
        {
            //Reset request header accumulator when complete
            _requestAccumulator.Reset();

            //Write message id to accumulator, it should already be reset
            Helpers.WriteMessageid(_requestAccumulator, messageId);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Span<char> IFBMHeaderBuffer.GetSpan(int offset, int count)
        {
            //Get the character span
            Span<char> span = MemoryMarshal.Cast<byte, char>(Handle.GetSpan());
            return span.Slice(offset, count);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Span<char> IFBMHeaderBuffer.GetSpan() => MemoryMarshal.Cast<byte, char>(Handle.GetSpan());


        private sealed class BinaryRequestAccumulator : IDataAccumulator<byte>
        {
            private readonly int Size;
            private readonly FBMBuffer Buffer;

            internal BinaryRequestAccumulator(FBMBuffer buffer, int size)
            {
                Buffer = buffer;
                Size = size;
            }

            ///<inheritdoc/>
            public int AccumulatedSize { get; private set; }

            ///<inheritdoc/>
            public int RemainingSize => Size - AccumulatedSize;

            ///<inheritdoc/>
            public Span<byte> Remaining => Buffer.Handle.GetSpan().Slice(AccumulatedSize, RemainingSize);

            ///<inheritdoc/>
            public Span<byte> Accumulated => Buffer.Handle.GetSpan()[..AccumulatedSize];

            /// <summary>
            /// Gets the accumulated data as a memory segment
            /// </summary>
            public Memory<byte> AccumulatedMemory => Buffer.Handle.GetMemory()[..AccumulatedSize];

            /// <summary>
            /// Gets the remaining buffer segment as a memory segment
            /// </summary>
            public Memory<byte> RemainingMemory => Buffer.Handle.GetMemory().Slice(AccumulatedSize, RemainingSize);

            ///<inheritdoc/>
            public void Advance(int count) => AccumulatedSize += count;

            ///<inheritdoc/>
            public void Reset() => AccumulatedSize = 0;
        }

        /*
         * A buffer writer that wraps the request accumulator to allow 
         * a finite amount of data to be written to the accumulator since
         * it uses a fixed size internal buffer.
         */
        private sealed class BufferWriter : IBufferWriter<byte>
        {
            private readonly FBMBuffer Buffer;

            public BufferWriter(FBMBuffer buffer) => Buffer = buffer;

            ///<inheritdoc/>
            public void Advance(int count) => Buffer._requestAccumulator.Advance(count);

            ///<inheritdoc/>
            public Memory<byte> GetMemory(int sizeHint = 0) => Buffer._requestAccumulator.RemainingMemory;

            ///<inheritdoc/>
            public Span<byte> GetSpan(int sizeHint = 0) => Buffer._requestAccumulator.Remaining;
        }
    }
}
