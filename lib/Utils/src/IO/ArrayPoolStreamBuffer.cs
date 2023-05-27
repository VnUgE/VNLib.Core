/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ArrayPoolStreamBuffer.cs 
*
* ArrayPoolStreamBuffer.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.IO
{

    internal class ArrayPoolStreamBuffer<T> : ISlindingWindowBuffer<T>
    {
        /// <summary>
        /// The shared <see cref="IStreamBufferFactory{T}"/> instance to allocate buffers 
        /// from
        /// </summary>
        public static IStreamBufferFactory<T> Shared { get; } = new DefaultFactory();

        private readonly ArrayPool<T> _pool;
        private T[] _buffer;

        /// <summary>
        /// Creates a new <see cref="ArrayPoolStreamBuffer{T}"/> from the 
        /// given array instance and <see cref="ArrayPool{T}"/> it came from.
        /// </summary>
        /// <param name="array">The rented array to use</param>
        /// <param name="pool">The pool to return the array to when completed</param>
        public ArrayPoolStreamBuffer(T[] array, ArrayPool<T> pool)
        {
            _pool = pool;
            _buffer = array;
        }

        ///<inheritdoc/>
        public int WindowStartPos { get; set; }

        ///<inheritdoc/>
        public int WindowEndPos { get; set; }

        ///<inheritdoc/>
        public Memory<T> Buffer => _buffer.AsMemory();

        ///<inheritdoc/>
        public void Advance(int count) => WindowEndPos += count;

        ///<inheritdoc/>
        public void AdvanceStart(int count) => WindowStartPos += count;

        ///<inheritdoc/>
        public void Close()
        {
            //Return buffer to pool
            _pool.Return(_buffer);
            _buffer = null;
        }

        ///<inheritdoc/>
        public void Reset()
        {
            //Reset window positions
            WindowStartPos = 0;
            WindowEndPos = 0;
        }

        private sealed class DefaultFactory : IStreamBufferFactory<T>
        {
            ///<inheritdoc/>
            public ISlindingWindowBuffer<T> CreateBuffer(int bufferSize)
            {
                //rent buffer
                T[] array = ArrayPool<T>.Shared.Rent(bufferSize);

                //return wrapper
                return new ArrayPoolStreamBuffer<T>(array, ArrayPool<T>.Shared);
            }
        }
    }
}