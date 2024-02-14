/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Diagnostics;
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
        private HandleState _handle;

        public void FreeBuffer()
        {
            _handle.Unpin();
            _handle = default;
        }

        public void SetBuffer(Memory<byte> buffer) => _handle = new(buffer);

        ///<inheritdoc/>
        public int Size => _handle.Size;

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Span<byte> GetBinSpan(int offset) => GetBinSpan(offset, Size - offset);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ref byte DangerousGetBinRef(int offset)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, _handle.Size);

            //Add offset to ref
            return ref Unsafe.Add(ref _handle.GetRef(), offset);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Memory<byte> GetMemory() => _handle.Memory;

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Span<byte> GetBinSpan(int offset, int size) 
            => (offset + size) < _handle.Size ? _handle.GetSpan(offset, size) : throw new ArgumentOutOfRangeException(nameof(offset));


        private struct HandleState
        {
            private MemoryHandle _handle;
            private readonly IntPtr _pointer;
            
            public readonly int Size;
            public readonly Memory<byte> Memory;

            public HandleState(Memory<byte> mem)
            {
                Memory = mem;
                Size = mem.Length;
                _handle = mem.Pin();
                _pointer = MemoryUtil.GetIntptr(ref _handle);
            }

            public readonly void Unpin() => _handle.Dispose();

            public readonly Span<byte> GetSpan(int offset, int size)
            {
                Debug.Assert((offset + size) < Size, "Call to GetSpan failed because the offset/size was out of valid range");
                return MemoryUtil.GetSpan<byte>(IntPtr.Add(_pointer, offset), size);
            }

            public readonly ref byte GetRef() => ref MemoryUtil.GetRef<byte>(_pointer);
        }
    }
}
