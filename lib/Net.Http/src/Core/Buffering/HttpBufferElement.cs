/*
* Copyright (c) 2025 Vaughn Nugent
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
            _handle.Handle.Dispose();
            _handle = default;
        }

        public void SetBuffer(Memory<byte> buffer)
        {
            Debug.Assert(_handle.Size == 0, "Buffer was not feed correctly");

            _handle = default;

            _handle.Memory = buffer;
            _handle.Size = buffer.Length;
            _handle.Handle = buffer.Pin();
            _handle.Pointer = MemoryUtil.GetIntptr(in _handle.Handle);
        }


        ///<inheritdoc/>
        public int Size => _handle.Size;

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Span<byte> GetBinSpan(int offset) => GetBinSpan(offset, Size - offset);

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ref byte DangerousGetBinRef(int offset)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, _handle.Size);

            return ref MemoryUtil.GetRef<byte>(_handle.Pointer, (nuint)offset);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Memory<byte> GetMemory() => _handle.Memory;

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Span<byte> GetBinSpan(int offset, int size)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(_handle.Size - offset, size);
           
            return MemoryUtil.GetSpan<byte>(IntPtr.Add(_handle.Pointer, offset), size);
        }

        private struct HandleState
        {
            public MemoryHandle Handle;
            public IntPtr Pointer;
            public int Size;
            public Memory<byte> Memory;
        }
    }
}
