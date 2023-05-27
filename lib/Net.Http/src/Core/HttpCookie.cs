/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpCookie.cs 
*
* HttpCookie.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http.Core
{
    internal sealed class HttpCookie : IStringSerializeable, IEquatable<HttpCookie>
    {
        public string Name { get; }
        public string? Value { get; init; }
        public string? Domain { get; init; }
        public string? Path { get; init; }
        public TimeSpan MaxAge { get; init; }
        public CookieSameSite SameSite { get; init; }
        public bool Secure { get; init; }
        public bool HttpOnly { get; init; }
        public bool IsSession { get; init; }

        public HttpCookie(string name)
        {
            this.Name = name;
        }

        public string Compile()
        {
            throw new NotImplementedException();
        }
        public void Compile(ref ForwardOnlyWriter<char> writer)
        {
            //set the name of the cookie
            writer.Append(Name);
            writer.Append('=');
            //set name
            writer.Append(Value);
            //Only set the max age parameter if the cookie is not a session cookie
            if (!IsSession)
            {
                writer.Append("; Max-Age=");
                writer.Append((int)MaxAge.TotalSeconds);
            }
            //Make sure domain is set
            if (!string.IsNullOrWhiteSpace(Domain))
            {
                writer.Append("; Domain=");
                writer.Append(Domain);
            }
            //Check and set path
            if (!string.IsNullOrWhiteSpace(Path))
            {
                //Set path
                writer.Append("; Path=");
                writer.Append(Path);
            }
            writer.Append("; SameSite=");
            //Set the samesite flag based on the enum value
            switch (SameSite)
            {
                case CookieSameSite.None:
                    writer.Append("None");
                    break;
                case CookieSameSite.SameSite:
                    writer.Append("Strict");
                    break;
                case CookieSameSite.Lax:
                default:
                    writer.Append("Lax");
                    break;
            }
            //Set httponly flag
            if (HttpOnly)
            {
                writer.Append("; HttpOnly");
            }
            //Set secure flag
            if (Secure)
            {
                writer.Append("; Secure");
            }
        }
        public ERRNO Compile(Span<char> buffer)
        {
            ForwardOnlyWriter<char> writer = new(buffer);
            Compile(ref writer);
            return writer.Written;
        }

        public override int GetHashCode() => Name.GetHashCode();

        public override bool Equals(object? obj)
        {
            return obj is HttpCookie other && Equals(other);
        }

        public bool Equals(HttpCookie? other)
        {
            return other != null && Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}