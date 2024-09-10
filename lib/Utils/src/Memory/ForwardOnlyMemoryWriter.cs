/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ForwardOnlyMemoryWriter.cs 
*
* ForwardOnlyMemoryWriter.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides a mutable sliding buffer writer
    /// </summary>
    /// <param name="Buffer">The buffer to write data to</param>
    public record struct ForwardOnlyMemoryWriter<T>(Memory<T> Buffer)
    {
        private int _written;

        /// <summary>
        /// The number of characters written to the buffer
        /// </summary>
        public int Written
        {
            readonly get => _written;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                _written = value;
            }
        }

        /// <summary>
        /// The number of characters remaining in the buffer
        /// </summary>
        public readonly int RemainingSize => Buffer.Length - _written;

        /// <summary>
        /// The remaining buffer window
        /// </summary>
        public readonly Memory<T> Remaining => Buffer[_written..];
        
        /// <summary>
        /// Returns a compiled string from the characters written to the buffer
        /// </summary>
        /// <returns>A string of the characters written to the buffer</returns>
        public readonly override string ToString() => Buffer[.._written].ToString();

        /// <summary>
        /// Appends a sequence to the buffer
        /// </summary>
        /// <param name="data">The data to append to the buffer</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Append(ReadOnlyMemory<T> data)
        {
            //Make sure the current window is large enough to buffer the new string
            ArgumentOutOfRangeException.ThrowIfGreaterThan(data.Length, RemainingSize, nameof(data));

            Memory<T> window = Buffer[_written..];

            //write data to window
            data.CopyTo(window);

            Advance(data.Length);
        }

        /// <summary>
        /// Appends a single item to the buffer
        /// </summary>
        /// <param name="c">The item to append to the buffer</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Append(T c)
        {
            //Make sure the current window is large enough to buffer the new string
            ArgumentOutOfRangeException.ThrowIfZero(RemainingSize);

            //Write data to buffer and increment the buffer position
            Buffer.Span[_written++] = c;
        }

        /// <summary>
        /// Advances the writer forward the specifed number of elements
        /// </summary>
        /// <param name="count">The number of elements to advance the writer by</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, RemainingSize);

            _written += count;
        }

        /// <summary>
        /// Resets the writer by setting the <see cref="Written"/> 
        /// property to 0.
        /// </summary>
        public void Reset() => _written = 0;
    }
}
