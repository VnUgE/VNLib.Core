/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: Helpers.cs 
*
* Helpers.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.IO;
using System.Text;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// Contains FBM library helper methods
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// The message-id of a connection control frame / out of band message
        /// </summary>
        public const int CONTROL_FRAME_MID = -500;

        /// <summary>
        /// Gets the default header character encoding instance
        /// </summary>
        public static Encoding DefaultEncoding { get; } = Encoding.UTF8;

        /// <summary>
        /// The FBM protocol header line termination symbols
        /// </summary>
        public static ReadOnlyMemory<byte> Termination { get; } = new byte[] { 0xFF, 0xF1 };

        /// <summary>
        /// Allocates a random integer to use as a message id
        /// </summary>
        public static int RandomMessageId => RandomNumberGenerator.GetInt32(1, int.MaxValue);

        /// <summary>
        /// Parses the header line for a message-id
        /// </summary>
        /// <param name="line">A sequence of bytes that make up a header line</param>
        /// <returns>The message-id if parsed, -1 if message-id is not valid</returns>
        public static int GetMessageId(ReadOnlySpan<byte> line)
        {
            //Make sure the message line is large enough to contain a message-id
            if (line.Length < 1 + sizeof(int))
            {
                return -1;
            }

            //The first byte should be the header id
            HeaderCommand headerId = (HeaderCommand)line[0];

            //Make sure the headerid is set
            if (headerId != HeaderCommand.MessageId)
            {
                return -2;
            }

            //Get the messageid after the header byte
            ReadOnlySpan<byte> messageIdSegment = line.Slice(1, sizeof(int));

            //get the messageid from the messageid segment
            return BinaryPrimitives.ReadInt32BigEndian(messageIdSegment);
        }

        /// <summary>
        /// Appends the message id header to the accumulator 
        /// </summary>
        /// <param name="accumulator">The accumulatore to write the message id to</param>
        /// <param name="messageid">The message id to write to the accumulator</param>
        public static void WriteMessageid(IDataAccumulator<byte> accumulator, int messageid)
        {
            //Alloc buffer for message id + the message id header
            Span<byte> buffer = stackalloc byte[sizeof(int) + 1];

            //Set 1st byte as message id
            buffer[0] = (byte)HeaderCommand.MessageId;

            //Write the message id as a big-endian message
            BinaryPrimitives.WriteInt32BigEndian(buffer[1..], messageid);

            //Write the header and message id + the trailing termination
            accumulator.Append(buffer);

            WriteTermination(accumulator);
        }

        /// <summary>
        /// Gets the remaining data after the current position of the stream.
        /// </summary>
        /// <param name="response">The stream to segment</param>
        /// <returns>The remaining data segment</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> GetRemainingData(VnMemoryStream response)
            => response.AsSpan()[(int)response.Position..];

        /// <summary>
        /// Reads the next available line from the response message
        /// </summary>
        /// <param name="response"></param>
        /// <returns>The read line</returns>
        public static ReadOnlySpan<byte> ReadLine(VnMemoryStream response)
        {
            //Get the span from the current stream position to end of the stream
            ReadOnlySpan<byte> line = GetRemainingData(response);
            //Search for next line termination
            int index = line.IndexOf(Termination.Span);
            if (index == -1)
            {
                return ReadOnlySpan<byte>.Empty;
            }
            //Update stream position to end of termination
            response.Seek(index + Termination.Length, SeekOrigin.Current);
            //slice up line and exclude the termination
            return line[..index];
        }

        /// <summary>
        /// Parses headers from the request stream, stores headers from the buffer into the 
        /// header collection
        /// </summary>
        /// <param name="vms">The FBM packet buffer</param>
        /// <param name="buffer">The header character buffer to write headers to</param>
        /// <param name="headers">The collection to store headers in</param>
        /// <param name="encoding">The encoding type used to deocde header values</param>
        /// <returns>The results of the parse operation</returns>
        internal static HeaderParseError ParseHeaders(VnMemoryStream vms, IFBMHeaderBuffer buffer, ICollection<FBMMessageHeader> headers, Encoding encoding)
        {
            HeaderParseError status = HeaderParseError.None;

            //Get a sliding window writer over the enitre buffer
            ForwardOnlyWriter<char> writer = new(buffer.GetSpan());

            //Accumulate headers
            while (true)
            {
                //Read the next line from the current stream
                ReadOnlySpan<byte> line = ReadLine(vms);

                if (line.IsEmpty)
                {
                    //Done reading headers
                    break;
                }

                //Read the header command from the next line
                HeaderCommand cmd = GetHeaderCommand(line);

                //Write the next header value from the line to the remaining space in the buffer
                ERRNO charsRead = GetHeaderValue(line, writer.Remaining, encoding);

                if (charsRead < 0)
                {
                    //Out of buffer space
                    status |= HeaderParseError.HeaderOutOfMem;
                    break;
                }
                else if (!charsRead)
                {
                    //Invalid header
                    status |= HeaderParseError.InvalidHeaderRead;
                }
                else
                {
                    //Use the writer to capture the offset, and the character size
                    FBMMessageHeader header = new(buffer, cmd, writer.Written, (int)charsRead);

                    //Store header as a read-only sequence
                    headers.Add(header);

                    //Advance the writer
                    writer.Advance(charsRead);
                }
            }
            return status;
        }

        /// <summary>
        /// Gets a <see cref="HeaderCommand"/> enum from the first byte of the message
        /// </summary>
        /// <param name="line"></param>
        /// <returns>The <see cref="HeaderCommand"/> enum value from hte first byte of the message</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HeaderCommand GetHeaderCommand(ReadOnlySpan<byte> line) => (HeaderCommand)line[0];

        /// <summary>
        /// Gets the value of the header following the colon bytes in the specifed
        /// data message data line
        /// </summary>
        /// <param name="line">The message header line to get the value of</param>
        /// <param name="output">The output character buffer to write characters to</param>
        /// <param name="encoding">The encoding to decode the specified data with</param>
        /// <returns>The number of characters encoded or -1 if the output buffer is too small</returns>
        public static ERRNO GetHeaderValue(ReadOnlySpan<byte> line, Span<char> output, Encoding encoding)
        {
            //Get the data following the header byte
            ReadOnlySpan<byte> value = line[1..];

            //Calculate the character account
            int charCount = encoding.GetCharCount(value);

            //Determine if the output buffer is large enough
            return charCount > output.Length
                ? (ERRNO)(-1)
                : (ERRNO)encoding.GetChars(value, output);
        }
       

        /// <summary>
        /// Rounds the requested byte size up to the 1kb 
        /// number of bytes
        /// </summary>
        /// <param name="byteSize">The number of bytes to get the rounded 1kb size of</param>
        /// <returns>The number of bytes equivalent to the requested byte size rounded to the next 1kb size</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint ToNearestKb(nint byteSize)
        {
            //Get page count by dividing count by number of pages
            nint kbs = (nint)Math.Ceiling(byteSize / (double)1024);

            //Multiply back to page sizes
            return kbs * 1024;
        }

        /// <summary>
        /// Writes a line termination to the message buffer
        /// </summary>
        /// <param name="buffer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTermination(IDataAccumulator<byte> buffer)
            => buffer.Append(Termination.Span);

        /// <summary>
        /// Appends an arbitrary header to the current request buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="header">The <see cref="HeaderCommand"/> of the header</param>
        /// <param name="value">The value of the header</param>
        /// <param name="encoding">Encoding to use when writing character message</param>
        /// <exception cref="ArgumentException"></exception>
        public static void WriteHeader(IDataAccumulator<byte> buffer, byte header, ReadOnlySpan<char> value, Encoding encoding)
        {
            if (header == 0)
            {
                throw new ArgumentException("A header command of 0 is illegal", nameof(header));
            }

            //Write header command enum value
            buffer.Append(header);
            //Convert the characters to binary and write to the buffer
            int written = encoding.GetBytes(value, buffer.Remaining);
            //Advance the buffer
            buffer.Advance(written);
            //Write termination (0)
            WriteTermination(buffer);
        }
    }
}
