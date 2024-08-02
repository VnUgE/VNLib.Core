/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: FallbackCompressionManager.cs 
*
* FallbackCompressionManager.cs is part of VNLib.WebServer which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;

using VNLib.Net.Http;

namespace VNLib.WebServer.Compression
{

    /*
     * The fallback compression manager is used when the user did not configure a 
     * compression manager library. Since .NET only exposes a brotli encoder, that 
     * is not a stream api, (gzip and deflate are stream api's) Im only supporting
     * brotli for now. This is better than nothing lol 
     */


    internal sealed class FallbackCompressionManager : IHttpCompressorManager
    {
        /// <inheritdoc/>
        public object AllocCompressor() => new BrCompressorState();

        /// <inheritdoc/>
        public CompressionMethod GetSupportedMethods() => CompressionMethod.Brotli;

        /// <inheritdoc/>
        public int InitCompressor(object compressorState, CompressionMethod compMethod)
        {
            BrCompressorState compressor = (BrCompressorState)compressorState;
            ref BrotliEncoder encoder = ref compressor.GetEncoder();

            //Init new brotli encoder struct
            encoder = new(9, 24);
            return 0;
        }

        /// <inheritdoc/>
        public void DeinitCompressor(object compressorState)
        {
            BrCompressorState compressor = (BrCompressorState)compressorState;
            ref BrotliEncoder encoder = ref compressor.GetEncoder();

            //Clean up the encoder
            encoder.Dispose();
            encoder = default;
        }

        /// <inheritdoc/>
        public CompressionResult CompressBlock(object compressorState, ReadOnlyMemory<byte> input, Memory<byte> output)
        {           
            //Output buffer should never be empty, server guards this
            Debug.Assert(!output.IsEmpty, "Exepcted a non-zero length output buffer");

            BrCompressorState compressor = (BrCompressorState)compressorState;
            ref BrotliEncoder encoder = ref compressor.GetEncoder();

            //Compress the supplied block
            OperationStatus status = encoder.Compress(input.Span, output.Span, out int bytesConsumed, out int bytesWritten, false);
            
            /*
             * Should always return done, because the output buffer is always 
             * large enough and that data/state cannot be invalid
             */
            Debug.Assert(status == OperationStatus.Done);

            return new()
            {
                BytesRead = bytesConsumed,
                BytesWritten = bytesWritten,
            };
        }

        /// <inheritdoc/>
        public int Flush(object compressorState, Memory<byte> output)
        {
            OperationStatus status;

            //Output buffer should never be empty, server guards this
            Debug.Assert(!output.IsEmpty, "Exepcted a non-zero length output buffer");

            BrCompressorState compressor = (BrCompressorState)compressorState;
            ref BrotliEncoder encoder = ref compressor.GetEncoder();

            /*
             * A call to compress with the isFinalBlock flag set to true will
             * cause a BROTLI_OPERATION_FINISH operation to be performed. This is 
             * actually the proper way to complete a brotli compression stream.
             * 
             * See vnlib_compress project for more details.
             */
            status = encoder.Compress(
                source: default, 
                destination: output.Span,
                bytesConsumed: out _,
                bytesWritten: out int bytesWritten,
                isFinalBlock: true
            );

            /*
             * Function can return Done or DestinationTooSmall if there is still more data
             * stored in the compressor to be written. If InvaliData is returned, then there 
             * is a problem with the encoder state or the output buffer, this condition should
             * never happen.
             */
            Debug.Assert(status != OperationStatus.InvalidData, $"Failed with status {status}, written {bytesWritten}, buffer size {output.Length}");

            //Return the number of bytes actually accumulated
            return bytesWritten;
        }
       

        private sealed class BrCompressorState
        {
            private BrotliEncoder _encoder;

            public ref BrotliEncoder GetEncoder() => ref _encoder;
        }
    }
}
