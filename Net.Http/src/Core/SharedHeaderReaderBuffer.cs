/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: SharedHeaderReaderBuffer.cs 
*
* SharedHeaderReaderBuffer.cs is part of VNLib.Net.Http which is part of the larger 
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
using System.Runtime.InteropServices;

using VNLib.Utils.Memory;



namespace VNLib.Net.Http.Core
{
    sealed class SharedHeaderReaderBuffer : IHttpLifeCycle
    {
        private UnsafeMemoryHandle<byte>? Handle;

        /// <summary>
        /// The size of the binary buffer
        /// </summary>
        public int BinLength { get; }

        private readonly int _bufferSize;

        internal SharedHeaderReaderBuffer(int length)
        {
            _bufferSize = length + (length * sizeof(char));

            //Bin buffer is the specified size
            BinLength = length;
        }

        /// <summary>
        /// The binary buffer to store reader information
        /// </summary>
        public Span<byte> BinBuffer => Handle!.Value.Span[..BinLength];

        /// <summary>
        /// The char buffer to store read characters in
        /// </summary>
        public Span<char> CharBuffer => MemoryMarshal.Cast<byte, char>(Handle!.Value.Span[BinLength..]);

        public void OnPrepare()
        {
            //Alloc the shared buffer
            Handle = CoreBufferHelpers.GetBinBuffer(_bufferSize, true);
        }

        public void OnRelease()
        {
            //Free buffer
            Handle?.Dispose();
            Handle = null;
        }

        public void OnNewRequest()
        {}

        public void OnComplete()
        {
            //Zero buffer
            Handle!.Value.Span.Clear();
        }
    }
}