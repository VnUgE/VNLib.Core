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
        /*
         * To make this structure read-only we can store the 
         * mutable values in a private segment of the internal
         * buffer. 8 bytes are reserved at the beining and an 
         * additional word is added for padding incase small/wild
         * under/over run occurs.
         */
        const int PrivateBufferOffset = 4 * sizeof(int);

        ///<inheritdoc/>
        public readonly Encoding Encoding { get; }

        ///<inheritdoc/>
        public readonly ReadOnlyMemory<byte> LineTermination { get; }

        ///<inheritdoc/>
        public readonly Stream BaseStream { get; }

        /*
         * Store the window start/end in the begging of the 
         * data buffer. Then use a constant offset to get the
         * start of the buffer
         */
        private readonly int BufWindowStart
        {
            get => MemoryMarshal.Read<int>(Buffer.GetBinSpan());
            set => MemoryMarshal.Write(Buffer.GetBinSpan(), ref value);
        }

        private readonly int BufWindowEnd
        {
            get => MemoryMarshal.Read<int>(Buffer.GetBinSpan()[sizeof(int)..]);
            set => MemoryMarshal.Write(Buffer.GetBinSpan()[sizeof(int)..], ref value);
        }

        private readonly IHttpHeaderParseBuffer Buffer;
        private readonly int MAxBufferSize;

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
            MAxBufferSize = buffer.BinSize - PrivateBufferOffset;

            //Initialize the buffer window
            SafeZeroPrivateSegments(Buffer);

            Debug.Assert(BufWindowEnd == 0 && BufWindowStart == 0);
        }

        /// <summary>
        /// Clears the initial window start/end values with the 
        /// extra padding 
        /// </summary>
        /// <param name="buffer">The buffer segment to initialize</param>
        private static void SafeZeroPrivateSegments(IHttpHeaderParseBuffer buffer)
        {
            ref byte start = ref MemoryMarshal.GetReference(buffer.GetBinSpan());
            Unsafe.InitBlock(ref start, 0, PrivateBufferOffset);
        }

        /// <summary>
        /// Gets the data segment of the buffer after the private segment
        /// </summary>
        /// <returns></returns>
        private readonly Span<byte> GetDataSegment() => Buffer.GetBinSpan()[PrivateBufferOffset..];
        
        ///<inheritdoc/>
        public readonly int Available => BufWindowEnd - BufWindowStart;

        ///<inheritdoc/>
        public readonly Span<byte> BufferedDataWindow => GetDataSegment()[BufWindowStart..BufWindowEnd];
      

        ///<inheritdoc/>
        public readonly void Advance(int count) => BufWindowStart += count;

        ///<inheritdoc/>
        public readonly void FillBuffer()
        {
            //Get a buffer from the end of the current window to the end of the buffer
            Span<byte> bufferWindow = GetDataSegment()[BufWindowEnd..];

            //Read from stream
            int read = BaseStream.Read(bufferWindow);

            //Update the end of the buffer window to the end of the read data
            BufWindowEnd += read;
        }

        ///<inheritdoc/>
        public readonly ERRNO CompactBufferWindow()
        {
            //No data to compact if window is not shifted away from start
            if (BufWindowStart > 0)
            {
                //Get span over engire buffer
                Span<byte> buffer = GetDataSegment();
                
                //Get used data segment within window
                Span<byte> usedData = buffer[BufWindowStart..BufWindowEnd];
                
                //Copy remaining to the begining of the buffer
                usedData.CopyTo(buffer);
                
                //Buffer window start is 0
                BufWindowStart = 0;
                
                //Buffer window end is now the remaining size
                BufWindowEnd = usedData.Length;
            }

            //Return the number of bytes of available space from the end of the current window
            return MAxBufferSize - BufWindowEnd;
        }
    }
}
