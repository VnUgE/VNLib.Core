/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMMessageHeader.cs 
*
* FBMMessageHeader.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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


namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// Represents a Key-Value pair FBM response message header
    /// </summary>
    public readonly struct FBMMessageHeader : IEquatable<FBMMessageHeader>
    {
        private readonly IFBMHeaderBuffer _buffer;

        /*
         * Header values are all stored consecutively inside a single
         * buffer, position the value is stored within the buffer
         * is designated by it's absolute offset and length within
         * the buffer segment
         */
        private readonly int _headerOffset;
        private readonly int _headerLength;

        /// <summary>
        /// The header value or command
        /// </summary>
        public readonly HeaderCommand Header { get; }

        /// <summary>
        /// The header value, or <see cref="ReadOnlySpan{T}.Empty"/> if default
        /// </summary>
        public readonly ReadOnlySpan<char> Value => _buffer != null ? _buffer.GetSpan(_headerOffset, _headerLength) : null;

        /// <summary>
        /// Gets the header value as a <see cref="string"/> or null if default instance
        /// </summary>
        /// <returns>The allocates string of the value</returns>
        public readonly string? GetValueString() => _buffer != null ? Value.ToString() : null;

        /// <summary>
        /// Initializes a new <see cref="FBMMessageHeader"/> of the sepcified command 
        /// that utilizes memory from the specified buffer
        /// </summary>
        /// <param name="buffer">The buffer that owns the memory our header is stored in</param>
        /// <param name="command">The command this header represents</param>
        /// <param name="offset">The char offset within the buffer our header begins</param>
        /// <param name="length">The character length of our header value</param>
        internal FBMMessageHeader(IFBMHeaderBuffer buffer, HeaderCommand command, int offset, int length)
        {
            Header = command;
            _buffer = buffer;
            _headerLength = length;
            _headerOffset = offset;
        }

        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is FBMMessageHeader other && Equals(other);

        /// <summary>
        /// Calculates a hash code from the <see cref="Value"/> parameter
        /// using original string hashcode computation
        /// </summary>
        /// <returns>The unique hashcode for the <see cref="Value"/> character sequence</returns>
        public override int GetHashCode() => string.GetHashCode(Value, StringComparison.Ordinal);

        ///<inheritdoc/>
        public static bool operator ==(FBMMessageHeader left, FBMMessageHeader right) => left.Equals(right);
        ///<inheritdoc/>
        public static bool operator !=(FBMMessageHeader left, FBMMessageHeader right) => !(left == right);

        /// <summary>
        /// Determines if the other response header is equal to the current header by 
        /// comparing its command and its value
        /// </summary>
        /// <param name="other">The other header to compare</param>
        /// <returns>True if both headers have the same commad and value sequence</returns>
        public bool Equals(FBMMessageHeader other) => Header == other.Header && Value.SequenceEqual(other.Value);

        /// <summary>
        /// Gets a concatinated string of the current instance for debugging purposes
        /// </summary>
        public readonly override string ToString() => $"{Header}:{Value.ToString()}";
    }
}
