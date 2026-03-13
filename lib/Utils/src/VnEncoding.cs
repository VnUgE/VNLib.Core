/*
* Copyright (c) 2026 Vaughn Nugent
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
using System.Text;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Codecs;

namespace VNLib.Utils
{

    /// <summary>
    /// Contains static methods for encoding data
    /// </summary>
    public static class VnEncoding
    {
        #region Base32        

        /// <inheritdoc cref="Base32.Encode(ReadOnlySpan{byte}, Span{char})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public static ERRNO TryToBase32Chars(ReadOnlySpan<byte> input, Span<char> output) 
            => Base32.Encode(input, output);

        /// <inheritdoc cref="Base32.Encode(ReadOnlySpan{byte}, ref ForwardOnlyWriter{char})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public static ERRNO TryToBase32Chars(ReadOnlySpan<byte> input, ref ForwardOnlyWriter<char> writer) 
            => Base32.Encode(input, ref writer);

        /// <inheritdoc cref="Base32.Decode(ReadOnlySpan{char}, Span{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public static ERRNO TryFromBase32Chars(ReadOnlySpan<char> input, Span<byte> output) 
            => Base32.Decode(input, output);

        /// <inheritdoc cref="Base32.GetDecodedSize(nint)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint Base32DecodedSizeSize(nint inputSize)
            => Base32.GetDecodedSize(inputSize);

        /// <inheritdoc cref="Base32.Decode(ReadOnlySpan{char}, ref ForwardOnlyWriter{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public unsafe static ERRNO TryFromBase32Chars(ReadOnlySpan<char> input, ref ForwardOnlyWriter<byte> writer)
            => Base32.Decode(input, ref writer);

        /// <inheritdoc cref="Base32.GetMaxBufferSize(int)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public static int Base32CalcMaxBufferSize(int bufferSize)
            => Base32.GetMaxBufferSize(bufferSize);

        /// <inheritdoc cref="Base32.Encode(ReadOnlySpan{byte}, bool)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public static string ToBase32String(ReadOnlySpan<byte> binBuffer, bool withPadding = false) 
            => Base32.Encode(binBuffer, withPadding);

        /// <inheritdoc cref="Base32.Deserialize{T}(ReadOnlySpan{char})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public static T FromBase32String<T>(ReadOnlySpan<char> base32) where T : unmanaged
            => Base32.Deserialize<T>(base32);

        /// <inheritdoc cref="Base32.Decode(ReadOnlySpan{char})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public static byte[]? FromBase32String(ReadOnlySpan<char> base32)
            => Base32.Decode(base32);

        /// <inheritdoc cref="Base32.Serialize{T}(T, bool)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base32 instead")]
        public static string ToBase32String<T>(T value, bool withPadding = false) where T : unmanaged
            => Base32.Serialize(value, withPadding);

        #endregion

        #region percent encoding

        /// <inheritdoc cref="PercentEncoding.GetEncodedSize(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.PercentEncoding.GetEncodedSize instead")]
        public static int PercentEncodeCalcBufferSize(ReadOnlySpan<byte> utf8Bytes, ReadOnlySpan<byte> allowedChars = default)
            => PercentEncoding.GetEncodedSize(utf8Bytes, allowedChars);

        /// <inheritdoc cref="PercentEncoding.Encode(ReadOnlySpan{byte}, Span{byte}, ReadOnlySpan{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.PercentEncoding.Encode instead")]
        public static ERRNO PercentEncode(ReadOnlySpan<byte> utf8Bytes, Span<byte> utf8Output, ReadOnlySpan<byte> allowedChars = default)
            => PercentEncoding.Encode(utf8Bytes, utf8Output, allowedChars);

        /// <inheritdoc cref="PercentEncoding.Encode(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.PercentEncoding.Encode instead")]
        public static string PercentEncode(ReadOnlySpan<byte> utf8Bytes, ReadOnlySpan<byte> allowedChars = default)
            => PercentEncoding.Encode(utf8Bytes, allowedChars);

        /// <inheritdoc cref="PercentEncoding.Decode(ReadOnlySpan{byte}, Span{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.PercentEncoding.Decode instead")]
        public static ERRNO PercentDecode(ReadOnlySpan<byte> utf8Encoded, Span<byte> utf8Output)
            => PercentEncoding.Decode(utf8Encoded, utf8Output);

        #endregion

        #region Base64

        /// <summary>
        /// Tries to convert the specified span containing a string representation that is 
        /// encoded with base-64 digits into a span of 8-bit unsigned integers.
        /// </summary>
        /// <param name="base64">Base64 character data to recover</param>
        /// <param name="buffer">The binary output buffer to write converted characters to</param>
        /// <returns>The number of bytes written, or <see cref="ERRNO.E_FAIL"/> of the conversion was unsuccessful</returns>
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
            return Convert.TryToBase64Chars(buffer, base64, out int charsWritten, options)
                ? charsWritten
                : ERRNO.E_FAIL;
        }

        /// <inheritdoc cref="Base64Url.GetRequiredPadding(int)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.GetRequiredPadding instead")]
        public static int Base64CalcRequiredPadding(int length) 
            => Base64Url.GetRequiredPadding(length);

        /// <inheritdoc cref="Base64Url.MakeUrlSafe(Span{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.MakeUrlSafe instead")]
        public static void Base64ToUrlSafeInPlace(Span<byte> base64) 
            => Base64Url.MakeUrlSafe(base64);

        /// <inheritdoc cref="Base64Url.RestoreBase64(Span{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.RestoreBase64 instead")]
        public static void Base64FromUrlSafeInPlace(Span<byte> uft8Base64Url) 
            => Base64Url.RestoreBase64(uft8Base64Url);

        /// <inheritdoc cref="Base64Url.MakeUrlSafe(ReadOnlySpan{byte}, Span{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.MakeUrlSafe instead")]
        public static ERRNO Base64ToUrlSafe(ReadOnlySpan<byte> base64, Span<byte> base64Url)
            => Base64Url.MakeUrlSafe(base64, base64Url);

        /// <inheritdoc cref="Base64Url.RestoreBase64(ReadOnlySpan{byte}, Span{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.RestoreBase64 instead")]
        public static ERRNO Base64FromUrlSafe(ReadOnlySpan<byte> base64Url, Span<byte> base64)
            => Base64Url.RestoreBase64(base64Url, base64);

        /// <inheritdoc cref="Base64Url.Decode(ReadOnlySpan{byte}, Span{byte})"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.Decode instead")]
        public static ERRNO Base64UrlDecode(ReadOnlySpan<byte> utf8Base64Url, Span<byte> output)
            => Base64Url.Decode(utf8Base64Url, output);

        /// <inheritdoc cref="Base64Url.Decode(ReadOnlySpan{char}, Span{byte}, Encoding?)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.Decode instead")]
        public static ERRNO Base64UrlDecode(ReadOnlySpan<char> chars, Span<byte> output, Encoding? encoding = null)
            => Base64Url.Decode(chars, output, encoding);

        /// <inheritdoc cref="Base64Url.EncodeInPlace(Span{byte}, int, bool)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.EncodeInPlace instead")]
        public static ERRNO Base64UrlEncodeInPlace(Span<byte> buffer, int dataLength, bool includePadding)
            => Base64Url.EncodeInPlace(buffer, dataLength, includePadding);

        /// <inheritdoc cref="Base64Url.EncodeInPlace(Span{byte}, int, bool)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.EncodeInPlace instead")]
        public static string ToBase64UrlSafeStringInPlace(Span<byte> rawData, int length, bool includePadding)
        {
            ERRNO converted = Base64Url.EncodeInPlace(rawData, length, includePadding);

            if (converted < 1)
            {
                throw new ArgumentException("The input buffer was not large enough to encode in-place", nameof(rawData));
            }

            return Encoding.UTF8.GetString(rawData[..(int)converted]);
        }

        /// <inheritdoc cref="Base64Url.Encode(ReadOnlySpan{byte}, bool, Encoding?)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.Encode instead")]
        public static string ToBase64UrlSafeString(ReadOnlySpan<byte> rawData, bool includePadding)
            => Base64Url.Encode(rawData, includePadding);

        /// <inheritdoc cref="Base64Url.Encode(ReadOnlySpan{byte}, Span{byte}, bool)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.Encode instead")]
        public static ERRNO Base64UrlEncode(ReadOnlySpan<byte> input, Span<byte> output, bool includePadding)
            => Base64Url.Encode(input, output, includePadding);

        /// <inheritdoc cref="Base64Url.Encode(ReadOnlySpan{byte}, bool, Encoding?)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.Encode instead")]
        public static string Base64UrlEncode(ReadOnlySpan<byte> input, bool includePadding, Encoding? encoding = null)
            => Base64Url.Encode(input, includePadding, encoding);

        /// <inheritdoc cref="Base64Url.Encode(ReadOnlySpan{byte}, Span{char}, bool, Encoding?)"/>
        [Obsolete("Use VNLib.Utils.Codecs.Base64Url.Encode instead")]
        public static ERRNO Base64UrlEncode(ReadOnlySpan<byte> input, Span<char> output, bool includePadding, Encoding? encoding = null)
            => Base64Url.Encode(input, output, includePadding, encoding);

        #endregion
    }
}
