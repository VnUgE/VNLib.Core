/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnTextReaderExtensions.cs 
*
* VnTextReaderExtensions.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Extensions;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Extension methods to help reuse code for used TextReader implementations
    /// </summary>
    public static class VnTextReaderExtensions
    {
        public const int E_BUFFER_TOO_SMALL = -1;


        /*
         * Generic extensions provide constained compiler method invocation
         *  for structs the implement the IVNtextReader
         */
        
        /// <summary>
        /// Attempts to read a line from the stream and store it in the specified buffer
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="charBuffer">The character buffer to write data to</param>
        /// <returns>Returns the number of bytes read, <see cref="E_BUFFER_TOO_SMALL"/> 
        /// if the buffer was not large enough, 0 if no data was available</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <remarks>Allows reading lines of data from the stream without allocations</remarks>
        public static ERRNO ReadLine<T>(this ref T reader, Span<char> charBuffer) where T:struct, IVnTextReader
        {
            return ReadLineInternal(ref reader, charBuffer);
        }

        /// <summary>
        /// Attempts to read a line from the stream and store it in the specified buffer
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="charBuffer">The character buffer to write data to</param>
        /// <returns>Returns the number of bytes read, <see cref="E_BUFFER_TOO_SMALL"/> 
        /// if the buffer was not large enough, 0 if no data was available</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <remarks>Allows reading lines of data from the stream without allocations</remarks>
        public static ERRNO ReadLine<T>(this T reader, Span<char> charBuffer) where T : class, IVnTextReader
        {
            return ReadLineInternal(ref reader, charBuffer);
        }

        /// <summary>
        /// Fill a buffer with reamining buffered data 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="buffer">Buffer to copy data to</param>
        /// <param name="offset">Offset in buffer to begin writing</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>The number of bytes copied to the input buffer</returns>
        public static int ReadRemaining<T>(this ref T reader, byte[] buffer, int offset, int count) where T : struct, IVnTextReader
        {
            return reader.ReadRemaining(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Fill a buffer with reamining buffered data 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="buffer">Buffer to copy data to</param>
        /// <param name="offset">Offset in buffer to begin writing</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>The number of bytes copied to the input buffer</returns>
        public static int ReadRemaining<T>(this T reader, byte[] buffer, int offset, int count) where T : class, IVnTextReader
        {
            return reader.ReadRemaining(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Fill a buffer with reamining buffered data, up to 
        /// the size of the supplied buffer
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="buffer">Buffer to copy data to</param>
        /// <returns>The number of bytes copied to the input buffer</returns>
        /// <remarks>You should use the <see cref="IVnTextReader.Available"/> property to know how much remaining data is buffered</remarks>
        public static int ReadRemaining<T>(this ref T reader, Span<byte> buffer) where T : struct, IVnTextReader
        {
            return ReadRemainingInternal(ref reader, buffer);
        }

        /// <summary>
        /// Fill a buffer with reamining buffered data, up to 
        /// the size of the supplied buffer
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="buffer">Buffer to copy data to</param>
        /// <returns>The number of bytes copied to the input buffer</returns>
        /// <remarks>You should use the <see cref="IVnTextReader.Available"/> property to know how much remaining data is buffered</remarks>
        public static int ReadRemaining<T>(this T reader, Span<byte> buffer) where T : class, IVnTextReader
        {
            return ReadRemainingInternal(ref reader, buffer);
        }
       
        private static ERRNO ReadLineInternal<T>(ref T reader, Span<char> chars) where T: IVnTextReader
        {
            /*
            *  I am aware of a potential bug, the line decoding process
            *  shifts the interal buffer by the exact number of bytes to 
            *  the end of the line, without considering if the decoder failed
            *  to properly decode the entire line.
            *  
            *  I dont expect this to be an issue unless there is a bug within the specified 
            *  encoder implementation
            */
           
            int result = 0;

            //If buffered data is available, check for line termination
            if (reader.Available > 0 && TryReadLine(ref reader, chars, ref result))
            {
                return result;
            }

            //Compact the buffer window and make sure it was compacted so there is room to fill the buffer
            if (reader.CompactBufferWindow() > 0)
            {
                //There is room, so buffer more data
                reader.FillBuffer();

                //Check again to see if more data is buffered
                if (reader.Available <= 0)
                {
                    //No data avialable
                    return 0;
                }

                //Try to read the line again after refill
                if (TryReadLine(ref reader, chars, ref result))
                {
                    return result;
                }
            }

            //Termination not found within the entire buffer, so buffer space has been exhausted

            //Supress as this response is expected when the buffer is exhausted, 
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new OutOfMemoryException("The line was not found within the current buffer, cannot continue");
#pragma warning restore CA2201 // Do not raise reserved exception types
        }

        private static bool TryReadLine<T>(ref T reader, Span<char> chars, ref int result) where T: IVnTextReader
        {
            ReadOnlySpan<byte> LineTermination = reader.LineTermination.Span;

            //Get current buffer window
            ReadOnlySpan<byte> bytes = reader.BufferedDataWindow;

            //search for line termination in current buffer
            int term = bytes.IndexOf(LineTermination);

            //Termination found in buffer window
            if (term > -1)
            {
                //Capture the line from the begining of the window to the termination
                ReadOnlySpan<byte> line = bytes[..term];

                //Get the number ot chars 
                result = reader.Encoding.GetCharCount(line);

                //See if the buffer is large enough
                if (bytes.Length < result)
                {
                    result = E_BUFFER_TOO_SMALL;
                    return true;
                }

                //Use the decoder to convert the data
                _ = reader.Encoding.GetChars(line, chars);

                //Shift the window to the end of the line (excluding the termination, regardless of the conversion result)
                reader.Advance(term + LineTermination.Length);

                //Return the number of characters
                return true;
            }

            return false;
        }
        
        private static int ReadRemainingInternal<T>(ref T reader, Span<byte> buffer) where T: IVnTextReader
        {
            //guard for empty buffer
            if (buffer.Length == 0 || reader.Available == 0)
            {
                return 0;
            }

            //get the remaining bytes in the reader
            Span<byte> remaining = reader.BufferedDataWindow;

            //Calculate the number of bytes to copy
            int canCopy = Math.Min(remaining.Length, buffer.Length);

            //Copy remaining bytes to buffer
            remaining[..canCopy].CopyTo(buffer);

            //Shift the window by the number of bytes copied
            reader.Advance(canCopy);
            return canCopy;
        }
    }
}