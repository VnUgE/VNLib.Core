/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: PercentEncoding.cs
*
* PercentEncoding.cs is part of VNLib.Utils which is part of the larger 
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
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;

namespace VNLib.Utils.Codecs
{
    /// <summary>
    /// Provides utility functions for percent-encoding (URL encoding) and decoding
    /// of UTF-8 byte sequences, as used in URIs per RFC 3986.
    /// </summary>
    public static class PercentEncoding
    {
        #region PercentEncoding

        private const int MAX_STACKALLOC = 512;

        /*
         * Hex lookup table: maps nibble value 0–15 to its uppercase ASCII hex character
         * ('0'–'9', 'A'–'F'). Used during both encode (nibble → char) and decode (char → nibble).
         */
        private static readonly byte[] HexTable = "0123456789ABCDEF"u8.ToArray();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsUrlSafeChar(byte value)
        {
            return
                // ASCII digits 0-9
                (value > 0x2f && value < 0x3a)
                // Unreserved: '-' (hyphen) and '_' (underscore)
                || value == 0x2d
                || value == 0x5f
                // Uppercase A-Z
                || (value > 0x40 && value < 0x5b)
                // Lowercase a-z
                || (value > 0x60 && value < 0x7b);
        }

        /// <summary>
        /// Calculates the exact number of UTF-8 bytes required to percent-encode
        /// <paramref name="utf8Input"/>, accounting for characters that must be escaped.
        /// Each unsafe byte expands from 1 byte to 3 bytes ('%' + 2 hex digits).
        /// </summary>
        /// <param name="utf8Input">The UTF-8 bytes to examine</param>
        /// <param name="allowedChars">
        /// Optional set of additional characters (as UTF-8 bytes) that should NOT be
        /// encoded even if they would otherwise be considered unsafe.
        /// </param>
        /// <returns>The exact buffer size needed for the encoded output</returns>
        public static unsafe int GetEncodedSize(ReadOnlySpan<byte> utf8Input, ReadOnlySpan<byte> allowedChars = default)
        {
            int extraBytes = 0;
            int len = utf8Input.Length;

            fixed (byte* ptr = &MemoryMarshal.GetReference(utf8Input))
            {
                if (allowedChars.IsEmpty)
                {
                    for (int i = 0; i < len; i++)
                    {
                        if (!IsUrlSafeChar(ptr[i]))
                        {
                            extraBytes += 2;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        if (!(IsUrlSafeChar(ptr[i]) || allowedChars.Contains(ptr[i])))
                        {
                            extraBytes += 2;
                        }
                    }
                }
            }

            return len + extraBytes;
        }

        /// <summary>
        /// Percent-encodes the UTF-8 input and writes the encoded UTF-8 output
        /// to <paramref name="utf8Output"/>. Use <see cref="GetEncodedSize"/> to allocate a
        /// correctly-sized output buffer.
        /// </summary>
        /// <param name="utf8Input">The UTF-8 bytes to encode</param>
        /// <param name="utf8Output">The buffer to write the percent-encoded UTF-8 output to</param>
        /// <param name="allowedChars">
        /// Optional set of additional characters that should NOT be encoded.
        /// </param>
        /// <returns>The number of bytes written to <paramref name="utf8Output"/></returns>
        public static ERRNO Encode(ReadOnlySpan<byte> utf8Input, Span<byte> utf8Output, ReadOnlySpan<byte> allowedChars = default)
        {
            int outPos = 0;
            int len = utf8Input.Length;
            ReadOnlySpan<byte> lookup = HexTable.AsSpan();

            if (allowedChars.IsEmpty)
            {
                for (int i = 0; i < len; i++)
                {
                    byte value = utf8Input[i];

                    if (IsUrlSafeChar(value))
                    {
                        utf8Output[outPos++] = value;
                    }
                    else
                    {
                        // Percent-encode: '%' followed by hi nibble and lo nibble hex digits
                        utf8Output[outPos++] = 0x25;  // '%'
                        utf8Output[outPos++] = lookup[(value & 0xf0) >> 4];
                        utf8Output[outPos++] = lookup[value & 0x0f];
                    }
                }
            }
            else
            {
                for (int i = 0; i < len; i++)
                {
                    byte value = utf8Input[i];

                    if (IsUrlSafeChar(value) || allowedChars.Contains(value))
                    {
                        utf8Output[outPos++] = value;
                    }
                    else
                    {
                        utf8Output[outPos++] = 0x25;  // '%'
                        utf8Output[outPos++] = lookup[(value & 0xf0) >> 4];
                        utf8Output[outPos++] = lookup[value & 0x0f];
                    }
                }
            }

            return outPos;
        }

        /// <summary>
        /// Percent-encodes the UTF-8 input and returns the result as a string.
        /// Allocates a temporary buffer internally.
        /// </summary>
        /// <param name="utf8Input">The UTF-8 bytes to encode</param>
        /// <param name="allowedChars">
        /// Optional set of additional characters that should NOT be encoded.
        /// </param>
        /// <returns>The percent-encoded string representation of the input</returns>
        /// <exception cref="FormatException">Encoding failed unexpectedly</exception>
        public static string Encode(ReadOnlySpan<byte> utf8Input, ReadOnlySpan<byte> allowedChars = default)
        {
            int bufferSize = GetEncodedSize(utf8Input, allowedChars);

            if (bufferSize <= MAX_STACKALLOC)
            {
                Span<byte> output = stackalloc byte[bufferSize];
                ERRNO encoded = Encode(utf8Input, output, allowedChars);

                return encoded > 0
                    ? Encoding.UTF8.GetString(output[..(int)encoded])
                    : throw new FormatException("Failed to percent-encode the input data");
            }
            else
            {
                using UnsafeMemoryHandle<byte> handle = MemoryUtil.UnsafeAllocNearestPage(bufferSize);
                ERRNO encoded = Encode(utf8Input, handle.Span, allowedChars);

                return encoded > 0
                    ? Encoding.UTF8.GetString(handle.AsSpan(0, encoded))
                    : throw new FormatException("Failed to percent-encode the input data");
            }
        }

        /// <summary>
        /// Decodes a percent-encoded UTF-8 buffer and writes the decoded bytes
        /// to <paramref name="utf8Output"/>. The decoded output is always equal to
        /// or shorter than the encoded input.
        /// </summary>
        /// <param name="utf8Encoded">The percent-encoded UTF-8 input buffer</param>
        /// <param name="utf8Output">The buffer to write decoded bytes to</param>
        /// <returns>The number of bytes written to <paramref name="utf8Output"/></returns>
        /// <exception cref="FormatException">The input contains an invalid percent-encoded sequence</exception>
        public static ERRNO Decode(ReadOnlySpan<byte> utf8Encoded, Span<byte> utf8Output)
        {
            //TODO: improve decode performance with a lookup table or arithmetic instead of IndexOf search
            int outPos = 0;
            int len = utf8Encoded.Length;
            ReadOnlySpan<byte> lookup = HexTable.AsSpan();

            for (int i = 0; i < len; i++)
            {
                byte value = utf8Encoded[i];

                if (value == 0x25) // '%'
                {
                    int hi = lookup.IndexOf(utf8Encoded[i + 1]);
                    int lo = lookup.IndexOf(utf8Encoded[i + 2]);

                    if (hi < 0 || lo < 0)
                    {
                        throw new FormatException(
                            $"Encoded buffer contains invalid hexadecimal characters following '%' at position {i}"
                        );
                    }

                    // Reconstruct the byte from hi and lo nibbles
                    value = (byte)(((byte)(hi << 4)) | ((byte)lo & 0x0f));
                    i += 2;
                }

                utf8Output[outPos++] = value;
            }

            return outPos;
        }

        #endregion
    }
}
