/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IDataAccumulator.cs 
*
* IDataAccumulator.cs is part of VNLib.Utils which is part of the larger 
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
    /// A data structure that represents a sliding window over a buffer
    /// for resetable forward-only reading or writing
    /// </summary>
    /// <typeparam name="T">The accumuation data type</typeparam>
    public interface IDataAccumulator<T>
    {
        /// <summary>
        /// Gets the number of available items within the buffer
        /// </summary>
        int AccumulatedSize { get; }
        /// <summary>
        /// The number of elements remaining in the buffer
        /// </summary>
        int RemainingSize { get; }
        /// <summary>
        /// The remaining space in the internal buffer as a contiguous segment
        /// </summary>
        Span<T> Remaining { get; }
        /// <summary>
        /// The buffer window over the accumulated data
        /// </summary>
        Span<T> Accumulated { get; }

        /// <summary>
        /// Advances the accumulator buffer window by the specified amount
        /// </summary>
        /// <param name="count">The number of elements accumulated</param>
        void Advance(int count);

        /// <summary>
        /// Resets the internal state of the accumulator
        /// </summary>
        void Reset();
    }
}