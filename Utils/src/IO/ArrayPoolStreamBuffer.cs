/*
* Copyright (c) 2022 Vaughn Nugent
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
        private readonly ArrayPool<T> _pool;
        private T[] _buffer;

        public ArrayPoolStreamBuffer(ArrayPool<T> pool, int bufferSize)
        {
            _pool = pool;
            _buffer = _pool.Rent(bufferSize);
        }

        public int WindowStartPos { get; set; }
        public int WindowEndPos { get; set; }
        
        public Memory<T> Buffer => _buffer.AsMemory();

        public void Advance(int count)
        {
            WindowEndPos += count;
        }

        public void AdvanceStart(int count)
        {
            WindowStartPos += count;
        }

        public void Close()
        {
            //Return buffer to pool
            _pool.Return(_buffer);
            _buffer = null;
        }

        public void Reset()
        {
            //Reset window positions
            WindowStartPos = 0;
            WindowEndPos = 0;
        }
    }
}