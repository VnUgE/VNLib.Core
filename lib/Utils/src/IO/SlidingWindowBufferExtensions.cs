/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: SlidingWindowBufferExtensions.cs 
*
* SlidingWindowBufferExtensions.cs is part of VNLib.Utils which is part of the larger 
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Extention methods for <see cref="ISlindingWindowBuffer{T}"/>
    /// </summary>
    public static class SlidingWindowBufferExtensions
    {
        /// <summary>
        /// Shifts/resets the current buffered data window down to the 
        /// beginning of the buffer if the buffer window is shifted away 
        /// from the beginning.
        /// </summary>
        /// <returns>The number of bytes of available space in the buffer</returns>
        public static ERRNO CompactBufferWindow<T>(this ISlindingWindowBuffer<T> sBuf)
        {
            //Nothing to compact if the starting data pointer is at the beining of the window
            if (sBuf.WindowStartPos > 0)
            {
                //Get span over entire buffer
                Span<T> buffer = sBuf.Buffer.Span;
                //Get data within window
                Span<T> usedData = sBuf.Accumulated;
                //Copy remaining to the beginning of the buffer
                usedData.CopyTo(buffer);

                //Reset positions, then advance to the specified size
                sBuf.Reset();
                sBuf.Advance(usedData.Length);
            }
            //Return the number of bytes of available space
            return sBuf.RemainingSize;
        }

        /// <summary>
        /// Appends the specified data to the end of the buffer 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sBuf"></param>
        /// <param name="val">The value to append to the end of the buffer</param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append<T>(this IDataAccumulator<T> sBuf, T val)
        {
            //Set the value at first position
            sBuf.Remaining[0] = val;
            //Advance by 1
            sBuf.Advance(1);
        }

        /// <summary>
        /// Appends the specified data to the end of the buffer 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sBuf"></param>
        /// <param name="val">The value to append to the end of the buffer</param>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append<T>(this IDataAccumulator<T> sBuf, ReadOnlySpan<T> val)
        {
            val.CopyTo(sBuf.Remaining);
            sBuf.Advance(val.Length);
        }

        /// <summary>
        /// Formats and appends a value type to the accumulator with proper endianess
        /// </summary>
        /// <typeparam name="T">The value type to appent</typeparam>
        /// <param name="accumulator">The binary accumulator to append</param>
        /// <param name="value">The value type to append</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append<T>(this IDataAccumulator<byte> accumulator, T value) where T: unmanaged
        {
            //Use forward reader for the memory extension to append a value type to a binary accumulator
            ForwardOnlyWriter<byte> w = new(accumulator.Remaining);
            w.Append(value);
            accumulator.Advance(w.Written);
        }

        /// <summary>
        /// Attempts to write as much data as possible to the remaining space
        /// in the buffer and returns the number of bytes accumulated.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="accumulator"></param>
        /// <param name="value">The value to accumulate</param>
        /// <returns>The number of bytes accumulated</returns>
        public static ERRNO TryAccumulate<T>(this IDataAccumulator<T> accumulator, ReadOnlySpan<T> value)
        {
            //Calc data size and reserve space for final crlf
            int dataToCopy = Math.Min(value.Length, accumulator.RemainingSize);

            //Write as much data as possible
            accumulator.Append(value[..dataToCopy]);

            //Return number of bytes not written
            return dataToCopy;
        }

        /// <summary>
        /// Appends a <see cref="ISpanFormattable"/> instance to the end of the accumulator
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="accumulator"></param>
        /// <param name="formattable">The formattable instance to write to the accumulator</param>
        /// <param name="format">The format arguments</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append<T>(this IDataAccumulator<char> accumulator, in T formattable, ReadOnlySpan<char> format = default) where T : struct, ISpanFormattable
        {
            ForwardOnlyWriter<char> writer = new(accumulator.Remaining);
            writer.Append(formattable, format);
            accumulator.Advance(writer.Written);
        }

        /// <summary>
        /// Uses the remaining data buffer to compile a <see cref="IStringSerializeable"/>
        /// instance, then advances the accumulator by the number of characters used.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="accumulator"></param>
        /// <param name="compileable">The <see cref="IStringSerializeable"/> instance to compile</param>
        public static void Append<T>(this IDataAccumulator<char> accumulator, in T compileable) where T : IStringSerializeable
        {
            //Write directly to the remaining space
            int written = compileable.Compile(accumulator.Remaining);
            //Advance the writer
            accumulator.Advance(written);
        }

        /// <summary>
        /// Reads available data from the current window and writes as much as possible it to the supplied buffer
        /// and advances the buffer window
        /// </summary>
        /// <typeparam name="T">Element type</typeparam>
        /// <param name="sBuf"></param>
        /// <param name="buffer">The output buffer to write data to</param>
        /// <returns>The number of elements written to the buffer</returns>
        public static ERRNO Read<T>(this ISlindingWindowBuffer<T> sBuf, Span<T> buffer)
        {
            //Calculate the amount of data to copy
            int dataToCopy = Math.Min(buffer.Length, sBuf.AccumulatedSize);
            //Copy the data to the buffer
            sBuf.Accumulated[..dataToCopy].CopyTo(buffer);
            //Advance the window
            sBuf.AdvanceStart(dataToCopy);
            //Return the number of bytes copied
            return dataToCopy;
        }

        /// <summary>
        /// Fills the remaining window space of the current accumulator with 
        /// data from the specified stream asynchronously.
        /// </summary>
        /// <param name="accumulator"></param>
        /// <param name="input">The stream to read data from</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A value task representing the operation</returns>
        public static async ValueTask AccumulateDataAsync(this ISlindingWindowBuffer<byte> accumulator, Stream input, CancellationToken cancellationToken)
        {
            //Get a buffer from the end of the current window to the end of the buffer
            Memory<byte> bufWindow = accumulator.RemainingBuffer;
            //Read from stream async
            int read = await input.ReadAsync(bufWindow, cancellationToken);
            //Update the end of the buffer window to the end of the read data
            accumulator.Advance(read);
        }

        /// <summary>
        /// Fills the remaining window space of the current accumulator with 
        /// data from the specified stream.
        /// </summary>
        /// <param name="accumulator"></param>
        /// <param name="input">The stream to read data from</param>
        public static void AccumulateData(this IDataAccumulator<byte> accumulator, Stream input)
        {
            //Get a buffer from the end of the current window to the end of the buffer
            Span<byte> bufWindow = accumulator.Remaining;
            //Read from stream async
            int read = input.Read(bufWindow);
            //Update the end of the buffer window to the end of the read data
            accumulator.Advance(read);
        }
    }
}