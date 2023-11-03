/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpBufferElement.cs 
*
* HttpBufferElement.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;

namespace VNLib.Net.Http.Core.Buffering
{       
    /*
     * Abstract class for controlled access to the raw buffer block
     * as we are pinning the block. The block is pinned once for the lifetime 
     * of the connection, so we have access to the raw memory for faster 
     * span access.
     * 
     * It is suggested to use an Unmanaged memory pool for zero-cost memory
     * pinning
    */
    internal abstract class HttpBufferElement : IHttpBuffer
    {
        private MemoryHandle Pinned;
        private int _size;
        protected Memory<byte> Buffer;

        public virtual void FreeBuffer()
        {
            //Unpin and set defaults
            Pinned.Dispose();
            Pinned = default;
            Buffer = default;
            _size = 0;
        }

        public virtual void SetBuffer(Memory<byte> buffer)
        {
            //Set mem buffer
            Buffer = buffer;
            //Pin buffer and hold handle
            Pinned = buffer.Pin();
            //Set size to length of buffer
            _size = buffer.Length;
        }

        ///<inheritdoc/>
        public int Size => _size;

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Span<byte> GetBinSpan() => MemoryUtil.GetSpan<byte>(ref Pinned, _size);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Memory<byte> GetMemory() => Buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual Span<byte> GetBinSpan(int maxSize)
        {
            return maxSize > _size ? throw new ArgumentOutOfRangeException(nameof(maxSize)) : MemoryUtil.GetSpan<byte>(ref Pinned, maxSize);
        }
    }
}
