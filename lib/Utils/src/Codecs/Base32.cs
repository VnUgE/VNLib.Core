/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: Base32.cs 
*
* Base32.cs is part of VNLib.Utils which is part of the larger 
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;

namespace VNLib.Utils.Codecs
{
    /// <summary>
    /// Contains Base32 encoding utility functions focused on performance
    /// optimized encoding and decoding. 
    /// </summary>
    public static class Base32
    {
        #region Base32

        private const string RFC_4648_BASE32_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetCharCode(char c)
        {
            //cast to byte to get its base 10 value
            return c switch
            {
                //Upper case
                'A' => 0,
                'B' => 1,
                'C' => 2,
                'D' => 3,
                'E' => 4,
                'F' => 5,
                'G' => 6,
                'H' => 7,
                'I' => 8,
                'J' => 9,
                'K' => 10,
                'L' => 11,
                'M' => 12,
                'N' => 13,
                'O' => 14,
                'P' => 15,
                'Q' => 16,
                'R' => 17,
                'S' => 18,
                'T' => 19,
                'U' => 20,
                'V' => 21,
                'W' => 22,
                'X' => 23,
                'Y' => 24,
                'Z' => 25,
                //Lower case
                'a' => 0,
                'b' => 1,
                'c' => 2,
                'd' => 3,
                'e' => 4,
                'f' => 5,
                'g' => 6,
                'h' => 7,
                'i' => 8,
                'j' => 9,
                'k' => 10,
                'l' => 11,
                'm' => 12,
                'n' => 13,
                'o' => 14,
                'p' => 15,
                'q' => 16,
                'r' => 17,
                's' => 18,
                't' => 19,
                'u' => 20,
                'v' => 21,
                'w' => 22,
                'x' => 23,
                'y' => 24,
                'z' => 25,
                //Base10 digits
                '2' => 26,
                '3' => 27,
                '4' => 28,
                '5' => 29,
                '6' => 30,
                '7' => 31,

                _ => throw new FormatException("Character found is not a Base32 encoded character")
            };
        }

        private unsafe static void WriteChars(ReadOnlySpan<byte> input, ref ForwardOnlyWriter<char> writer)
        {
            //Get the input buffer as long 
            ulong inputAsLong = 0;

            //Get a byte pointer over the ulong to index it as a byte buffer
            byte* buffer = (byte*)&inputAsLong;

            //Check proc endianness
            if (BitConverter.IsLittleEndian)
            {
                //store each byte consecutively and allow for padding
                for (int i = 0; i < 5 && i < input.Length; i++)
                {
                    //Write bytes from upper to lower byte order for little endian systems
                    buffer[7 - i] = input[i];
                }
            }
            else
            {
                //store each byte consecutively and allow for padding
                for (int i = 0; i < 5 && i < input.Length; i++)
                {
                    //Write bytes from lower to upper byte order for Big Endian systems
                    buffer[i] = input[i];
                }
            }

            /*
             * We need to determine how many bytes can be encoded
             * and if padding needs to be added
             */

            int rounds = input.Length switch
            {
                1 => 2,
                2 => 4,
                3 => 5,
                4 => 7,
                _ => 8
            };

            //Convert each byte segment up to the number of bytes encoded
            for (int i = 0; i < rounds; i++)
            {
                //store the leading byte
                byte val = buffer[7];

                //right shift the value to lower 5 bits
                val >>= 3;

                //append the character to the writer
                writer.Append(RFC_4648_BASE32_ALPHABET[val]);

                //Shift input left by 5 bits so the next 5 bits can be read
                inputAsLong <<= 5;
            }

            //Fill remaining bytes with padding chars
            for (; rounds < 8; rounds++)
            {
                //Append trailing '=' padding character
                writer.Append('=');
            }
        }

        /// <summary>
        /// Gets the size of the buffer required to decode a base32 encoded 
        /// string. This buffer size will always be smaller than the input size.
        /// </summary>
        /// <param name="inputSize">The base32 encoded data input size</param>
        /// <returns>The size of the output buffer needed to write decoded data to</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint GetDecodedSize(nint inputSize) => inputSize * 5 / 8;

        /*
         * Base32 encoding consumes 8 bytes for every 5 bytes
         * of input data
         */

        /// <summary>
        /// Calculates the maximum buffer size required to encode a binary block to its Base32
        /// character encoding
        /// </summary>
        /// <param name="bufferSize">The binary buffer size used to calculate the base32 buffer size</param>
        /// <returns>The maximum size (including padding) of the character buffer required to encode the binary data</returns>
        public static int GetMaxBufferSize(int bufferSize)
            => (int)(Math.Ceiling(bufferSize / 5d) * 8) + (8 - bufferSize % 8);

        /// <summary>
        /// Encodes the specified byte sequence in Base32 encoding 
        /// and writing the encoded data to the output buffer.
        /// </summary>
        /// <param name="input">The input buffer to convert</param>
        /// <param name="output">The ouput buffer to write encoded data to</param>
        /// <returns>The number of characters written, false if no data was written or output buffer was too small</returns>
        public static ERRNO Encode(ReadOnlySpan<byte> input, Span<char> output)
        {
            ForwardOnlyWriter<char> writer = new(output);
            return Encode(input, ref writer);
        }

        /// <summary>
        /// Encodes the specified byte sequence in Base32 encoding 
        /// and writing the encoded data to the output buffer.
        /// </summary>
        /// <param name="input">The input buffer to convert</param>
        /// <param name="writer">A <see cref="ForwardOnlyWriter{T}"/> to write encoded chars to</param>
        /// <returns>The number of characters written, false if no data was written or output buffer was too small</returns>
        public static ERRNO Encode(ReadOnlySpan<byte> input, ref ForwardOnlyWriter<char> writer)
        {
            //calculate char size
            int charCount = (int)Math.Ceiling(input.Length / 5d) * 8;

            //Make sure there is enough room
            if (charCount > writer.RemainingSize)
            {
                return false;
            }

            //sliding window over input buffer
            ForwardOnlyReader<byte> reader = new(input);

            while (reader.WindowSize > 0)
            {
                //Convert the current window
                WriteChars(reader.Window, ref writer);

                //shift the window
                reader.Advance(Math.Min(5, reader.WindowSize));
            }
            return writer.Written;
        }

        /// <summary>
        /// Encodes the binary buffer to a base32 character string with optional padding characters
        /// </summary>
        /// <param name="binBuffer">The buffer to encode</param>
        /// <param name="includePadding">Should padding be included in the result</param>
        /// <returns>The base32 encoded string representation of the specified buffer</returns>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static string Encode(ReadOnlySpan<byte> binBuffer, bool includePadding)
        {
            //Calculate the base32 entropy to alloc an appropriate buffer (minium buffer of 2 chars)
            int entropy = GetMaxBufferSize(binBuffer.Length);

            using UnsafeMemoryHandle<char> charBuffer = MemoryUtil.UnsafeAlloc<char>(entropy);

            //Encode
            ERRNO encoded = Encode(binBuffer, charBuffer.Span);

            if (!encoded)
            {
                throw new InternalBufferTooSmallException("Base32 char buffer was too small");
            }

            //Convert with or w/o padding
            return includePadding
                ? charBuffer.Span[0..(int)encoded].ToString()
                : charBuffer.Span[0..(int)encoded].Trim('=').ToString();
        }

        /// <summary>
        /// Attempts to decode the Base32 encoded string
        /// </summary>
        /// <param name="input">The Base32 encoded data to decode</param>
        /// <param name="writer">A <see cref="ForwardOnlyWriter{T}"/> to write decoded bytes to</param>
        /// <returns>The number of bytes written to the output</returns>
        /// <exception cref="FormatException"></exception>
        public unsafe static ERRNO Decode(ReadOnlySpan<char> input, ref ForwardOnlyWriter<byte> writer)
        {
            //TODO support Big-Endian byte order

            int count = 0;
            ulong bufferLong = 0;                   //buffer used to shift data while decoding
            byte* buffer = (byte*)&bufferLong;      //re-cast to byte* to use it as a byte buffer

            //trim padding characters
            input = input.Trim('=');

            //Calc the number of bytes to write
            nint outputSize = GetDecodedSize(input.Length);

            //make sure the output buffer is large enough
            if (writer.RemainingSize < outputSize)
            {
                return false;
            }

            while (count < input.Length)
            {
                /*
                 * Attempts to accumulate 8 bytes from the input buffer
                 * and write it from hi-lo byte order to the output buffer
                 * 
                 * The underlying 64-bit integer is shifted left by 5 bits
                 * on every loop, removing leading zero bits. The OR operation
                 * ignores the zeros when the next byte is written, and anything 
                 * leading is shifted off the end when 8 bytes are written.
                 * 
                 * Remember: each character only contains 5 bits of useful data
                 */

                buffer[0] |= GetCharCode(input[count]);

                count++;

                //If 8 characters have been decoded, reset the buffer
                if (count % 8 == 0)
                {
                    //Write the 5 upper bytes in reverse order to the output buffer
                    for (int j = 0; j < 5; j++)
                    {
                        writer.Append(buffer[4 - j]);
                    }

                    bufferLong = 0;
                }

                //left shift the buffer up by 5 bits, because thats all we 
                bufferLong <<= 5;
            }

            //If remaining data has not be written, but has been buffered, finalize it
            if (writer.Written < outputSize)
            {
                //calculate how many bits the buffer still needs to be shifted by (will be 5 bits off because of the previous loop)
                int remainingShift = (7 - count % 8) * 5;

                //right shift the buffer by the remaining bit count
                bufferLong <<= remainingShift;

                //calc remaining bytes
                nint remaining = outputSize - writer.Written;

                //Write remaining bytes to the output
                for (int i = 0; i < remaining; i++)
                {
                    writer.Append(buffer[4 - i]);
                }
            }
            return writer.Written;
        }

        /// <summary>
        /// Attempts to decode the Base32 encoded string
        /// </summary>
        /// <param name="input">The Base32 encoded data to decode</param>
        /// <param name="output">The output buffer to write decoded data to</param>
        /// <returns>The number of bytes written to the output</returns>
        /// <exception cref="FormatException"></exception>
        public static ERRNO Decode(ReadOnlySpan<char> input, Span<byte> output)
        {
            ForwardOnlyWriter<byte> writer = new(output);
            return Decode(input, ref writer);
        }

        /// <summary>
        /// Gets a byte array of the base32 decoded data
        /// </summary>
        /// <param name="base32">The character array to decode</param>
        /// <returns>The byte[] of the decoded binary data, or null if the supplied character array was empty</returns>
        public static byte[]? Decode(ReadOnlySpan<char> base32)
        {
            if (base32.IsEmpty)
            {
                return null;
            }

            //Buffer size of the base32 string will always be enough buffer space
            using UnsafeMemoryHandle<byte> tempBuffer = MemoryUtil.UnsafeAlloc(base32.Length);

            //Try to decode the data
            ERRNO decoded = Decode(base32, tempBuffer.Span);
            Debug.Assert(decoded > 0, "The supplied base32 buffer was too small to decode data into, but should not have been");

            return tempBuffer.Span[0..(int)decoded].ToArray();
        }            

        /// <summary>
        /// Converts the base32 character buffer to its structure representation
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="base32">The base32 character buffer</param>
        /// <returns>The new structure of the base32 data</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static T Deserialize<T>(ReadOnlySpan<char> base32) where T : unmanaged
        {
            //calc size of bin buffer
            int size = base32.Length;

            using UnsafeMemoryHandle<byte> binBuffer = MemoryUtil.UnsafeAlloc(size);

            ERRNO decoded = Decode(base32, binBuffer.Span);

            return decoded
                ? MemoryMarshal.Read<T>(binBuffer.Span[..(int)decoded])
                : throw new InternalBufferTooSmallException("Binbuffer was too small");
        }

        /// <summary>
        /// Converts a structure to its base32 representation and returns the string of its value
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="value">The structure to encode</param>
        /// <param name="includePadding">A value indicating if padding should be used</param>
        /// <returns>The base32 string representation of the structure</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static string Serialize<T>(T value, bool includePadding = false) where T : unmanaged
        {
            //get the size of the structure
            int binSize = Unsafe.SizeOf<T>();

            Span<byte> binBuffer = stackalloc byte[binSize];

            MemoryMarshal.Write(binBuffer, in value);

            return Encode(binBuffer, includePadding);
        }

        /// <summary>
        /// Converts a structure to its base32 representation and writes the characters
        /// to the supplied output buffer.
        /// </summary>
        /// <typeparam name="T">The unmanaged structure type</typeparam>
        /// <param name="value">The structure to encode</param>
        /// <param name="output">The character output buffer to write the base32 encoded data to</param>
        /// <returns>The number of characters written, or false if the output buffer was too small</returns>
        public static ERRNO Serialize<T>(T value, Span<char> output) where T : unmanaged
        {
            //get the size of the structure
            int binSize = Unsafe.SizeOf<T>();

            Span<byte> binBuffer = stackalloc byte[binSize];

            MemoryMarshal.Write(binBuffer, in value);

            return Encode(binBuffer, output);
        }

        #endregion
    }
}
