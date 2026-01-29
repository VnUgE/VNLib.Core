/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ISlindingWindowBuffer.cs 
*
* ISlindingWindowBuffer.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Represents a sliding window buffer for reading/wiriting data
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ISlindingWindowBuffer<T> : IDataAccumulator<T>
    {
        /// <summary>
        /// The number of elements remaining in the buffer
        /// </summary>
        int IDataAccumulator<T>.RemainingSize => Buffer.Length - WindowEndPos;
        /// <summary>
        /// The remaining space in the internal buffer as a contiguous segment
        /// </summary>
        Span<T> IDataAccumulator<T>.Remaining => RemainingBuffer.Span;
        /// <summary>
        /// The buffer window over the accumulated data
        /// </summary>
        Span<T> IDataAccumulator<T>.Accumulated => AccumulatedBuffer.Span;
        /// <summary>
        /// Gets the number of available items within the buffer
        /// </summary>
        int IDataAccumulator<T>.AccumulatedSize => WindowEndPos - WindowStartPos;

        /// <summary>
        /// The starting positon of the available data within the buffer
        /// </summary>
        int WindowStartPos { get; }
        /// <summary>
        /// The ending position of the available data within the buffer
        /// </summary>
        int WindowEndPos { get; }
        /// <summary>
        /// Buffer memory wrapper
        /// </summary>
        Memory<T> Buffer { get; }
        
        /// <summary>
        /// Releases resources used by the current instance
        /// </summary>
        void Close();
        /// <summary>
        /// <para>
        /// Advances the beginning of the accumulated data window.
        /// </para>
        /// <para>
        /// This method is used during reading to singal that data 
        /// has been read from the internal buffer and the 
        /// accumulator window can be shifted.
        /// </para>
        /// </summary>
        /// <param name="count">The number of elements to shift by</param>
        void AdvanceStart(int count);

        /// <summary>
        /// Gets a window within the buffer of available buffered data
        /// </summary>
        Memory<T> AccumulatedBuffer => Buffer[WindowStartPos..WindowEndPos];
        /// <summary>
        /// Gets the available buffer window to write data to
        /// </summary>
        Memory<T> RemainingBuffer => Buffer[WindowEndPos..];
    }
}