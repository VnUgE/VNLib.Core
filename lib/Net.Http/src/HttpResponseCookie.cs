/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpResponseCookie.cs 
*
* HttpResponseCookie.cs is part of VNLib.Net.Http which is part of the larger 
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

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents an HTTP cookie that is set with responses.
    /// </summary>
    /// <param name="name">The cookie name</param>
    public readonly struct HttpResponseCookie(string name) : IStringSerializeable, IEquatable<HttpResponseCookie>
    {
        /// <summary>
        /// The default copy buffer allocated when calling the <see cref="Compile()"/>
        /// family of methods.
        /// </summary>
        public const int DefaultCookieBufferSize = 4096;


        /// <summary>
        /// The name of the cookie to set.
        /// </summary>
        public readonly string Name { get; } = name;

        /// <summary>
        /// The actual cookie content or value.
        /// </summary>
        public readonly string? Value { get; init; }

        /// <summary>
        /// The domain this cookie will be sent to.
        /// </summary>
        public readonly string? Domain { get; init; }

        /// <summary>
        /// The cookie path the client will send this cookie with. Null 
        /// or empty string for all paths.
        /// </summary>
        public readonly string? Path { get; init; }

        /// <summary>
        /// Sets the duration of the cookie lifetime (in seconds), aka MaxAge
        /// </summary>
        public readonly TimeSpan MaxAge { get; init; }

        /// <summary>
        /// Sets the cookie Samesite field.
        /// </summary>
        public readonly CookieSameSite SameSite { get; init; }

        /// <summary>
        /// Sets the cookie Secure flag. If true only sends the cookie with requests
        /// if the connection is secure.
        /// </summary>
        public readonly bool Secure { get; init; }

        /// <summary>
        /// Sets cookie HttpOnly flag. If true denies JavaScript access to 
        /// </summary>
        public readonly bool HttpOnly { get; init; }

        /// <summary>
        /// Sets the cookie expiration to the duration of the user's session (aka no expiration)
        /// </summary>
        public readonly bool IsSession { get; init; }

        /// <summary>
        /// Creates an HTTP 1.x spec cookie header value from the 
        /// cookie fields
        /// <para>
        /// The internal copy buffer defaults to <see cref="DefaultCookieBufferSize"/>
        /// use <see cref="Compile(int)"/> if you need control over the buffer size
        /// </para>
        /// </summary>
        /// <returns>The cookie header value as a string</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public readonly string Compile()
        {
            nint bufSize = MemoryUtil.NearestPage(DefaultCookieBufferSize);

            return Compile(bufSize.ToInt32());
        }

        /// <summary>
        /// Creates an HTTP 1.x spec cookie header value from the 
        /// cookie fields.
        /// </summary>
        /// <param name="bufferSize">The size of the internal accumulator buffer</param>
        /// <returns>The cookie header value as a string</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public readonly string Compile(int bufferSize)
        {
            using UnsafeMemoryHandle<char> cookieBuffer = MemoryUtil.UnsafeAlloc<char>(bufferSize, false);

            ERRNO count = Compile(cookieBuffer.Span);

            return cookieBuffer.AsSpan(0, (int)count).ToString();
        }

        /// <summary>
        /// Creates an HTTP 1.x spec cookie header value from the 
        /// cookie fields.
        /// </summary>
        /// <param name="buffer">The character buffer to write the cookie data tor</param>
        /// <returns>The cookie header value as a string</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public readonly ERRNO Compile(Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }

        /// <summary>
        /// Writes the HTTP 1.x header format for the cookie
        /// </summary>
        /// <param name="writer"></param>
        public readonly void Compile(ref ForwardOnlyWriter<char> writer)
        {
            writer.Append(Name);
            writer.Append('=');
            writer.Append(Value);

            /*
             * If a session cookie is set, then do not include a max-age value
             * browsers will default to session duration if not set
             */
            if (!IsSession)
            {
                writer.AppendSmall("; Max-Age=");
                writer.Append((int)MaxAge.TotalSeconds);
            }

            if (!string.IsNullOrWhiteSpace(Domain))
            {
                writer.AppendSmall("; Domain=");
                writer.Append(Domain);
            }

            if (!string.IsNullOrWhiteSpace(Path))
            {
                //Set path
                writer.AppendSmall("; Path=");
                writer.Append(Path);
            }

            writer.AppendSmall("; SameSite=");

            switch (SameSite)
            {
                case CookieSameSite.None:
                    writer.AppendSmall("None");
                    break;
                case CookieSameSite.Strict:
                    writer.AppendSmall("Strict");
                    break;
                case CookieSameSite.Lax:
                default:
                    writer.AppendSmall("Lax");
                    break;
            }

            if (HttpOnly)
            {
                writer.AppendSmall("; HttpOnly");
            }

            if (Secure)
            {
                writer.AppendSmall("; Secure");
            }
        }

        ///<inheritdoc/>
        public readonly override int GetHashCode() => string.GetHashCode(Name, StringComparison.OrdinalIgnoreCase);

        ///<inheritdoc/>
        public readonly override bool Equals(object? obj) => obj is HttpResponseCookie other && Equals(other);

        ///<inheritdoc/>
        public readonly bool Equals(HttpResponseCookie other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Creates an HTTP 1.x spec cookie header value from the 
        /// cookie fields
        /// <para>
        /// The internal copy buffer defaults to <see cref="DefaultCookieBufferSize"/>
        /// use <see cref="Compile(int)"/> if you need control over the buffer size
        /// </para>
        /// </summary>
        /// <returns>The cookie header value as a string</returns>
        public override string ToString() => Compile();

        ///<inheritdoc/>
        public static bool operator ==(HttpResponseCookie left, HttpResponseCookie right) => left.Equals(right);

        ///<inheritdoc/>
        public static bool operator !=(HttpResponseCookie left, HttpResponseCookie right) => !(left == right);
    }
}