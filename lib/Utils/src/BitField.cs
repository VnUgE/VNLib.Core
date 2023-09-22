/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: BitField.cs 
*
* BitField.cs is part of VNLib.Utils which is part of the larger 
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

using System.Runtime.CompilerServices;

namespace VNLib.Utils
{
    /// <summary>
    /// Represents a field of 64 bits that can be set or cleared using unsigned or signed masks
    /// </summary>
    public class BitField
    {
        private ulong Field;

        /// <summary>
        /// The readonly value of the <see cref="BitField"/>
        /// </summary>
        public ulong Value => Field;

        /// <summary>
        /// Creates a new <see cref="BitField"/> initialized to the specified value
        /// </summary>
        /// <param name="initial">Initial value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitField(ulong initial) => Field = initial;

        /// <summary>
        /// Creates a new <see cref="BitField"/> initialized to the specified value
        /// </summary>
        /// <param name="initial">Initial value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitField(long initial) => Field = unchecked((ulong)initial);

        /// <summary>
        /// Determines if the specified flag is set
        /// </summary>
        /// <param name="mask">The mask to compare against the field value</param>
        /// <returns>True if the flag(s) is currently set, false if flag is not set</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet(ulong mask) => (Field & mask) != 0;

        /// <summary>
        /// Determines if the specified flag is set
        /// </summary>
        /// <param name="mask">The mask to compare against the field value</param>
        /// <returns>True if the flag(s) is currently set, false if flag is not set</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet(long mask) => (Field & unchecked((ulong)mask)) != 0;

        /// <summary>
        /// Determines if the specified flag is set
        /// </summary>
        /// <param name="mask">The mask to compare against the field value</param>
        /// <returns>True if the flag(s) is currently set, false if flag is not set</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(ulong mask) => Field |= mask;

        /// <summary>
        /// Determines if the specified flag is set
        /// </summary>
        /// <param name="mask">The mask to compare against the field value</param>
        /// <returns>True if the flag(s) is currently set, false if flag is not set</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(long mask) => Field |= unchecked((ulong)mask);

        /// <summary>
        /// Sets or clears a flag(s) indentified by a mask based on the value
        /// </summary>
        /// <param name="mask">Mask used to identify flags</param>
        /// <param name="value">True to set a flag, false to clear a flag</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(ulong mask, bool value)
        {
            if (value)
            {
                Set(mask);
            }
            else
            {
                Clear(mask);
            }
        }

        /// <summary>
        /// Clears the flag identified by the specified mask
        /// </summary>
        /// <param name="mask">The mask used to clear the given flag</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(ulong mask) => Field &= ~mask;

        /// <summary>
        /// Clears the flag identified by the specified mask
        /// </summary>
        /// <param name="mask">The mask used to clear the given flag</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(long mask) => Field &= ~unchecked((ulong)mask);

        /// <summary>
        /// Clears all flags by setting the <see cref="Field"/> property value to 0
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAll() => Field = 0;
    }
}