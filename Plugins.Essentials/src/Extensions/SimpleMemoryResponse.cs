/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: SimpleMemoryResponse.cs 
*
* SimpleMemoryResponse.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Text;
using VNLib.Net.Http;
using System.Buffers;

#nullable enable

namespace VNLib.Plugins.Essentials.Extensions
{
    internal sealed class SimpleMemoryResponse : IMemoryResponseReader
    {
        private byte[]? _buffer;
        private int _written;

        /// <summary>
        /// Copies the data in the specified buffer to the internal buffer 
        /// to initalize the new <see cref="SimpleMemoryResponse"/>
        /// </summary>
        /// <param name="data">The data to copy</param>
        public SimpleMemoryResponse(ReadOnlySpan<byte> data)
        {
            Remaining = data.Length;
            //Alloc buffer
            _buffer = ArrayPool<byte>.Shared.Rent(Remaining);
            //Copy data to buffer
            data.CopyTo(_buffer);
        }

        /// <summary>
        /// Encodes the character buffer data using the encoder and stores
        /// the result in the internal buffer for reading.
        /// </summary>
        /// <param name="data">The data to encode</param>
        /// <param name="enc">The encoder to use</param>
        public SimpleMemoryResponse(ReadOnlySpan<char> data, Encoding enc)
        {
            //Calc byte count
            Remaining = enc.GetByteCount(data);
            
            //Alloc buffer
            _buffer = ArrayPool<byte>.Shared.Rent(Remaining);
            
            //Encode data
            Remaining = enc.GetBytes(data, _buffer);
        }

        ///<inheritdoc/>
        public int Remaining { get; private set; }
        ///<inheritdoc/>
        void IMemoryResponseReader.Advance(int written)
        {
            Remaining -= written;
            _written += written;
        }
        ///<inheritdoc/>
        void IMemoryResponseReader.Close()
        {
            //Return buffer to pool
            ArrayPool<byte>.Shared.Return(_buffer!);
            _buffer = null;
        }
        ///<inheritdoc/>
        ReadOnlyMemory<byte> IMemoryResponseReader.GetMemory() => _buffer!.AsMemory(_written, Remaining);
    }
}