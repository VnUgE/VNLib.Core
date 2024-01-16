/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ERRNO.cs 
*
* ERRNO.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.InteropServices;

namespace VNLib.Utils
{
    /// <summary>
    /// Implements a C style integer error code type. Size is platform dependent
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ERRNO : IEquatable<ERRNO>, ISpanFormattable, IFormattable
    {
        /// <summary>
        /// Represents a successfull error code (true)
        /// </summary>
        public static readonly ERRNO SUCCESS = true;

        /// <summary>
        /// Represents a failure error code (false)
        /// </summary>
        public static readonly ERRNO E_FAIL = false;
        
        private readonly nint ErrorCode;

        /// <summary>
        /// Creates a new <see cref="ERRNO"/> from the specified error value
        /// </summary>
        /// <param name="errno">The value of the error to represent</param>
        public ERRNO(nint errno) => ErrorCode = errno;

        /// <summary>
        /// Creates a new <see cref="ERRNO"/> from an <see cref="int"/> error code. null = 0 = false
        /// </summary>
        /// <param name="errorVal">Error code</param>
        public static implicit operator ERRNO(int errorVal) => new (errorVal);

        /// <summary>
        /// Creates a new <see cref="ERRNO"/> from an <see cref="int"/> error code. null = 0 = false
        /// </summary>
        /// <param name="errorVal">Error code</param>
        public static explicit operator ERRNO(int? errorVal) => new(errorVal ?? 0);

        /// <summary>
        /// Creates a new <see cref="ERRNO"/> from a booleam, 1 if true, 0 if false
        /// </summary>
        /// <param name="errorVal"></param>
        public static implicit operator ERRNO(bool errorVal) => new(errorVal ? 1 : 0);

        /// <summary>
        /// Creates a new <see cref="ERRNO"/> from a pointer value
        /// </summary>
        /// <param name="errno">The pointer value representing an error code</param>
        public static implicit operator ERRNO(nint errno) => new(errno);

        /// <summary>
        /// Error value as integer. Value of supplied error code or if cast from boolean 1 if true, 0 if false
        /// </summary>
        /// <param name="errorVal"><see cref="ERRNO"/> to get error code from</param>
        public static implicit operator int(ERRNO errorVal) => (int)errorVal.ErrorCode;

        /// <summary>
        /// C style boolean conversion. false if 0, true otherwise 
        /// </summary>
        /// <param name="errorVal"></param>
        public static implicit operator bool(ERRNO errorVal) => errorVal != 0;   

        /// <summary>
        /// Creates a new <c>nint</c> from the value if the stored error code 
        /// </summary>
        /// <param name="errno">The <see cref="ERRNO"/> contating the pointer value</param>
        public static implicit operator nint(ERRNO errno) => errno.ErrorCode;

        /// <summary>
        /// Compares the value of this error code to another and returns true if they are equal
        /// </summary>
        /// <param name="other">The value to compare</param>
        /// <returns>True if the ERRNO value is equal to the current value</returns>
        public readonly bool Equals(ERRNO other) => ErrorCode == other.ErrorCode;

        /// <summary>
        /// Compares the value of this error code to another and returns true if they are equal. 
        /// You should avoid this overload as it will box the value type.
        /// </summary>
        /// <param name="obj">The instance to compare</param>
        /// <returns>True if the ERRNO value is equal to the current value</returns>
        public readonly override bool Equals(object? obj) => obj is ERRNO other && Equals(other);

        /// <summary>
        /// Returns the hash code of the underlying value
        /// </summary>
        /// <returns>The hashcode of the current value</returns>
        public readonly override int GetHashCode() => ErrorCode.GetHashCode();

        /// <summary>
        /// Attempts to parse the value of the character sequence as a new error code
        /// </summary>
        /// <param name="value">The character sequence value to parse</param>
        /// <param name="result">The value </param>
        /// <returns>True if the value was successfully parsed, false othwerwise</returns>
        public static bool TryParse(ReadOnlySpan<char> value, out ERRNO result)
        {
            result = 0;
            if (nint.TryParse(value, out nint res))
            {
                result = new ERRNO(res);
                return true;
            }
            return false;
        }

        /// <summary>
        /// The integer error value of the current instance in radix 10
        /// </summary>
        /// <returns>The radix 10 formatted error code</returns>
        public readonly override string ToString() => ErrorCode.ToString();

        /// <summary>
        /// Formats the internal nint error code as a string in specified format
        /// </summary>
        /// <param name="format">The format to use</param>
        /// <returns>The formatted error code</returns>
        public readonly string ToString(string format) => ErrorCode.ToString(format);

        ///<inheritdoc/>
        public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => ErrorCode.TryFormat(destination, out charsWritten, format, provider);

        ///<inheritdoc/>
        public readonly string ToString(string? format, IFormatProvider? formatProvider) => ErrorCode.ToString(format, formatProvider);

        public static ERRNO operator +(ERRNO err, int add) => new(err.ErrorCode + add);
        public static ERRNO operator +(ERRNO err, nint add) => new(err.ErrorCode + add);
        public static ERRNO operator ++(ERRNO err) => new(err.ErrorCode + 1);
        public static ERRNO operator --(ERRNO err) => new(err.ErrorCode - 1);
        public static ERRNO operator -(ERRNO err, int subtract) => new(err.ErrorCode - subtract);
        public static ERRNO operator -(ERRNO err, nint subtract) => new(err.ErrorCode - subtract);

        public static bool operator >(ERRNO err, ERRNO other) => err.ErrorCode > other.ErrorCode;
        public static bool operator <(ERRNO err, ERRNO other) => err.ErrorCode < other.ErrorCode;
        public static bool operator >=(ERRNO err, ERRNO other) => err.ErrorCode >= other.ErrorCode;
        public static bool operator <=(ERRNO err, ERRNO other) => err.ErrorCode <= other.ErrorCode;

        public static bool operator >(ERRNO err, int other) => err.ErrorCode > other;
        public static bool operator <(ERRNO err, int other) => err.ErrorCode < other;
        public static bool operator >=(ERRNO err, int other) => err.ErrorCode >= other;
        public static bool operator <=(ERRNO err, int other) => err.ErrorCode <= other;

        public static bool operator >(ERRNO err, nint other) => err.ErrorCode > other;
        public static bool operator <(ERRNO err, nint other) => err.ErrorCode < other;
        public static bool operator >=(ERRNO err, nint other) => err.ErrorCode >= other;
        public static bool operator <=(ERRNO err, nint other) => err.ErrorCode <= other;

        public static bool operator ==(ERRNO err, ERRNO other) => err.ErrorCode == other.ErrorCode;
        public static bool operator !=(ERRNO err, ERRNO other) => err.ErrorCode != other.ErrorCode;
        public static bool operator ==(ERRNO err, int other) => err.ErrorCode == other;
        public static bool operator !=(ERRNO err, int other) => err.ErrorCode != other;
        public static bool operator ==(ERRNO err, nint other) => err.ErrorCode == other;
        public static bool operator !=(ERRNO err, nint other) => err.ErrorCode != other;

        
    }
}
