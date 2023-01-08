/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: PrivateString.cs 
*
* PrivateString.cs is part of VNLib.Utils which is part of the larger 
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
using System.Diagnostics.CodeAnalysis;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides a wrapper class that will have unsafe access to the memory of 
    /// the specified <see cref="string"/> provided during object creation. 
    /// </summary>
    /// <remarks>The value of the memory the protected string points to is undefined when the instance is disposed</remarks>
    public class PrivateString : PrivateStringManager, IEquatable<PrivateString>, IEquatable<string>, ICloneable
    {
        protected string StrRef => base[0]!;
        private readonly bool OwnsReferrence;

        /// <summary>
        /// Creates a new <see cref="PrivateString"/> over the specified string and the memory it points to.
        /// </summary>
        /// <param name="data">The <see cref="string"/> instance pointing to the memory to protect</param>
        /// <param name="ownsReferrence">Does the current instance "own" the memory the data parameter points to</param>
        /// <remarks>You should no longer reference the input string directly</remarks>
        public PrivateString(string data, bool ownsReferrence = true) : base(1)
        {
            //Create a private string manager to store referrence to string
            base[0] = data ?? throw new ArgumentNullException(nameof(data));
            OwnsReferrence = ownsReferrence;
        }

        //Create private string from a string
        public static explicit operator PrivateString?(string? data)
        {
            //Allow passing null strings during implicit casting
            return data == null ? null : new(data);
        }

        public static PrivateString? ToPrivateString(string? value)
        {
            return value == null ? null : new PrivateString(value, true);
        }

        //Cast to string
        public static explicit operator string (PrivateString str)
        {
            //Check if disposed, or return the string
            str.Check();
            return str.StrRef;
        }

        public static implicit operator ReadOnlySpan<char>(PrivateString str)
        {
            return str.Disposed ? Span<char>.Empty : str.StrRef.AsSpan();
        }

        /// <summary>
        /// Gets the value of the internal string as a <see cref="ReadOnlySpan{T}"/>
        /// </summary>
        /// <returns>The <see cref="ReadOnlySpan{T}"/> referrence to the internal string</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public ReadOnlySpan<char> ToReadOnlySpan()
        {
            Check();
            return StrRef.AsSpan();
        }

        ///<inheritdoc/>
        public bool Equals(string? other)
        {
            Check();
            return StrRef.Equals(other);
        }
        ///<inheritdoc/>
        public bool Equals(PrivateString? other)
        {
            Check();
            return other != null && StrRef.Equals(other.StrRef);
        }
        ///<inheritdoc/>
        public override bool Equals(object? other)
        {
            Check();
            return other is PrivateString otherRef && StrRef.Equals(otherRef);
        }
        ///<inheritdoc/>
        public bool Equals(ReadOnlySpan<char> other)
        {
            Check();
            return StrRef.AsSpan().SequenceEqual(other);
        }
        /// <summary>
        /// Creates a deep copy of the internal string and returns that copy
        /// </summary>
        /// <returns>A deep copy of the internal string</returns>
        public override string ToString()
        {
            Check();
            return new(StrRef.AsSpan());
        }
        /// <summary>
        /// String length
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public int Length
        {
            get
            {
                Check();
                return StrRef.Length;
            }
        }
        /// <summary>
        /// Indicates whether the underlying string is null or an empty string ("")
        /// </summary>
        /// <param name="ps"></param>
        /// <returns>True if the parameter is null, or an empty string (""). False otherwise</returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)] PrivateString? ps) => ps == null|| ps.Length == 0;

        /// <summary>
        /// The hashcode of the underlying string
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            Check();
            return StrRef.GetHashCode();
        }

        /// <summary>
        /// Creates a new deep copy of the current instance that is an independent <see cref="PrivateString"/>
        /// </summary>
        /// <returns>The new <see cref="PrivateString"/> instance</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public override object Clone()
        {
            Check();
            //Copy all contents of string to another reference 
            string clone = new (StrRef.AsSpan());
            //return a new private string
            return new PrivateString(clone, true);
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            Erase();
        }

        /// <summary>
        /// Erases the contents of the internal CLR string
        /// </summary>
        public void Erase()
        {
            //Only dispose the instance if we own the memory
            if (OwnsReferrence && !Disposed)
            {
                base.Free();
            }
        }

       
    }
}