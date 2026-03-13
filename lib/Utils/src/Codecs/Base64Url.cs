/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: Base64Url.cs
*
* Base64Url.cs is part of VNLib.Utils which is part of the larger 
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
using System.Buffers;
using System.Diagnostics;
using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;

namespace VNLib.Utils.Codecs
{
    /// <summary>
    /// Provides utility functions for URL-safe Base64 (Base64URL) encoding and decoding
    /// as defined in RFC 4648 §5. Base64URL uses '-' instead of '+' and '_' instead
    /// of '/', and optionally omits the '=' padding character.
    /// </summary>
    public static class Base64Url
    {
        #region Base64Url

        private const int MAX_STACKALLOC = 512;

        /*
         * Unsafe in-place character substitution cores. Called from the public in-place
         * overloads and the internal encode/decode pipeline.
         */

        static SearchValues<byte> vals = SearchValues.Create([0x2b, 0x2f]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MakeUrlSafeCore(Span<byte> base64Utf8)
        {
            int len = base64Utf8.Length;

            MemoryExtensions.IndexOfAny<byte>(base64Utf8, 0x2b, 0x2f);

            fixed (byte* ptr = &MemoryMarshal.GetReference(base64Utf8))
            {
                for (int i = 0; i < len; i++)
                {
                    switch (ptr[i])
                    {
                        case 0x2b: ptr[i] = 0x2d; break;   // '+' -> '-'
                        case 0x2f: ptr[i] = 0x5f; break;   // '/' -> '_'
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void RestoreBase64Core(Span<byte> base64UrlUtf8)
        {
            int len = base64UrlUtf8.Length;

            fixed (byte* ptr = &MemoryMarshal.GetReference(base64UrlUtf8))
            {
                for (int i = 0; i < len; i++)
                {
                    switch (ptr[i])
                    {
                        case 0x2d: ptr[i] = 0x2b; break;   // '-' -> '+'
                        case 0x5f: ptr[i] = 0x2f; break;   // '_' -> '/'
                    }
                }
            }
        }

        /*
         * Applies the URL-safe character substitution and optional padding trim to an
         * already-encoded base64 buffer. Shared by EncodeInPlace and Encode(bytes→bytes)
         * to avoid duplicating the same branching logic.
         */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ERRNO ApplyUrlSafe(Span<byte> buffer, int bytesWritten, bool includePadding)
        {
            if (includePadding)
            {
                MakeUrlSafeCore(buffer[..bytesWritten]);
                return bytesWritten;
            }
            else
            {
                // Remove padding chars '=' as 0x3d
                Span<byte> noPad = buffer[..bytesWritten].TrimEnd((byte)0x3d);
                MakeUrlSafeCore(noPad);
                return noPad.Length;
            }
        }

        /*
         * Shared helpers that finish an encode operation by producing a string or writing
         * characters. Both delegate to the primitive Encode(bytes→bytes) overload.
         */

        private static string EncodeToStringCore(ReadOnlySpan<byte> input, Span<byte> buffer, bool includePadding, Encoding encoding)
        {
            OperationStatus status = Base64.EncodeToUtf8(input, buffer, out _, out int written, isFinalBlock: true);

            Debug.Assert(status != OperationStatus.DestinationTooSmall, "Buffer allocation was too small for the Base64URL conversion");
            Debug.Assert(status != OperationStatus.NeedMoreData, "NeedMoreData is not valid for a final-block encoding operation");

            if (status == OperationStatus.InvalidData)
            {
                throw new ArgumentException("The input data contains values that could not be converted to Base64", "input");
            }

            Span<byte> encoded = buffer[..written];
            MakeUrlSafeCore(encoded);

            if (!includePadding)
            {
                encoded = encoded.TrimEnd((byte)0x3d);
            }

            return encoding.GetString(encoded);
        }

        private static ERRNO EncodeToCharsCore(
            ReadOnlySpan<byte> input,
            Span<byte> buffer,
            Span<char> output,
            Encoding encoding,
            bool includePadding
        )
        {
            ERRNO count = Encode(input, buffer, includePadding);

            if (count <= 0)
            {
                return count;
            }

            int charCount = encoding.GetCharCount(buffer[..(int)count]);
            encoding.GetChars(buffer[..(int)count], output);
            return charCount;
        }

        /*
         * DecodeCore uses a caller-supplied temp buffer (already sized to
         * utf8Input.Length + padding) to avoid writing beyond the output buffer
         * during the intermediate padded-base64 step.
         */
        private static ERRNO DecodeCore(ReadOnlySpan<byte> utf8Input, Span<byte> temp, Span<byte> output)
        {

            utf8Input.CopyTo(temp);

            RestoreBase64Core(temp[..utf8Input.Length]);

            // Fill the trailing slice with '=' so the length is a multiple of 4
            temp[utf8Input.Length..].Fill(0x3d);

            OperationStatus status = Base64.DecodeFromUtf8InPlace(temp, out int bytesWritten);

            if (status != OperationStatus.Done)
            {
                return ERRNO.E_FAIL;
            }

            temp[..bytesWritten].CopyTo(output);
            return bytesWritten;
        }

        /// <summary>
        /// Returns the maximum number of UTF-8 bytes required to Base64URL-encode
        /// the given number of input bytes.
        /// </summary>
        /// <param name="inputLength">The number of binary bytes to encode</param>
        /// <returns>The maximum encoded output size in bytes</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxEncodedSize(int inputLength)
            => Base64.GetMaxEncodedToUtf8Length(inputLength);

        /// <summary>
        /// Calculates the number of Base64 '=' padding characters required to make
        /// the encoded length a multiple of 4.
        /// <code>(4 - length % 4) &amp; 0x03</code>
        /// </summary>
        /// <param name="encodedLength">The length of the Base64 encoded data without padding</param>
        /// <returns>The number of padding characters required (0, 1, or 2)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRequiredPadding(int encodedLength) => (4 - encodedLength % 4) & 0x03;

        /// <summary>
        /// Converts a UTF-8 standard Base64 buffer to Base64URL format in-place,
        /// replacing '+' with '-' and '/' with '_'.
        /// </summary>
        /// <param name="base64Utf8">The UTF-8 Base64 buffer to convert in-place</param>
        public static void MakeUrlSafe(Span<byte> base64Utf8) => MakeUrlSafeCore(base64Utf8);

        /// <summary>
        /// Copies <paramref name="base64"/> to <paramref name="output"/> then converts
        /// it to Base64URL format in-place. The output buffer must be at least as large
        /// as the input.
        /// </summary>
        /// <param name="base64">The source UTF-8 Base64 data</param>
        /// <param name="output">The destination buffer to write the Base64URL output to</param>
        /// <returns>The number of bytes written to <paramref name="output"/></returns>
        public static ERRNO MakeUrlSafe(ReadOnlySpan<byte> base64, Span<byte> output)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(output.Length, base64.Length, nameof(output));

            base64.CopyTo(output);
            MakeUrlSafeCore(output[..base64.Length]);
            return base64.Length;
        }

        /// <summary>
        /// Converts a UTF-8 Base64URL buffer back to standard Base64 format in-place,
        /// replacing '-' with '+' and '_' with '/'.
        /// </summary>
        /// <param name="base64UrlUtf8">The UTF-8 Base64URL buffer to restore in-place</param>
        public static void RestoreBase64(Span<byte> base64UrlUtf8) => RestoreBase64Core(base64UrlUtf8);

        /// <summary>
        /// Copies <paramref name="base64Url"/> to <paramref name="output"/> then restores
        /// it to standard Base64 format in-place. The output buffer must be at least as
        /// large as the input.
        /// </summary>
        /// <param name="base64Url">The source UTF-8 Base64URL data</param>
        /// <param name="output">The destination buffer to write the standard Base64 output to</param>
        /// <returns>The number of bytes written to <paramref name="output"/></returns>
        public static ERRNO RestoreBase64(ReadOnlySpan<byte> base64Url, Span<byte> output)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(output.Length, base64Url.Length, nameof(output));

            base64Url.CopyTo(output);
            RestoreBase64Core(output[..base64Url.Length]);
            return base64Url.Length;
        }

        /// <summary>
        /// Base64URL encodes binary data directly within <paramref name="buffer"/>, overwriting
        /// the binary input with the encoded result. The buffer must be large enough to hold
        /// the encoded output; use <see cref="GetMaxEncodedSize(int)"/> to determine the required size.
        /// </summary>
        /// <param name="buffer">The buffer containing data to encode, overwritten with the encoded result</param>
        /// <param name="dataLength">The number of binary data bytes within <paramref name="buffer"/> to encode</param>
        /// <param name="includePadding">When <see langword="true"/>, '=' padding is preserved; when <see langword="false"/>, padding is trimmed</param>
        /// <returns>The number of encoded bytes written, or <see cref="ERRNO.E_FAIL"/> on failure</returns>
        public static ERRNO EncodeInPlace(Span<byte> buffer, int dataLength, bool includePadding = false)
        {
            if (Base64.EncodeToUtf8InPlace(buffer, dataLength, out int written) != OperationStatus.Done)
            {
                return ERRNO.E_FAIL;
            }

            return ApplyUrlSafe(buffer, written, includePadding);
        }

        /// <summary>
        /// Encodes binary data to its UTF-8 Base64URL representation and writes the output to
        /// <paramref name="output"/>. Use <see cref="GetMaxEncodedSize(int)"/> to allocate a
        /// correctly-sized output buffer.
        /// </summary>
        /// <param name="input">The binary data to encode</param>
        /// <param name="output">The UTF-8 output buffer to write the encoded data to</param>
        /// <param name="includePadding">When <see langword="true"/>, '=' padding is included; when <see langword="false"/>, padding is trimmed</param>
        /// <returns>The number of bytes written, or <see cref="ERRNO.E_FAIL"/> on failure</returns>
        public static ERRNO Encode(ReadOnlySpan<byte> input, Span<byte> output, bool includePadding = false)
        {
            if (Base64.EncodeToUtf8(input, output, out _, out int written) != OperationStatus.Done)
            {
                return ERRNO.E_FAIL;
            }

            return ApplyUrlSafe(output, written, includePadding);
        }

        /// <summary>
        /// Encodes binary data to a Base64URL string. Allocates a temporary UTF-8 buffer internally.
        /// </summary>
        /// <param name="input">The binary data to encode</param>
        /// <param name="includePadding">When <see langword="true"/>, '=' padding is included; when <see langword="false"/>, padding is trimmed</param>
        /// <param name="encoding">Character encoding used to produce the string. Defaults to UTF-8.</param>
        /// <returns>The Base64URL-encoded string, or <see cref="string.Empty"/> if <paramref name="input"/> is empty</returns>
        /// <exception cref="ArgumentException">The input data contained values that could not be converted to Base64</exception>
        public static string Encode(ReadOnlySpan<byte> input, bool includePadding, Encoding? encoding = null)
        {
            if (input.IsEmpty)
            {
                return string.Empty;
            }

            encoding ??= Encoding.UTF8;
            int maxSize = GetMaxEncodedSize(input.Length);

            // Prefer stack buffer for small encoding
            if (maxSize > MAX_STACKALLOC)
            {
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(maxSize);
                return EncodeToStringCore(input, buffer.Span, includePadding, encoding);
            }
            else
            {
                Span<byte> buffer = stackalloc byte[maxSize];
                return EncodeToStringCore(input, buffer, includePadding, encoding);
            }
        }

        /// <summary>
        /// Encodes binary data to Base64URL characters and writes them to <paramref name="output"/>.
        /// </summary>
        /// <param name="input">The binary data to encode</param>
        /// <param name="output">The character buffer to write the encoded output to</param>
        /// <param name="includePadding">When <see langword="true"/>, '=' padding is included; when <see langword="false"/>, padding is trimmed</param>
        /// <param name="encoding">Character encoding used to convert bytes to characters. Defaults to UTF-8.</param>
        /// <returns>The number of characters written, or <see cref="ERRNO.E_FAIL"/> on failure</returns>
        public static ERRNO Encode(ReadOnlySpan<byte> input, Span<char> output, bool includePadding, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            int maxSize = GetMaxEncodedSize(input.Length);

            if (maxSize > MAX_STACKALLOC)
            {
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(maxSize);
                return EncodeToCharsCore(input, buffer.Span, output, encoding, includePadding);
            }
            else
            {
                Span<byte> buffer = stackalloc byte[maxSize];
                return EncodeToCharsCore(input, buffer, output, encoding, includePadding);
            }
        }

        /// <summary>
        /// Decodes a UTF-8 Base64URL encoded buffer to binary data. The output buffer must
        /// be at least as large as the decoded result; a safe upper bound is
        /// <c>utf8Input.Length</c> bytes (the binary output is always smaller than the encoded input).
        /// </summary>
        /// <param name="utf8Input">The UTF-8 Base64URL encoded input</param>
        /// <param name="output">The output buffer to write decoded binary data to</param>
        /// <returns>The number of bytes written, or <see cref="ERRNO.E_FAIL"/> on failure</returns>
        public static ERRNO Decode(ReadOnlySpan<byte> utf8Input, Span<byte> output)
        {
            if (utf8Input.IsEmpty || output.IsEmpty)
            {
                return ERRNO.E_FAIL;
            }

            // Allocate a separate intermediate buffer sized for the restored base64 plus
            // the padding bytes needed to reach a multiple-of-4 length before in-place decode.
            int padding = GetRequiredPadding(utf8Input.Length);
            int tempSize = utf8Input.Length + padding;

            // Prefer stack buffer for small encoding
            if (tempSize > MAX_STACKALLOC)
            {
                using UnsafeMemoryHandle<byte> temp = MemoryUtil.UnsafeAlloc(tempSize);
                return DecodeCore(utf8Input, temp.Span[..tempSize], output);
            }
            else
            {
                Span<byte> temp = stackalloc byte[tempSize];
                return DecodeCore(utf8Input, temp, output);
            }
        }

        /// <summary>
        /// Decodes a Base64URL character buffer to binary data.
        /// </summary>
        /// <param name="input">The Base64URL character input to decode</param>
        /// <param name="output">The output buffer to write decoded binary data to</param>
        /// <param name="encoding">Character encoding used to convert characters to bytes. Defaults to UTF-8.</param>
        /// <returns>The number of bytes written, or <see cref="ERRNO.E_FAIL"/> on failure</returns>
        public static ERRNO Decode(ReadOnlySpan<char> input, Span<byte> output, Encoding? encoding = null)
        {
            if (input.IsEmpty || output.IsEmpty)
            {
                return ERRNO.E_FAIL;
            }

            encoding ??= Encoding.UTF8; // Default to utf8 encoding as a standard for base64

            int byteCount = encoding.GetByteCount(input);

            // Prefer stack buffer for small encoding
            if (byteCount > MAX_STACKALLOC)
            {
                using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(byteCount);

                int count = encoding.GetBytes(input, buffer.Span);

                return Decode(buffer.Span[..count], output);
            }
            else
            {
                Span<byte> buffer = stackalloc byte[byteCount];

                int count = encoding.GetBytes(input, buffer);

                return Decode(buffer[..count], output);
            }
        }

        #endregion
    }
}
