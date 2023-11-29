/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Net.Http.Core.Buffering;

namespace VNLib.Net.Http.Core
{

    /// <summary>
    /// Structure implementation of <see cref="IVnTextReader"/>
    /// </summary>
    internal readonly struct TransportReader : IVnTextReader
    {
        private readonly static int BufferPosStructSize = Unsafe.SizeOf<BufferPosition>();

        ///<inheritdoc/>
        public readonly Encoding Encoding { get; }

        ///<inheritdoc/>
        public readonly ReadOnlyMemory<byte> LineTermination { get; }

        ///<inheritdoc/>
        public readonly Stream BaseStream { get; }
      

        private readonly IHttpHeaderParseBuffer Buffer;
        private readonly uint MaxBufferSize;

        /// <summary>
        /// Initializes a new <see cref="TransportReader"/> for reading text lines from the transport stream
        /// </summary>
        /// <param name="transport">The transport stream to read data from</param>
        /// <param name="buffer">The shared binary buffer</param>
        /// <param name="encoding">The encoding to use when reading bianry</param>
        /// <param name="lineTermination">The line delimiter to search for</param>
        public TransportReader(Stream transport, IHttpHeaderParseBuffer buffer, Encoding encoding, ReadOnlyMemory<byte> lineTermination)
        {
            Encoding = encoding;
            BaseStream = transport;
            LineTermination = lineTermination;
            Buffer = buffer;
            MaxBufferSize = (uint)(buffer.BinSize - BufferPosStructSize);

            //Assign an zeroed position
            BufferPosition position = default;
            SetPosition(ref position);

            AssertZeroPosition();
        }

        [Conditional("DEBUG")]
        private void AssertZeroPosition()
        {
            BufferPosition position = default;
            GetPosition(ref position);
            Debug.Assert(position.WindowStart == 0);
            Debug.Assert(position.WindowEnd == 0);
        }

        /// <summary>
        /// Reads the current position from the buffer segment
        /// </summary>
        /// <param name="position">A reference to the varable to write the position to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void GetPosition(ref BufferPosition position)
        {
            //Get the beining of the segment and read the position
            Span<byte> span = Buffer.GetBinSpan();
            position = MemoryMarshal.Read<BufferPosition>(span);
        }

        /// <summary>
        /// Updates the current position in the buffer segment
        /// </summary>
        /// <param name="position"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void SetPosition(ref BufferPosition position)
        {
            //Store the position at the beining of the segment
            Span<byte> span = Buffer.GetBinSpan();
            MemoryMarshal.Write(span, ref position);
        }

        /// <summary>
        /// Gets the data segment of the buffer after the private segment
        /// </summary>
        /// <returns></returns>
        private readonly Span<byte> GetDataSegment()
        {
            //Get the beining of the segment
            Span<byte> span = Buffer.GetBinSpan();
            //Return the segment after the private segment
            return span[BufferPosStructSize..];
        }
        
        ///<inheritdoc/>
        public readonly int Available
        {
            get
            {
                //Read position and return the window size
                BufferPosition position = default;
                GetPosition(ref position);
                return (int)position.GetWindowSize();
            }
        }

        ///<inheritdoc/>
        public readonly Span<byte> BufferedDataWindow
        {
            get
            {
                //Read current position and return the window
                BufferPosition position = default;
                GetPosition(ref position);
                return GetDataSegment()[(int)position.WindowStart..(int)position.WindowEnd];
            }
        }      

        ///<inheritdoc/>
        public readonly void Advance(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive");
            }

            //read the current position
            BufferPosition position = default;            
            GetPosition(ref position);

            //Advance the window start by the count and set the position
            position.AdvanceStart(count);
            SetPosition(ref position);
        }

        ///<inheritdoc/>
        public readonly void FillBuffer()
        {
            //Read the current position
            BufferPosition bufferPosition = default;
            GetPosition(ref bufferPosition);

            //Get a buffer from the end of the current window to the end of the buffer
            Span<byte> bufferWindow = GetDataSegment()[(int)bufferPosition.WindowEnd..];

            //Read from stream
            int read = BaseStream.Read(bufferWindow);
            Debug.Assert(read > -1, "Read should never be negative");

            //Update the end of the buffer window to the end of the read data
            bufferPosition.AdvanceEnd(read);
            SetPosition(ref bufferPosition);
        }

        ///<inheritdoc/>
        public readonly ERRNO CompactBufferWindow()
        {
            //Read the current position
            BufferPosition bufferPosition = default;
            GetPosition(ref bufferPosition);

            //No data to compact if window is not shifted away from start
            if (bufferPosition.WindowStart > 0)
            {
                //Get span over engire buffer
                Span<byte> buffer = GetDataSegment();
                
                //Get used data segment within window
                Span<byte> usedData = buffer[(int)bufferPosition.WindowStart..(int)bufferPosition.WindowEnd];
                
                //Copy remaining to the begining of the buffer
                usedData.CopyTo(buffer);
              
                /*
                 * Now that data has been shifted, update the position to 
                 * the new window and write the new position to the buffer
                 */
                bufferPosition.Set(0, usedData.Length);
                SetPosition(ref bufferPosition);
            }

            //Return the number of bytes of available space from the end of the current window
            return (nint)(MaxBufferSize - bufferPosition.WindowEnd);
        }

        [StructLayout(LayoutKind.Sequential)]
        private record struct BufferPosition
        {
            public uint WindowStart;
            public uint WindowEnd;
           
            /// <summary>
            /// Sets the the buffer window position
            /// </summary>
            /// <param name="start">Window start</param>
            /// <param name="end">Window end</param>
            public void Set(int start, int end)
            {
                //Verify that the start and end are not negative
                Debug.Assert(start >= 0, "Negative internal value passed to http buffer window start");
                Debug.Assert(end >= 0, "Negative internal value passed to http buffer window end");

                WindowStart = (uint)start;
                WindowEnd = (uint)end;
            }

            public readonly uint GetWindowSize() => WindowEnd - WindowStart;

            public void AdvanceEnd(int count) => WindowEnd += (uint)count;

            public void AdvanceStart(int count) => WindowStart += (uint)count;
        }
    }
}
