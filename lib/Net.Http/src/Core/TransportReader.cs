/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: TransportReader.cs 
*
* TransportReader.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Text;
using System.Diagnostics;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core
{

    /// <summary>
    /// Structure implementation of <see cref="IVnTextReader"/>
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="TransportReader"/> for reading text lines from the transport stream
    /// </remarks>
    /// <param name="transport">The transport stream to read data from</param>
    /// <param name="buffer">The shared binary buffer</param>
    /// <param name="encoding">The encoding to use when reading bianry</param>
    /// <param name="lineTermination">The line delimiter to search for</param>
    internal struct TransportReader(Stream transport, IHttpHeaderParseBuffer buffer, Encoding encoding, ReadOnlyMemory<byte> lineTermination) 
        : IVnTextReader
    {
        ///<inheritdoc/>
        public readonly Encoding Encoding => encoding;

        ///<inheritdoc/>
        public readonly ReadOnlyMemory<byte> LineTermination => lineTermination;      
       
        private readonly uint MaxBufferSize = (uint)buffer.BinSize;

        private BufferPosition _position = default;


        /// <summary>
        /// Gets the data segment of the buffer after the private segment
        /// </summary>
        /// <returns></returns>
        private readonly Span<byte> GetDataSegment() 
            => buffer.GetBinSpan((int)_position.WindowStart, (int)_position.GetWindowSize());

        private readonly Span<byte> GetRemainingSegment() => buffer.GetBinSpan((int)_position.WindowEnd);

        ///<inheritdoc/>
        public readonly int Available => (int)_position.GetWindowSize();

        ///<inheritdoc/>
        public readonly Span<byte> BufferedDataWindow => GetDataSegment();

        ///<inheritdoc/>
        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            //Advance the window start by the count and set the position
            _position = _position.AdvanceStart(count);
        }

        /// <summary>
        /// Sets the number of bytes to read from the transport stream
        /// </summary>
        /// <param name="start">The number of bytes to make available within the buffer window</param>
        public void SetAvailableData(int start)
        {
            Debug.Assert(start <= MaxBufferSize, "Stream buffer would overflow");

            _position = _position.AdvanceEnd(start);
        }

        ///<inheritdoc/>
        public void FillBuffer()
        {
            //Read from stream into the remaining buffer segment
            int read = transport.Read(GetRemainingSegment());
            Debug.Assert(read > -1, "Read should never be negative");

            //Update the end of the buffer window to the end of the read data
            _position = _position.AdvanceEnd(read);
        }

        ///<inheritdoc/>
        public ERRNO CompactBufferWindow()
        {
            //store the current size of the window
            uint windowSize = _position.GetWindowSize();

            //No data to compact if window is not shifted away from start
            if (_position.WindowStart > 0)
            {
                //Get a ref to the entire buffer segment, then do an in-place move to shift the data to the start of the buffer
                ref byte ptr = ref buffer.DangerousGetBinRef(0);
                MemoryUtil.Memmove(ref ptr, _position.WindowStart, ref ptr, dstOffset: 0, windowSize);

                /*
                 * Now that data has been shifted, update the position to 
                 * the new window and write the new position to the buffer
                 */
                _position = BufferPosition.Set(0, windowSize);
            }

            //Return the number of bytes of available space from the end of the current window
            return (nint)(MaxBufferSize - windowSize);
        }

        /// <summary>
        /// Releases the remaining internal buffer as an <see cref="TransportBufferRemainder"/> structure
        /// for optimized transport reading
        /// </summary>
        /// <returns>A structure that contains information about the remaining data</returns>
        public readonly TransportBufferRemainder ReleaseBufferRemainder()
        {
            return new TransportBufferRemainder(
                buffer, 
                offset: (int)_position.WindowStart, 
                size: Available
            );
        }

        private readonly record struct BufferPosition
        {
            public readonly uint WindowStart;
            public readonly uint WindowEnd;

            private BufferPosition(uint start, uint end)
            {
                WindowStart = start;
                WindowEnd = end;
            }

            /// <summary>
            /// Sets the the buffer window position
            /// </summary>
            /// <param name="start">Window start</param>
            /// <param name="end">Window end</param>
            public static BufferPosition Set(uint start, uint end) => new(start, end);

            public readonly uint GetWindowSize() => WindowEnd - WindowStart;

            public readonly BufferPosition AdvanceEnd(int count) => new(WindowStart, WindowEnd + (uint)count);

            public readonly BufferPosition AdvanceStart(int count) => new(WindowStart + (uint)count, WindowEnd);
        }
    }
}
