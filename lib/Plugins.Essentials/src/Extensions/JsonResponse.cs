/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: JsonResponse.cs 
*
* JsonResponse.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.IO;
using System.Buffers;

using VNLib.Net.Http;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Plugins.Essentials.Extensions
{
    internal sealed class JsonResponse : IJsonSerializerBuffer, IMemoryResponseReader
    {
        private readonly IObjectRental<JsonResponse> _pool;

        private readonly MemoryHandle<byte> _handle;
        private readonly IMemoryOwner<byte> _memoryOwner;
        //Stream "owns" the handle, so we cannot dispose the stream
        private readonly VnMemoryStream _asStream;
        
        private int _written;

        internal JsonResponse(IObjectRental<JsonResponse> pool)
        {
            /*
             * I am breaking the memoryhandle rules by referrencing the same
             * memory handle in two different wrappers.
             */

            _pool = pool;
            
            //Alloc buffer
            _handle = MemoryUtil.Shared.Alloc<byte>(4096, false);
            
            //Create stream around handle and not own it
            _asStream = VnMemoryStream.FromHandle(_handle, false, 0, false);
            
            //Get memory owner from handle
            _memoryOwner = _handle.ToMemoryManager(false);
        }

        ~JsonResponse()
        {
            _handle.Dispose();
        }

        ///<inheritdoc/>
        public Stream GetSerialzingStream()
        {
            //Reset stream position
            _asStream.Seek(0, SeekOrigin.Begin);
            return _asStream;
        }

        ///<inheritdoc/>
        public void SerializationComplete()
        {
            //Reset written position
            _written = 0;
            //Update remaining pointer
            Remaining = Convert.ToInt32(_asStream.Position);
        }


        ///<inheritdoc/>
        public int Remaining { get; private set; }

        ///<inheritdoc/>
        void IMemoryResponseReader.Advance(int written)
        {
            //Update position
            _written += written;
            Remaining -= written;
        }

        ///<inheritdoc/>
        void IMemoryResponseReader.Close()
        {
            //Reset and return to pool
            _written = 0;
            Remaining = 0;
            //Return self back to pool
            _pool.Return(this);
        }

        ///<inheritdoc/>
        ReadOnlyMemory<byte> IMemoryResponseReader.GetMemory() => _memoryOwner.Memory.Slice(_written, Remaining);
    }
}