/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace VNLib.Net.Http.Core.Compression
{
    internal sealed class ManagedHttpCompressor(IHttpCompressorManager manager) : IResponseCompressor
    {
        const sbyte FlagCommited = 0x01;
        const sbyte FlagInitialized = 0x02;

        /*
         * The compressor alloc is deferd until the first call to Init()
         * This is because user-code should not be called during the constructor
         * or internal calls. This is to avoid user-code errors causing the app
         * to crash during critical sections that do not have exception handling.
         */

        private object? _compressor;
        private sbyte _flags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCommited()
        {
            if ((_flags & FlagCommited) == 0)
            {
                // Ensure the compressor instance is allocated
                _compressor ??= manager.AllocCompressor();

                manager.CommitMemory(_compressor);

                _flags |= FlagCommited;
            }
        }

        ///<inheritdoc/>
        public int BlockSize { get; private set; }

        ///<inheritdoc/>
        public CompressionResult CompressBlock(ReadOnlyMemory<byte> input, Memory<byte> output)
        {
            Debug.Assert((_flags & FlagInitialized) > 0);
            Debug.Assert(_compressor != null);
            Debug.Assert(!output.IsEmpty, "Expected non-zero output buffer");

            //Compress the block
            return manager.CompressBlock(_compressor!, input, output);
        }

        ///<inheritdoc/>
        public int Flush(Memory<byte> output)
        {
            Debug.Assert((_flags & FlagInitialized) > 0);
            Debug.Assert(_compressor != null);
            Debug.Assert(!output.IsEmpty, "Expected non-zero output buffer");

            return manager.Flush(_compressor!, output);
        }

        ///<inheritdoc/>
        public void Init(CompressionMethod compMethod)
        {
            //Defer alloc the compressor
            EnsureCommited();         

            //Init the compressor and get the block size
            BlockSize = manager.InitCompressor(_compressor!, compMethod);

            _flags |= FlagInitialized;
        }

        ///<inheritdoc/>
        public void DeInit()
        {
            //Deinit compressor if initialized
            if ((_flags & FlagInitialized) > 0)
            {
                Debug.Assert(_compressor != null, "Compressor was initialized, exepcted a non null instance");

                manager.DeinitCompressor(_compressor);

                _flags &= ~FlagInitialized;
            }
        }

        ///<inheritdoc/>
        public void Free()
        {
            /*
             * while the server will likely make a all to deinit before free,
             * its still acceptable to free without deinit. So checking for any \
             * flags means the compressor was commited and needs to be freed
             */

            if (_flags != 0)
            {             
                Debug.Assert(_compressor != null, "Compressor was commited, expected a non null instance");
                
                manager.DecommitMemory(_compressor);

                // Clear all flags when freed
                _flags = 0;
            }
        }
    }
}