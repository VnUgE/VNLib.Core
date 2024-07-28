/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnEncoding.cs 
*
* VnEncoding.cs is part of VNLib.Utils which is part of the larger 
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
using System.Buffers;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;
using System.Buffers.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Utils
{
    /// <summary>
    /// Contains static methods for encoding data
    /// </summary>
    public static class VnEncoding
    {

        /// <summary>
        /// Encodes a <see cref="ReadOnlySpan{T}"/> with the specified <see cref="Encoding"/> to a <see cref="VnMemoryStream"/> that must be disposed by the user
        /// </summary>
        /// <param name="data">Data to be encoded</param>
        /// <param name="encoding"><see cref="Encoding"/> to encode data with</param>
        /// <param name="heap">Heap to allocate memory from</param>
        /// <returns>A <see cref="Stream"/> contating the encoded data</returns>
        public static VnMemoryStream GetMemoryStream(ReadOnlySpan<char> data, Encoding encoding, IUnmangedHeap? heap = null)
        {
            ArgumentNullException.ThrowIfNull(encoding);

            //Assign default heap if not specified
            heap ??= MemoryUtil.Shared;
            
            //Create new memory handle to copy data to
            MemoryHandle<byte>? handle = null;
            try
            {
                //get number of bytes
                int byteCount = encoding.GetByteCount(data);
                //resize the handle to fit the data
                handle = heap.Alloc<byte>(byteCount);
                //encode
                int size = encoding.GetBytes(data, handle.Span);
                //Consume the handle into a new vnmemstream and return it
                return VnMemoryStream.ConsumeHandle(handle, size, true);
            }
            catch
            {
                //Dispose the handle if there is an excpetion
                handle?.Dispose();
                throw;
            }
        }
       
        /// <summary>
        /// Attempts to deserialze a json object from a stream of UTF8 data
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize</typeparam>
        /// <param name="data">Binary data to read from</param>
        /// <param name="options"><see cref="JsonSerializerOptions"/> object to pass to deserializer</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The object decoded from the stream</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static ValueTask<T?> JSONDeserializeFromBinaryAsync<T>(
            Stream? data, 
            JsonSerializerOptions? options = null, 
            CancellationToken cancellationToken = default
        )
        {
            //Return default if null
            return data == null || data.Length == 0 
                ? ValueTask.FromResult<T?>(default) 
                : JsonSerializer.DeserializeAsync<T>(data, options, cancellationToken);
        }      

        #region Base32
        
        private const string RFC_4648_BASE32_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>
        /// Attempts to convert the specified byte sequence in Base32 encoding 
        /// and writing the encoded data to the output buffer.
        /// </summary>
        /// <param name="input">The input buffer to convert</param>
        /// <param name="output">The ouput buffer to write encoded data to</param>
        /// <returns>The number of characters written, false if no data was written or output buffer was too small</returns>
        public static ERRNO TryToBase32Chars(ReadOnlySpan<byte> input, Span<char> output)
        {
            ForwardOnlyWriter<char> writer = new(output);
            return TryToBase32Chars(input, ref writer);
        }

        /// <summary>
        /// Attempts to convert the specified byte sequence in Base32 encoding 
        /// and writing the encoded data to the output buffer.
        /// </summary>
        /// <param name="input">The input buffer to convert</param>
        /// <param name="writer">A <see cref="ForwardOnlyWriter{T}"/> to write encoded chars to</param>
        /// <returns>The number of characters written, false if no data was written or output buffer was too small</returns>
        public static ERRNO TryToBase32Chars(ReadOnlySpan<byte> input, ref ForwardOnlyWriter<char> writer)
        {
            //calculate char size
            int charCount = (int)Math.Ceiling(input.Length / 5d) * 8;
            
            //Make sure there is enough room
            if(charCount > writer.RemainingSize)
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

        private unsafe static void WriteChars(ReadOnlySpan<byte> input, ref ForwardOnlyWriter<char> writer)
        {
            //Get the input buffer as long 
            ulong inputAsLong = 0;

            //Get a byte pointer over the ulong to index it as a byte buffer
            byte* buffer = (byte*)&inputAsLong;

            //Check proc endianness
            if (BitConverter.IsLittleEndian)
            {
                //store each byte consecutivley and allow for padding
                for (int i = 0; (i < 5 && i < input.Length); i++)
                {
                    //Write bytes from upper to lower byte order for little endian systems
                    buffer[7 - i] = input[i];
                }
            }
            else
            {
                //store each byte consecutivley and allow for padding
                for (int i = 0; (i < 5 && i < input.Length); i++)
                {
                    //Write bytes from lower to upper byte order for Big Endian systems
                    buffer[i] = input[i];
                }
            }
            
            /*
             * We need to determine how many bytes can be encoded
             * and if padding needs to be added
             */

            int rounds = (input.Length) switch
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
                writer.Append(RFC_4648_BASE32_CHARS[val]);

                //Shift input left by 5 bits so the next 5 bits can be read
                inputAsLong <<= 5;
            }

            //Fill remaining bytes with padding chars
            for(; rounds < 8; rounds++)
            {
                //Append trailing '=' padding character
                writer.Append('=');
            }
        }
      
        /// <summary>
        /// Attempts to decode the Base32 encoded string
        /// </summary>
        /// <param name="input">The Base32 encoded data to decode</param>
        /// <param name="output">The output buffer to write decoded data to</param>
        /// <returns>The number of bytes written to the output</returns>
        /// <exception cref="FormatException"></exception>
        public static ERRNO TryFromBase32Chars(ReadOnlySpan<char> input, Span<byte> output)
        {
            ForwardOnlyWriter<byte> writer = new(output);
            return TryFromBase32Chars(input, ref writer);
        }

        /// <summary>
        /// Gets the size of the buffer required to decode a base32 encoded 
        /// string. This buffer size will always be smaller than the input size.
        /// </summary>
        /// <param name="inputSize">The base32 encoded data input size</param>
        /// <returns>The size of the output buffer needed to write decoded data to</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint Base32DecodedSizeSize(nint inputSize) => (inputSize * 5) / 8;

        /// <summary>
        /// Attempts to decode the Base32 encoded string
        /// </summary>
        /// <param name="input">The Base32 encoded data to decode</param>
        /// <param name="writer">A <see cref="ForwardOnlyWriter{T}"/> to write decoded bytes to</param>
        /// <returns>The number of bytes written to the output</returns>
        /// <exception cref="FormatException"></exception>
        public unsafe static ERRNO TryFromBase32Chars(ReadOnlySpan<char> input, ref ForwardOnlyWriter<byte> writer)
        {
            //TODO support Big-Endian byte order

            int count = 0;
            ulong bufferLong = 0;                   //buffer used to shift data while decoding
            byte* buffer = (byte*)&bufferLong;      //re-cast to byte* to use it as a byte buffer

            //trim padding characters
            input = input.Trim('=');

            //Calc the number of bytes to write
            nint outputSize = Base32DecodedSizeSize(input.Length);

            //make sure the output buffer is large enough
            if(writer.RemainingSize < outputSize)
            {
                return false;
            }

            while(count < input.Length)
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
                 * Reemeber: each character only contains 5 bits of useful data
                 */
                
                buffer[0] |= GetCharCode(input[count]);

                count++;

                //If 8 characters have been decoded, reset the buffer
                if ((count % 8) == 0)
                {
                    //Write the 5 upper bytes in reverse order to the output buffer
                    for(int j = 0; j < 5; j++)
                    {
                        writer.Append(buffer[4 - j]);
                    }
                    
                    bufferLong = 0;
                }

                //left shift the buffer up by 5 bits, because thats all we 
                bufferLong <<= 5;
            }

            //If remaining data has not be written, but has been bufferedd, finalize it
            if (writer.Written < outputSize)
            {
                //calculate how many bits the buffer still needs to be shifted by (will be 5 bits off because of the previous loop)
                int remainingShift = (7 - (count % 8)) * 5;

                //right shift the buffer by the remaining bit count
                bufferLong <<= remainingShift;

                //calc remaining bytes
                nint remaining = (outputSize - writer.Written);

                //Write remaining bytes to the output
                for(int i = 0; i < remaining; i++)
                {
                    writer.Append(buffer[4 - i]);
                }
            }
            return writer.Written;
        }

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

                _=> throw new FormatException("Character found is not a Base32 encoded character")
            };
        }

        /// <summary>
        /// Calculates the maximum buffer size required to encode a binary block to its Base32
        /// character encoding
        /// </summary>
        /// <param name="bufferSize">The binary buffer size used to calculate the base32 buffer size</param>
        /// <returns>The maximum size (including padding) of the character buffer required to encode the binary data</returns>
        public static int Base32CalcMaxBufferSize(int bufferSize)
        {
            /*
             * Base32 encoding consumes 8 bytes for every 5 bytes
             * of input data
             */
            //Add up to 8 bytes for padding
            return (int)(Math.Ceiling(bufferSize / 5d) * 8) + (8 - (bufferSize % 8));
        }

        /// <summary>
        /// Converts the binary buffer to a base32 character string with optional padding characters
        /// </summary>
        /// <param name="binBuffer">The buffer to encode</param>
        /// <param name="withPadding">Should padding be included in the result</param>
        /// <returns>The base32 encoded string representation of the specified buffer</returns>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static string ToBase32String(ReadOnlySpan<byte> binBuffer, bool withPadding = false)
        {            
            //Calculate the base32 entropy to alloc an appropriate buffer (minium buffer of 2 chars)
            int entropy = Base32CalcMaxBufferSize(binBuffer.Length);
           
            using UnsafeMemoryHandle<char> charBuffer = MemoryUtil.UnsafeAlloc<char>(entropy);

            //Encode
            ERRNO encoded = TryToBase32Chars(binBuffer, charBuffer.Span);

            if (!encoded)
            {
                throw new InternalBufferTooSmallException("Base32 char buffer was too small");
            }

            //Convert with or w/o padding
            return withPadding 
                ? charBuffer.Span[0..(int)encoded].ToString() 
                : charBuffer.Span[0..(int)encoded].Trim('=').ToString();
        }   
        
        /// <summary>
        /// Converts the base32 character buffer to its structure representation
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="base32">The base32 character buffer</param>
        /// <returns>The new structure of the base32 data</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static T FromBase32String<T>(ReadOnlySpan<char> base32) where T: unmanaged
        {
            //calc size of bin buffer
            int size = base32.Length;
           
            using UnsafeMemoryHandle<byte> binBuffer = MemoryUtil.UnsafeAlloc(size);
            
            ERRNO decoded = TryFromBase32Chars(base32, binBuffer.Span);
           
            return decoded 
                ? MemoryMarshal.Read<T>(binBuffer.Span[..(int)decoded])
                : throw new InternalBufferTooSmallException("Binbuffer was too small");
        }

        /// <summary>
        /// Gets a byte array of the base32 decoded data
        /// </summary>
        /// <param name="base32">The character array to decode</param>
        /// <returns>The byte[] of the decoded binary data, or null if the supplied character array was empty</returns>
        public static byte[]? FromBase32String(ReadOnlySpan<char> base32)
        {
            if (base32.IsEmpty)
            {
                return null;
            }
            //Buffer size of the base32 string will always be enough buffer space
            using UnsafeMemoryHandle<byte> tempBuffer = MemoryUtil.UnsafeAlloc(base32.Length);

            //Try to decode the data
            ERRNO decoded = TryFromBase32Chars(base32, tempBuffer.Span);
            Debug.Assert(decoded > 0, "The supplied base32 buffer was too small to decode data into, but should not have been");

            return tempBuffer.Span[0..(int)decoded].ToArray();
        }

        /// <summary>
        /// Converts a structure to its base32 representation and returns the string of its value
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="value">The structure to encode</param>
        /// <param name="withPadding">A value indicating if padding should be used</param>
        /// <returns>The base32 string representation of the structure</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static string ToBase32String<T>(T value, bool withPadding = false) where T : unmanaged
        {
            //get the size of the structure
            int binSize = Unsafe.SizeOf<T>();
         
            Span<byte> binBuffer = stackalloc byte[binSize];
        
            MemoryMarshal.Write(binBuffer, in value);

            return ToBase32String(binBuffer, withPadding);
        }

        #endregion

        #region percent encoding

        private const int MAX_STACKALLOC = 512;

        private static readonly byte[] HexToUtf8Pos = "0123456789ABCDEF"u8.ToArray();

        /// <summary>
        /// Deterimes the size of the buffer needed to encode a utf8 encoded 
        /// character buffer into its url-safe percent/hex encoded representation
        /// </summary>
        /// <param name="utf8Bytes">The buffer to examine</param>
        /// <param name="allowedChars">A sequence of characters that are excluded from encoding</param>
        /// <returns>The size of the buffer required to encode</returns>
        public static unsafe int PercentEncodeCalcBufferSize(ReadOnlySpan<byte> utf8Bytes, ReadOnlySpan<byte> allowedChars = default)
        {
            /*
             * For every illegal character, the percent encoding adds 3 bytes of 
             * entropy. So a single byte will be replaced by 3, so adding 
             * 2 bytes for every illegal character plus the length of the 
             * intial buffer, we get the exact size of the buffer needed to 
             * percent encode.
             */
            int count = 0, len = utf8Bytes.Length;

            fixed (byte* utfBase = &MemoryMarshal.GetReference(utf8Bytes))
            {
                if (allowedChars.IsEmpty)
                {
                    //Find all unsafe characters and add the entropy size
                    for (int i = 0; i < len; i++)
                    {
                        if (!IsUrlSafeChar(utfBase[i]))
                        {
                            count += 2;
                        }
                    }
                }
                else
                {
                    //Find all unsafe characters and add the entropy size
                    for (int i = 0; i < len; i++)
                    {
                        //Check if value is url safe or is allowed by the allowed chars argument
                        if (!(IsUrlSafeChar(utfBase[i]) || allowedChars.Contains(utfBase[i])))
                        {
                            count += 2;
                        }
                    }
                }
            }
            //Size is initial buffer size + count bytes
            return len + count;
        }

        /// <summary>
        /// Percent encodes the buffer for utf8 encoded characters to its percent/hex encoded 
        /// utf8 character representation
        /// </summary>
        /// <param name="utf8Bytes">The buffer of utf8 encoded characters to encode</param>
        /// <param name="utf8Output">The buffer to write the encoded characters to</param>
        /// <param name="allowedChars">A sequence of characters that are excluded from encoding</param>
        /// <returns>The number of characters encoded and written to the output buffer</returns>
        public static ERRNO PercentEncode(ReadOnlySpan<byte> utf8Bytes, Span<byte> utf8Output, ReadOnlySpan<byte> allowedChars = default)
        {
            int outPos = 0, len = utf8Bytes.Length;
            ReadOnlySpan<byte> lookupTable = HexToUtf8Pos.AsSpan();

            if (allowedChars.IsEmpty)
            {
                for (int i = 0; i < len; i++)
                {
                    byte value = utf8Bytes[i];
                    //Check if value is url safe
                    if (IsUrlSafeChar(value))
                    {
                        //Skip
                        utf8Output[outPos++] = value;
                    }
                    else
                    {
                        /*
                        * Leading byte is %, followed by a single byte 
                        * for the hi and low nibble of the value
                        */
                      
                        utf8Output[outPos++] = 0x25;  // '%'
                        utf8Output[outPos++] = lookupTable[(value & 0xf0) >> 4];
                        utf8Output[outPos++] = lookupTable[value & 0x0f];
                    }
                }
            }
            else
            {
                for (int i = 0; i < len; i++)
                {
                    byte value = utf8Bytes[i];
                    //Check if value is url safe
                    if (IsUrlSafeChar(value) || allowedChars.Contains(value))
                    {
                        //Skip
                        utf8Output[outPos++] = value;
                    }
                    else
                    {
                        /*
                         * Leading byte is %, followed by a single byte 
                         * for the hi and low nibble of the value
                         */
                        
                        utf8Output[outPos++] = 0x25;  // '%'                        
                        utf8Output[outPos++] = lookupTable[(value & 0xf0) >> 4];
                        utf8Output[outPos++] = lookupTable[value & 0x0f];
                    }
                }
            }

            //Return the size of the output buffer
            return outPos;
        }


        private static bool IsUrlSafeChar(byte value)
        {
            return
                // base10 digits
                value > 0x2f && value < 0x3a
                // '_' (underscore)
                || value == 0x5f
                // '-' (hyphen)
                || value == 0x2d
                // Uppercase letters
                || value > 0x40 && value < 0x5b
                // lowercase letters
                || value > 0x60 && value < 0x7b;
            
        }

        //TODO: Implement decode with better performance, lookup table or math vs searching the table

        /// <summary>
        /// Decodes a percent (url/hex) encoded utf8 encoded character buffer to its utf8
        /// encoded binary value
        /// </summary>
        /// <param name="utf8Encoded">The buffer containg characters to be decoded</param>
        /// <param name="utf8Output">The buffer to write deocded values to</param>
        /// <returns>The nuber of bytes written to the output buffer</returns>
        /// <exception cref="FormatException"></exception>
        public static ERRNO PercentDecode(ReadOnlySpan<byte> utf8Encoded, Span<byte> utf8Output)
        {
            int outPos = 0, len = utf8Encoded.Length;
            ReadOnlySpan<byte> lookupTable = HexToUtf8Pos.AsSpan();

            for (int i = 0; i < len; i++)
            {
                byte value = utf8Encoded[i];

                //Begining of percent encoding character
                if (value == 0x25)
                {
                    //Calculate the base16 multiplier from the upper half of the 
                    int multiplier = lookupTable.IndexOf(utf8Encoded[i + 1]);

                    //get the base16 lower half to add
                    int lower = lookupTable.IndexOf(utf8Encoded[i + 2]);

                    //Check format
                    if (multiplier < 0 || lower < 0)
                    {
                        throw new FormatException($"Encoded buffer contains invalid hexadecimal characters following the % character at position {i}");
                    }

                    //Calculate the new value, shift multiplier to the upper 4 bits, then mask + or the lower 4 bits
                    value = (byte)(((byte)(multiplier << 4)) | ((byte)lower & 0x0f));

                    //Advance the encoded index by the two consumed chars
                    i += 2;
                }

                utf8Output[outPos++] = value;
            }
            return outPos;
        }

        /// <summary>
        /// Encodes the utf8 encoded character buffer to its percent/hex encoded utf8 
        /// character representation and returns the encoded string
        /// </summary>
        /// <param name="utf8Bytes">The bytes to encode</param>
        /// <param name="allowedChars">A collection of allowed characters that will not be encoded</param>
        /// <returns>The percent encoded string</returns>
        /// <exception cref="FormatException"></exception>
        public static string PercentEncode(ReadOnlySpan<byte> utf8Bytes, ReadOnlySpan<byte> allowedChars = default)
        {
            /*
             * I cannot avoid the allocation of a binary buffer without doing some sketchy
             * byte -> char cast on the string.create method. Which would also require object 
             * allocation for state data, and since spans are used, we cannot cross that 
             * callback boundry anyway. 
             */

            int bufferSize = PercentEncodeCalcBufferSize(utf8Bytes, allowedChars);

            //use stackalloc if the buffer is small enough
            if (bufferSize <= MAX_STACKALLOC)
            {
                //stack alloc output buffer
                Span<byte> output = stackalloc byte[bufferSize];

                ERRNO encoded = PercentEncode(utf8Bytes, output, allowedChars);

                return encoded > 0 
                    ? Encoding.UTF8.GetString(output) 
                    : throw new FormatException("Failed to percent encode the input data");
            }
            else
            {
                //Alloc heap buffer
                using UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAllocNearestPage(bufferSize);
                
                ERRNO encoded = PercentEncode(utf8Bytes, handle.Span, allowedChars);

                return encoded > 0
                    ? Encoding.UTF8.GetString(handle.AsSpan(0, encoded))
                    : throw new FormatException("Failed to percent encode the input data");
            }
        }     

        #endregion

        #region Base64

        /// <summary>
        /// Tries to convert the specified span containing a string representation that is 
        /// encoded with base-64 digits into a span of 8-bit unsigned integers.
        /// </summary>
        /// <param name="base64">Base64 character data to recover</param>
        /// <param name="buffer">The binary output buffer to write converted characters to</param>
        /// <returns>The number of bytes written, or <see cref="ERRNO.E_FAIL"/> of the conversion was unsucessful</returns>
        public static ERRNO TryFromBase64Chars(ReadOnlySpan<char> base64, Span<byte> buffer)
        {
            return Convert.TryFromBase64Chars(base64, buffer, out int bytesWritten) ? bytesWritten : ERRNO.E_FAIL;
        }

        /// <summary>
        /// Tries to convert the 8-bit unsigned integers inside the specified read-only span
        /// into their equivalent string representation that is encoded with base-64 digits.
        /// You can optionally specify whether to insert line breaks in the return value.
        /// </summary>
        /// <param name="buffer">The binary buffer to convert characters from</param>
        /// <param name="base64">The base64 output buffer</param>
        /// <param name="options">
        /// One of the enumeration values that specify whether to insert line breaks in the
        /// return value. The default value is System.Base64FormattingOptions.None.
        /// </param>
        /// <returns>The number of characters encoded, or <see cref="ERRNO.E_FAIL"/> if conversion was unsuccessful</returns>
        public static ERRNO TryToBase64Chars(
            ReadOnlySpan<byte> buffer, 
            Span<char> base64, 
            Base64FormattingOptions options = Base64FormattingOptions.None
        )
        {
            return Convert.TryToBase64Chars(buffer, base64, out int charsWritten, options) ? charsWritten : ERRNO.E_FAIL;
        }
       

        /*
         * Calc base64 padding chars excluding the length mod 4 = 0 case
         * by and-ing 0x03 (011) with the result
         */
        
        /// <summary>
        /// Determines the number of missing padding bytes from the length of the base64
        /// data sequence. 
        /// <code>
        /// Formula  (4 - (length mod 4) and 0x03
        /// </code>
        /// </summary>
        /// <param name="length">The length of the base64 buffer</param>
        /// <returns>The number of padding bytes to add to the end of the sequence</returns>
        public static int Base64CalcRequiredPadding(int length) => (4 - (length % 4)) & 0x03;

        /// <summary>
        /// Converts a base64 utf8 encoded binary buffer to 
        /// its base64url encoded version
        /// </summary>
        /// <param name="base64">The binary buffer to convert</param>
        public static unsafe void Base64ToUrlSafeInPlace(Span<byte> base64)
        {
            int len = base64.Length;
            
            fixed(byte* ptr = &MemoryMarshal.GetReference(base64))
            {
                for (int i = 0; i < len; i++)
                {
                    switch (ptr[i])
                    {
                        //Replace + with - (minus)
                        case 0x2b:
                            ptr[i] = 0x2d;
                            break;
                        //Replace / with _ (underscore)
                        case 0x2f:
                            ptr[i] = 0x5f;
                            break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Converts a base64url encoded utf8 encoded binary buffer to
        /// its base64 encoded version
        /// </summary>
        /// <param name="uft8Base64Url">The base64url utf8 to decode</param>
        public static unsafe void Base64FromUrlSafeInPlace(Span<byte> uft8Base64Url)
        {
            int len = uft8Base64Url.Length;

            fixed (byte* ptr = &MemoryMarshal.GetReference(uft8Base64Url))
            {
                for (int i = 0; i < len; i++)
                {
                    switch (ptr[i])
                    {
                        //Replace - with + (plus)
                        case 0x2d:
                            ptr[i] = 0x2b;
                            break;
                        //Replace _ with / (slash)
                        case 0x5f:
                            ptr[i] = 0x2f;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Converts the input buffer to a url safe base64 encoded 
        /// utf8 buffer from the base64 input buffer. The base64 is copied
        /// directly to the output then converted in place. This is 
        /// just a shortcut method for readonly spans
        /// </summary>
        /// <param name="base64">The base64 encoded data</param>
        /// <param name="base64Url">The base64url encoded output</param>
        /// <returns>The size of the <paramref name="base64"/> buffer</returns>
        public static ERRNO Base64ToUrlSafe(ReadOnlySpan<byte> base64, Span<byte> base64Url)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(base64.Length, base64Url.Length, nameof(base64));

            //Aligned copy to the output buffer
            MemoryUtil.Memmove(
                ref MemoryMarshal.GetReference(base64Url),
                0,
                ref MemoryMarshal.GetReference(base64),
                0,
                (nuint)base64Url.Length
            );

            //One time convert the output buffer to url safe
            Base64ToUrlSafeInPlace(base64Url);
            return base64.Length;
        }

        /// <summary>
        /// Converts the urlsafe input buffer to a base64 encoded 
        /// utf8 buffer from the base64 input buffer. The base64 is copied
        /// directly to the output then converted in place. This is 
        /// just a shortcut method for readonly spans
        /// </summary>
        /// <param name="base64">The base64 encoded data</param>
        /// <param name="base64Url">The base64url encoded output</param>
        /// <returns>The size of the <paramref name="base64Url"/> buffer</returns>
        public static ERRNO Base64FromUrlSafe(ReadOnlySpan<byte> base64Url, Span<byte> base64)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(base64.Length, base64Url.Length, nameof(base64));

            //Aligned copy to the output buffer
            MemoryUtil.Memmove(
                ref MemoryMarshal.GetReference(base64Url),
                0,
                ref MemoryMarshal.GetReference(base64),
                0,
                (nuint)base64Url.Length
            );

            //One time convert the output buffer to url safe
            Base64FromUrlSafeInPlace(base64);
            return base64Url.Length;
        }
        
        /// <summary>
        /// Decodes a utf8 base64url encoded sequence of data and writes it 
        /// to the supplied output buffer
        /// </summary>
        /// <param name="utf8Base64Url">The utf8 base64 url encoded string</param>
        /// <param name="output">The output buffer to write the decoded data to</param>
        /// <returns>The number of bytes written or <see cref="ERRNO.E_FAIL"/> if the operation failed</returns>
        public static ERRNO Base64UrlDecode(ReadOnlySpan<byte> utf8Base64Url, Span<byte> output)
        {
            if(utf8Base64Url.IsEmpty || output.IsEmpty)
            {
                return ERRNO.E_FAIL;
            }
            //url deocde
            ERRNO count = Base64FromUrlSafe(utf8Base64Url, output);

            //Writer for adding padding bytes
            ForwardOnlyWriter<byte> writer = new (output);
            writer.Advance(count);
            
            //Calc required padding
            int paddingToAdd = Base64CalcRequiredPadding(writer.Written);
            //Add padding bytes
            for (; paddingToAdd > 0; paddingToAdd--)
            {
                writer.Append(0x3d); // '='
            }
          
            //Base64 decode in place, we should have a buffer large enough
            OperationStatus status = Base64.DecodeFromUtf8InPlace(writer.AsSpan(), out int bytesWritten);
            //If status is successful return the number of bytes written
            return status == OperationStatus.Done ? bytesWritten : ERRNO.E_FAIL;
        }

        /// <summary>
        /// Decodes a base64url encoded character sequence
        /// of data and writes it to the supplied output buffer
        /// </summary>
        /// <param name="chars">The character buffer to decode</param>
        /// <param name="output">The output buffer to write decoded data to</param>
        /// <param name="encoding">The character encoding</param>
        /// <returns>The number of bytes written or <see cref="ERRNO.E_FAIL"/> if the operation failed</returns>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static ERRNO Base64UrlDecode(ReadOnlySpan<char> chars, Span<byte> output, Encoding? encoding = null)
        {
            if (chars.IsEmpty || output.IsEmpty)
            {
                return ERRNO.E_FAIL;
            }

            //Set the encoding to utf8
            encoding ??= Encoding.UTF8;

            //get the number of bytes to alloc a buffer
            int decodedSize = encoding.GetByteCount(chars);

            if(decodedSize > MAX_STACKALLOC)
            {
                using UnsafeMemoryHandle<byte> decodeHandle = MemoryUtil.UnsafeAlloc(decodedSize);

                //Get the utf8 binary data
                int count = encoding.GetBytes(chars, decodeHandle.Span);
                return Base64UrlDecode(decodeHandle.Span[..count], output);
            }
            else
            {               
                Span<byte> decodeBuffer = stackalloc byte[decodedSize];

                //Get the utf8 binary data
                int count = encoding.GetBytes(chars, decodeBuffer);
                return Base64UrlDecode(decodeBuffer[..count], output);
            }
        }

        private static string ConvertToBase64UrlStringInternal(
            ReadOnlySpan<byte> rawData,
            Span<byte> buffer,
            bool includePadding,
            Encoding encoding
        )
        {
            //Conver to base64
            OperationStatus status = Base64.EncodeToUtf8(rawData, buffer, out _, out int written, true);

            //Check for invalid states
            Debug.Assert(status != OperationStatus.DestinationTooSmall, "Buffer allocation was too small for the conversion");
            Debug.Assert(status != OperationStatus.NeedMoreData, "Need more data status was returned but is not valid for an encoding operation");

            //Should never occur, but just in case, this is an input error
            if (status == OperationStatus.InvalidData)
            {
                throw new ArgumentException("Your input data contained values that could not be converted to base64", nameof(rawData));
            }

            Span<byte> base64 = buffer[..written];

            //Make url safe
            Base64ToUrlSafeInPlace(base64);

            //Remove padding
            if (!includePadding)
            {
                base64 = base64.TrimEnd((byte)0x3d);
            }

            //Convert to string
            return encoding.GetString(base64);
        }

        /// <summary>
        /// Base64url encodes the binary buffer to its utf8 binary representation
        /// </summary>
        /// <param name="buffer">The intput binary buffer to base64url encode</param>
        /// <param name="dataLength">The data within the buffer to encode, must be smaller than the entire buffer</param>
        /// <param name="includePadding">A value that indicates if base64 padding should be url encoded(true), or removed(false).</param>
        /// <returns>The number characters written to the buffer, or <see cref="ERRNO.E_FAIL"/> if a error occured.</returns>
        public static ERRNO Base64UrlEncodeInPlace(Span<byte> buffer, int dataLength, bool includePadding)
        {
            //Convert to base64
            if (Base64.EncodeToUtf8InPlace(buffer, dataLength, out int bytesWritten) != OperationStatus.Done)
            {
                return ERRNO.E_FAIL;
            }

            if (includePadding)
            {
                //Url encode in place
                Base64ToUrlSafeInPlace(buffer[..bytesWritten]);
                return bytesWritten;
            }
            else
            {
                //Remove padding bytes
                Span<byte> nonPadded = buffer[..bytesWritten].TrimEnd((byte)0x3d);

                Base64ToUrlSafeInPlace(nonPadded);
                return nonPadded.Length;
            }
        }

        /// <summary>
        /// Attempts to base64url encode the binary buffer to it's base64url encoded representation
        /// in place, aka does not allocate a temporary buffer. The buffer must be large enough to
        /// encode the data, if not the operation will fail. The data in this span will be overwritten
        /// to do the conversion
        /// </summary>
        /// <param name="rawData">The raw data buffer that will be used to encode data aswell as read it</param>
        /// <param name="length">The length of the binary data to encode</param>
        /// <param name="includePadding">A value specifying whether base64 padding should be encoded</param>
        /// <returns>The base64url encoded string</returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ToBase64UrlSafeStringInPlace(Span<byte> rawData, int length, bool includePadding)
        {
            ERRNO converted = Base64UrlEncodeInPlace(rawData, length, includePadding);

            //Encode in place
            if (converted < 1)
            {
                throw new ArgumentException("The input buffer was not large enough to encode in-place", nameof(rawData));
            }

            //Convert to string
            return Encoding.UTF8.GetString(rawData[..(int)converted]);
        }

        /// <summary>
        /// Converts binary data to it's base64url encoded representation and may allocate a temporary 
        /// heap buffer.
        /// </summary>
        /// <param name="rawData">The binary data to encode</param>
        /// <param name="includePadding">A value that indicates if the base64 padding characters should be included</param>
        /// <returns>The base64url encoded string</returns>
        /// <exception cref="ArgumentException"></exception>
        [Obsolete("Use Base64UrlEncode instead")]
        public static string ToBase64UrlSafeString(ReadOnlySpan<byte> rawData, bool includePadding) => Base64UrlEncode(rawData, includePadding);

        /// <summary>
        /// Encodes the binary input buffer to its base64url safe utf8 encoding, and writes the output 
        /// to the supplied buffer. Be sure to call <see cref="Base64.GetMaxEncodedToUtf8Length(int)"/>
        /// to allocate the correct size buffer for encoding
        /// </summary>
        /// <param name="input">The intput binary buffer to base64url encode</param>
        /// <param name="output">The output buffer to write the base64url safe encodded date to</param>
        /// <param name="includePadding">A value that indicates if base64 padding should be url encoded(true), or removed(false).</param>
        /// <returns>The number characters written to the buffer, or <see cref="ERRNO.E_FAIL"/> if a error occured.</returns>
        public static ERRNO Base64UrlEncode(ReadOnlySpan<byte> input, Span<byte> output, bool includePadding)
        {
            //Do bsae64 encoding avoiding the tripple copy
            if (Base64.EncodeToUtf8(input, output, out _, out int bytesWritten) != OperationStatus.Done)
            {
                return ERRNO.E_FAIL;
            }

            if (includePadding)
            {
                //Url encode in place
                Base64ToUrlSafeInPlace(output[..bytesWritten]);
                return bytesWritten;
            }
            else
            {
                //Remove padding bytes from base64 encode
                Span<byte> nonPadded = output[..bytesWritten].TrimEnd((byte)0x3d);

                Base64ToUrlSafeInPlace(nonPadded);
                return nonPadded.Length;
            }
        }

        /// <summary>
        /// Encodes the binary intput buffer to its base64url safe encoding, then converts the binary 
        /// value to it character encoded value and allocates a new string. Defaults to UTF8 character 
        /// encoding. Base64url is a subset of ASCII,UTF7,UTF8,UTF16 etc so most encodings should be safe.
        /// </summary>
        /// <param name="input">The input binary intput buffer</param>
        /// <param name="includePadding">A value that indicates if base64 padding should be url encoded(true), or removed(false).</param>
        /// <param name="encoding">The encoding used to convert the binary buffer to its character representation.</param>
        /// <returns>The base64url encoded string of the input data using the desired encoding</returns>
        public static string Base64UrlEncode(ReadOnlySpan<byte> input, bool includePadding, Encoding? encoding = null)
        {
            if (input.IsEmpty)
            {
                return string.Empty;
            }

            encoding ??= Encoding.UTF8;

            //We need to alloc an intermediate buffer, get the base64 max size
            int maxSize = Base64.GetMaxEncodedToUtf8Length(input.Length);

            if (maxSize > MAX_STACKALLOC)
            {
                //Alloc heap buffer
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(maxSize);
                return ConvertToBase64UrlStringInternal(input, buffer.Span, includePadding, encoding);
            }
            else
            {
                //Alloc stack buffer
                Span<byte> bufer = stackalloc byte[maxSize];
                return ConvertToBase64UrlStringInternal(input, bufer, includePadding, encoding);
            }
        }

        /// <summary>
        /// Encodes the binary intput buffer to its base64url safe encoding, then converts the internal buffer
        /// to its character encoding using the supplied <paramref name="encoding"/>, and writes the characters
        /// to the output buffer. Defaults to UTF8 character encoding. Base64url is a subset of ASCII,UTF7,UTF8,UTF16 etc
        /// so most encodings should be safe.
        /// </summary>
        /// <param name="input">The input binary intput buffer</param>
        /// <param name="output">The character output buffer</param>
        /// <param name="includePadding">A value that indicates if base64 padding should be url encoded(true), or removed(false).</param>
        /// <param name="encoding">The encoding used to convert the binary buffer to its character representation.</param>
        /// <returns>The number of characters written to the buffer, or <see cref="ERRNO.E_FAIL"/> if a error occured</returns>
        public static ERRNO Base64UrlEncode(ReadOnlySpan<byte> input, Span<char> output, bool includePadding, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;

            //We need to alloc an intermediate buffer, get the base64 max size
            int maxSize = Base64.GetMaxEncodedToUtf8Length(input.Length);

            if (maxSize > MAX_STACKALLOC)
            {
                //Alloc heap buffer
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(maxSize);
                return Base64UrlEncodeCore(input, buffer.Span, output, encoding, includePadding);
            }
            else
            {
                //Alloc stack buffer
                Span<byte> bufer = stackalloc byte[maxSize];
                return Base64UrlEncodeCore(input, bufer, output, encoding, includePadding);
            }

            static ERRNO Base64UrlEncodeCore(ReadOnlySpan<byte> input, Span<byte> buffer, Span<char> output, Encoding encoding, bool includePadding)
            {
                //Encode to url safe binary
                ERRNO count = Base64UrlEncode(input, buffer, includePadding);

                if (count <= 0)
                {
                    return count;
                }

                //Get char count to return to caller
                int charCount = encoding.GetCharCount(buffer[..(int)count]);

                //Encode to characters
                encoding.GetChars(buffer[0..(int)count], output);

                return charCount;
            }
        }

        #endregion
    }
}