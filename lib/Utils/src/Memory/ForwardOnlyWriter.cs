/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ForwardOnlyWriter.cs 
*
* ForwardOnlyWriter.cs is part of VNLib.Utils which is part of the larger 
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
using System.Runtime.CompilerServices;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides a stack based buffer writer
    /// </summary>
    public ref struct ForwardOnlyWriter<T>
    {
        /// <summary>
        /// The buffer for writing output data to
        /// </summary>
        public readonly Span<T> Buffer { get; }

        /// <summary>
        /// The number of characters written to the buffer
        /// </summary>
        public int Written { readonly get; set; }

        /// <summary>
        /// The number of characters remaining in the buffer
        /// </summary>
        public readonly int RemainingSize => Buffer.Length - Written;

        /// <summary>
        /// The remaining buffer window
        /// </summary>
        public readonly Span<T> Remaining => Buffer[Written..];

        /// <summary>
        /// Creates a new <see cref="ForwardOnlyWriter{T}"/> assigning the specified buffer
        /// </summary>
        /// <param name="buffer">The buffer to write data to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ForwardOnlyWriter(Span<T> buffer)
        {
            Buffer = buffer;
            Written = 0;
        }

        /// <summary>
        /// Creates a new <see cref="ForwardOnlyWriter{T}"/> assigning the specified buffer
        /// at the specified offset
        /// </summary>
        /// <param name="buffer">The buffer to write data to</param>
        /// <param name="offset">The offset to begin the writer at</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ForwardOnlyWriter(Span<T> buffer, int offset)
        {
            Buffer = buffer[offset..];
            Written = 0;
        }

        /// <summary>
        /// Returns a compiled string from the characters written to the buffer
        /// </summary>
        /// <returns>A string of the characters written to the buffer</returns>
        public readonly override string ToString() => Buffer[..Written].ToString();

        /// <summary>
        /// Appends a sequence to the buffer
        /// </summary>
        /// <param name="data">The data to append to the buffer</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Append(ReadOnlySpan<T> data)
        {
            //Make sure the current window is large enough to buffer the new string
            ArgumentOutOfRangeException.ThrowIfGreaterThan(data.Length, RemainingSize, nameof(Remaining));
            Span<T> window = Buffer[Written..];
            //write data to window
            data.CopyTo(window);
            //update char position
            Written += data.Length;
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
            Buffer[Written++] = c;
        }

        /// <summary>
        /// Advances the writer forward the specifed number of elements
        /// </summary>
        /// <param name="count">The number of elements to advance the writer by</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, RemainingSize, nameof(Remaining));
            Written += count;
        }

        /// <summary>
        /// Resets the writer by setting the <see cref="Written"/> 
        /// property to 0.
        /// </summary>
        public void Reset() => Written = 0;
    }
}
