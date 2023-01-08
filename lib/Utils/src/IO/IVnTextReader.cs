/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IVnTextReader.cs 
*
* IVnTextReader.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Represents a streaming text reader with internal buffers
    /// </summary>
    public interface IVnTextReader
    {
        /// <summary>
        /// The base stream to read data from
        /// </summary>
        Stream BaseStream { get; }
        /// <summary>
        /// The character encoding used by the TextReader
        /// </summary>
        Encoding Encoding { get; }
        /// <summary>
        /// Number of available bytes of buffered data within the current buffer window
        /// </summary>
        int Available { get; }
        /// <summary>
        /// Gets or sets the line termination used to deliminate a line of data
        /// </summary>
        ReadOnlyMemory<byte> LineTermination { get; }
        /// <summary>
        /// The unread/available data within the internal buffer
        /// </summary>
        Span<byte> BufferedDataWindow { get; }
        /// <summary>
        /// Shifts the sliding buffer window by the specified number of bytes.
        /// </summary>
        /// <param name="count">The number of bytes read from the buffer</param>
        void Advance(int count);
        /// <summary>
        /// Reads data from the stream into the remaining buffer space for processing
        /// </summary>
        void FillBuffer();
        /// <summary>
        /// Compacts the available buffer space back to the begining of the buffer region
        /// and determines if there is room for more data to be buffered
        /// </summary>
        /// <returns>The remaining buffer space if any</returns>
        ERRNO CompactBufferWindow();
    }
}