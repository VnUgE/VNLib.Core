/*
* Copyright (c) 2024 Vaughn Nugent
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
    public class PrivateString : 
        PrivateStringManager, 
        IEquatable<PrivateString>, 
        IEquatable<string>, 
        ICloneable
    {
        /// <summary>
        /// Gets the internal string referrence
        /// </summary>
        protected string StringRef => base[0]!;

        /// <summary>
        /// Does the current instance "own" the memory the data parameter points to
        /// </summary>
        protected bool OwnsReferrence { get; }

        /// <summary>
        /// The internal string's length
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public int Length => StringRef.Length;

        /// <summary>
        /// Creates a new <see cref="PrivateString"/> over the specified string and the memory it points to.
        /// </summary>
        /// <param name="data">The <see cref="string"/> instance pointing to the memory to protect</param>
        /// <param name="ownsReferrence">Does the current instance "own" the memory the data parameter points to</param>
        /// <remarks>You should no longer reference the input string directly</remarks>
        /// <exception cref="ArgumentException"></exception>
        public PrivateString(string data, bool ownsReferrence) : base(1)
        {
            //Create a private string manager to store referrence to string
            base[0] = data ?? throw new ArgumentNullException(nameof(data));
            OwnsReferrence = ownsReferrence;
        }

        /// <summary>
        /// Gets the value of the internal string as a <see cref="ReadOnlySpan{T}"/>
        /// </summary>
        /// <returns>The <see cref="ReadOnlySpan{T}"/> referrence to the internal string</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public ReadOnlySpan<char> ToReadOnlySpan() => StringRef.AsSpan();

        /// <summary>
        /// Creates a new deep copy of the current instance that 
        /// is an independent <see cref="PrivateString"/>
        /// </summary>
        /// <returns>The new <see cref="PrivateString"/> instance</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual PrivateString Clone() => new(ToString(), true);

        ///<inheritdoc/>
        public bool Equals(string? other) => StringRef.Equals(other, StringComparison.Ordinal);

        ///<inheritdoc/>
        public bool Equals(PrivateString? other) => other is not null && StringRef.Equals(other.StringRef, StringComparison.Ordinal);

        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is PrivateString otherRef && StringRef.Equals(otherRef);

        ///<inheritdoc/>
        public bool Equals(ReadOnlySpan<char> other) => StringRef.AsSpan().SequenceEqual(other);

        /// <summary>
        /// Creates a deep copy of the internal string and returns that copy
        /// </summary>
        /// <returns>A deep copy of the internal string</returns>
        public override string ToString() => CopyStringAtIndex(0)!;

        /// <summary>
        /// The hashcode of the underlying string
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() => Disposed ? 0 : string.GetHashCode(StringRef, StringComparison.Ordinal);

        /// <summary>
        /// Creates a new deep copy of the current instance that 
        /// is an independent <see cref="PrivateString"/>
        /// </summary>
        /// <returns>The new <see cref="PrivateString"/> instance</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        object ICloneable.Clone() => new PrivateString(ToString(), true);       

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

        ///<inheritdoc/>
        protected override void Free() => Erase();

        /// <summary>
        /// Indicates whether the underlying string is null or an empty string ("")
        /// </summary>
        /// <param name="ps"></param>
        /// <returns>True if the parameter is null, or an empty string (""). False otherwise</returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)] PrivateString? ps) => ps is null || ps.Length == 0;

        /// <summary>
        /// A nullable cast to a <see cref="PrivateString"/>
        /// </summary>
        /// <param name="data"></param>
        [return:NotNullIfNotNull(nameof(data))]
        public static explicit operator PrivateString?(string? data) => ToPrivateString(data, true);

        /// <summary>
        /// Creates a new <see cref="PrivateString"/> if the data is not null that owns the memory 
        /// the string points to, null otherwise. 
        /// </summary>
        /// <param name="data">The string reference to wrap</param>
        /// <param name="ownsString">A value that indicates if the string memory is owned by the instance</param>
        /// <returns>The new private string wrapper, or null if the value is null</returns>
        [return:NotNullIfNotNull(nameof(data))]
        public static PrivateString? ToPrivateString(string? data, bool ownsString) => data == null ? null : new(data, ownsString);

        /// <summary>
        /// Casts the <see cref="PrivateString"/> to a <see cref="string"/>
        /// </summary>
        /// <param name="str"></param>
        [return: NotNullIfNotNull(nameof(str))]
        public static explicit operator string?(PrivateString? str) => str?.StringRef;

        /// <summary>
        /// Casts the <see cref="PrivateString"/> to a <see cref="ReadOnlySpan{T}"/>
        /// </summary>
        /// <param name="str"></param>
        public static implicit operator ReadOnlySpan<char>(PrivateString? str) => (str is null || str.Disposed) ? Span<char>.Empty : str.StringRef.AsSpan();
      
    }
}
