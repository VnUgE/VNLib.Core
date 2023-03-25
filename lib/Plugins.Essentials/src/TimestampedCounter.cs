/*
* Copyright (c) 2023 Vaughn Nugent
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

namespace VNLib.Plugins.Essentials
{

    /// <summary>
    /// Stucture that allows for convient storage of a counter value
    /// and a second precision timestamp into a 64-bit unsigned integer
    /// </summary>
    public readonly record struct TimestampedCounter(uint Count, uint UnixMs)
    {
        /// <summary>
        /// The time the count was last modifed
        /// </summary>
        public readonly DateTimeOffset LastModified => DateTimeOffset.FromUnixTimeSeconds(UnixMs);

        /// <summary>
        /// Compacts and converts the counter value and timestamp into
        /// a 64bit unsigned integer
        /// </summary>
        /// <returns>The uint64 compacted value</returns>
        public readonly ulong ToUInt64()
        {
            //Upper 32 bits time, lower 32 bits count
            ulong value = UnixMs;
            //Lshift to upper32
            value <<= 32;
            //Set count as lower32
            value |= Count;
            return value;
        }

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
            uint time = (uint)(value >> 32);
            uint count = (uint)(value & uint.MaxValue);

            return new TimestampedCounter(count, time);
        }

        /// <summary>
        /// Creates a new <see cref="TimestampedCounter"/> from the given counter 
        /// value and the <see cref="DateTimeOffset"/> unix ms value
        /// </summary>
        /// <param name="count"></param>
        /// <param name="time">The internal time to store in the counter</param>
        /// <returns>An initialized <see cref="TimestampedCounter"/></returns>
        /// <exception cref="StackOverflowException"></exception>
        public static TimestampedCounter FromValues(uint count, DateTimeOffset time)
        {
            //The time in seconds truncated to a uint32
            uint sec = Convert.ToUInt32(time.ToUnixTimeSeconds());
            return new TimestampedCounter(count, sec);
        }

        /// <summary>
        /// The previously compacted <see cref="TimestampedCounter"/> 
        /// value to cast back to a counter
        /// </summary>
        /// <param name="value"></param>
        public static explicit operator TimestampedCounter(ulong value) => FromUInt64(value);

        /// <summary>
        /// Compacts and converts the counter value and timestamp into
        /// a 64bit unsigned integer
        /// </summary>
        /// <param name="count">The counter to convert</param>
        public static explicit operator ulong(TimestampedCounter count) => count.ToUInt64();
    }
}