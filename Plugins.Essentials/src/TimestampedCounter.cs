/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: TimestampedCounter.cs 
*
* TimestampedCounter.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;


#nullable enable

namespace VNLib.Plugins.Essentials
{
    /// <summary>
    /// Stucture that allows for convient storage of a counter value
    /// and a second precision timestamp into a 64-bit unsigned integer
    /// </summary>
    public readonly struct TimestampedCounter : IEquatable<TimestampedCounter>
    {
        /// <summary>
        /// The time the count was last modifed
        /// </summary>
        public readonly DateTimeOffset LastModified;
        /// <summary>
        /// The last failed login attempt count value
        /// </summary>
        public readonly uint Count;

        /// <summary>
        /// Initalizes a new flc structure with the current UTC date
        /// and the specified count value
        /// </summary>
        /// <param name="count">FLC current count</param>
        public TimestampedCounter(uint count) : this(DateTimeOffset.UtcNow, count)
        { }

        private TimestampedCounter(DateTimeOffset dto, uint count)
        {
            Count = count;
            LastModified = dto;
        }

        /// <summary>
        /// Compacts and converts the counter value and timestamp into
        /// a 64bit unsigned integer
        /// </summary>
        /// <param name="count">The counter to convert</param>
        public static explicit operator ulong(TimestampedCounter count) => count.ToUInt64();

        /// <summary>
        /// Compacts and converts the counter value and timestamp into
        /// a 64bit unsigned integer
        /// </summary>
        /// <returns>The uint64 compacted value</returns>
        public ulong ToUInt64()
        {
            //Upper 32 bits time, lower 32 bits count
            ulong value = (ulong)LastModified.ToUnixTimeSeconds() << 32;
            value |= Count;
            return value;
        }

        /// <summary>
        /// The previously compacted <see cref="TimestampedCounter"/> 
        /// value to cast back to a counter
        /// </summary>
        /// <param name="value"></param>
        public static explicit operator TimestampedCounter(ulong value) => FromUInt64(value);
      
        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is TimestampedCounter counter && Equals(counter);
        ///<inheritdoc/>
        public override int GetHashCode() => this.ToUInt64().GetHashCode();
        ///<inheritdoc/>
        public static bool operator ==(TimestampedCounter left, TimestampedCounter right) => left.Equals(right);
        ///<inheritdoc/>
        public static bool operator !=(TimestampedCounter left, TimestampedCounter right) => !(left == right);
        ///<inheritdoc/>
        public bool Equals(TimestampedCounter other) => ToUInt64() == other.ToUInt64();

        /// <summary>
        /// The previously compacted <see cref="TimestampedCounter"/> 
        /// value to cast back to a counter
        /// </summary>
        /// <param name="value">The uint64 encoded <see cref="TimestampedCounter"/></param>
        /// <returns>
        /// The decoded <see cref="TimestampedCounter"/> from its 
        /// compatcted representation
        /// </returns>
        public static TimestampedCounter FromUInt64(ulong value)
        {
            //Upper 32 bits time, lower 32 bits count
            long time = (long)(value >> 32);
            uint count = (uint)(value & uint.MaxValue);
            //Init dto struct
            return new(DateTimeOffset.FromUnixTimeSeconds(time), count);
        }
    }
}