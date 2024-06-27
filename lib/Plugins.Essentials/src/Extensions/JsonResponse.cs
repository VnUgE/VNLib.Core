/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Diagnostics;

using VNLib.Net.Http;
using VNLib.Utils.IO;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Plugins.Essentials.Extensions
{
    internal sealed class JsonResponse(IObjectRental<JsonResponse> pool) : IJsonSerializerBuffer, IMemoryResponseReader, IDisposable
    {
        const int InitBufferSize = 4096;
        const int MaxSizeThreshold = 24 * 1024; //24KB

        private readonly IObjectRental<JsonResponse> _pool = pool;

        //Stream "owns" the handle, so we cannot dispose the stream
        private readonly VnMemoryStream _stream = new(InitBufferSize, false);

        private int _read;
        private ReadOnlyMemory<byte> _dataSegToSend;

        //Cleanup any dangling resources dangling somehow
        ~JsonResponse() => Dispose();

        ///<inheritdoc/>
        public void Dispose()
        {
            _stream.Dispose();
            GC.SuppressFinalize(this);
        }

        ///<inheritdoc/>
        public Stream GetSerialzingStream()
        {
            //Reset stream position
            _stream.Seek(0, SeekOrigin.Begin);
            return _stream;
        }

        ///<inheritdoc/>
        public void SerializationComplete()
        {
            //Reset data read position
            _read = 0;

            //Update remaining pointer
            Remaining = Convert.ToInt32(_stream.Position);

            /*
             * Store the written segment for streaming now that the 
             * serialization is complete. This is the entire window of 
             * the stream, from 0 - length
             */
            _dataSegToSend = _stream.AsMemory();
        }


        ///<inheritdoc/>
        public int Remaining { get; private set; }

        ///<inheritdoc/>
        void IMemoryResponseReader.Advance(int written)
        {
            //Update position
            _read += written;
            Remaining -= written;

            Debug.Assert(Remaining > 0);
        }

        ///<inheritdoc/>
        void IMemoryResponseReader.Close()
        {
            //Reset and return to pool
            _read = 0;
            Remaining = 0;

            //if the stream size was pretty large, shrink it before returning to the pool
            if (_stream.Length > MaxSizeThreshold)
            {
                _stream.SetLength(InitBufferSize);
            }

            //Return self back to pool
            _pool.Return(this);
        }

        ///<inheritdoc/>
        ReadOnlyMemory<byte> IMemoryResponseReader.GetMemory() => _dataSegToSend.Slice(_read, Remaining);
    }
}