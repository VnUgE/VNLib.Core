/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ManagedHttpCompressor.cs 
*
* ManagedHttpCompressor.cs is part of VNLib.Net.Http which is part of 
* the larger VNLib collection of libraries and utilities.
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
using System.IO;
using System.Threading.Tasks;


namespace VNLib.Net.Http.Core.Compression
{

    internal sealed class ManagedHttpCompressor : IResponseCompressor
    {
        //Store the compressor
        private readonly IHttpCompressorManager _provider;

        public ManagedHttpCompressor(IHttpCompressorManager provider)
        {
            _provider = provider;
        }

        /*
         * The compressor alloc is deferd until the first call to Init()
         * This is because user-code should not be called during the constructor
         * or internal calls. This is to avoid user-code errors causing the app
         * to crash during critical sections that do not have exception handling.
         */

        private object? _compressor;
        private Stream? _stream;

        ///<inheritdoc/>
        public int BlockSize { get; private set; }

        ///<inheritdoc/>
        public ValueTask CompressBlockAsync(ReadOnlyMemory<byte> buffer, bool finalBlock)
        {
            //Compress the block
            ReadOnlyMemory<byte> result = _provider.CompressBlock(_compressor!, buffer, finalBlock);

            //Write the compressed block to the stream
            return _stream!.WriteAsync(result);
        }

        ///<inheritdoc/>
        public void Free()
        {
            //Remove stream ref and de-init the compressor
            _stream = null;
            _provider.DeinitCompressor(_compressor!);
        }

        ///<inheritdoc/>
        public void Init(Stream output, CompressionMethod compMethod)
        {
            //Defer alloc the compressor
            _compressor ??= _provider.AllocCompressor();

            //Store the stream and init the compressor
            _stream = output;
            BlockSize = _provider.InitCompressor(_compressor, compMethod);
        }
    }
}