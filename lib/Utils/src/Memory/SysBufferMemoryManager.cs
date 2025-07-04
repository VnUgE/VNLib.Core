﻿/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: SysBufferMemoryManager.cs 
*
* SysBufferMemoryManager.cs is part of VNLib.Utils which is part of the larger 
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
using System.Buffers;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides an unmanaged System.Buffers integration with zero-cost pinning. Uses <see cref="MemoryHandle{T}"/>
    /// as a memory provider which implements a <see cref="System.Runtime.InteropServices.SafeHandle"/>
    /// </summary>
    /// <typeparam name="T">Unmanaged memory type</typeparam>
    public sealed class SysBufferMemoryManager<T> : MemoryManager<T>
    {
        private readonly IMemoryHandle<T> BackingMemory;
        private readonly bool _ownsHandle;

        /// <summary>
        /// Consumes an exisitng <see cref="MemoryHandle{T}"/> to provide <see cref="MemoryUtil"/> wrappers.
        /// The handle should no longer be referrenced directly
        /// </summary>
        /// <param name="existingHandle">The existing handle to consume</param>
        /// <param name="ownsHandle">A value that indicates if the memory manager owns the handle reference</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="OverflowException"></exception>
        public SysBufferMemoryManager(IMemoryHandle<T> existingHandle, bool ownsHandle)
        {
            BackingMemory = existingHandle ?? throw new ArgumentNullException(nameof(existingHandle));

            //check for overflow
            if (existingHandle.Length > Int32.MaxValue)
            {
                throw new OverflowException("This memory manager does not accept handles larger than Int32.MaxValue");
            }

            _ownsHandle = ownsHandle;
        }

        ///<inheritdoc/>
        ///<exception cref="OverflowException"></exception>
        ///<exception cref="ObjectDisposedException"></exception>
        public override Span<T> GetSpan() => BackingMemory.Span;

        /// <summary>
        /// <inheritdoc/>
        /// </summary> 
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public unsafe override MemoryHandle Pin(int elementIndex = 0)
            => BackingMemory.Pin(elementIndex);

        ///<inheritdoc/>
        public override void Unpin() { }

        ///<inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (_ownsHandle)
            {
                BackingMemory.Dispose();
            }
        }
    }
}
