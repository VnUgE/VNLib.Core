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

using VNLib.Utils;
using VNLib.Utils.IO;

namespace VNLib.Net.Http.Core
{

    /// <summary>
    /// Structure implementation of <see cref="IVnTextReader"/>
    /// </summary>
    internal struct TransportReader : IVnTextReader
    {
        ///<inheritdoc/>
        public readonly Encoding Encoding { get; }
        ///<inheritdoc/>
        public readonly ReadOnlyMemory<byte> LineTermination { get; }
        ///<inheritdoc/>
        public readonly Stream BaseStream { get; }

        private readonly SharedHeaderReaderBuffer BinBuffer;
      
        private int BufWindowStart;
        private int BufWindowEnd;

        /// <summary>
        /// Initializes a new <see cref="TransportReader"/> for reading text lines from the transport stream
        /// </summary>
        /// <param name="transport">The transport stream to read data from</param>
        /// <param name="buffer">The shared binary buffer</param>
        /// <param name="encoding">The encoding to use when reading bianry</param>
        /// <param name="lineTermination">The line delimiter to search for</param>
        public TransportReader(Stream transport, SharedHeaderReaderBuffer buffer, Encoding encoding, ReadOnlyMemory<byte> lineTermination)
        {
            BufWindowEnd = 0;
            BufWindowStart = 0;
            Encoding = encoding;
            BaseStream = transport;
            LineTermination = lineTermination;
            BinBuffer = buffer;
        }
        
        ///<inheritdoc/>
        public readonly int Available => BufWindowEnd - BufWindowStart;

        ///<inheritdoc/>
        public readonly Span<byte> BufferedDataWindow => BinBuffer.BinBuffer[BufWindowStart..BufWindowEnd];
      

        ///<inheritdoc/>
        public void Advance(int count) => BufWindowStart += count;
        ///<inheritdoc/>
        public void FillBuffer()
        {
            //Get a buffer from the end of the current window to the end of the buffer
            Span<byte> bufferWindow = BinBuffer.BinBuffer[BufWindowEnd..];
            //Read from stream
            int read = BaseStream.Read(bufferWindow);
            //Update the end of the buffer window to the end of the read data
            BufWindowEnd += read;
        }
        ///<inheritdoc/>
        public ERRNO CompactBufferWindow()
        {
            //No data to compact if window is not shifted away from start
            if (BufWindowStart > 0)
            {
                //Get span over engire buffer
                Span<byte> buffer = BinBuffer.BinBuffer;
                //Get data within window
                Span<byte> usedData = buffer[BufWindowStart..BufWindowEnd];
                //Copy remaining to the begining of the buffer
                usedData.CopyTo(buffer);
                //Buffer window start is 0
                BufWindowStart = 0;
                //Buffer window end is now the remaining size
                BufWindowEnd = usedData.Length;
            }
            //Return the number of bytes of available space
            return BinBuffer.BinLength - BufWindowEnd;
        }
    }
}
