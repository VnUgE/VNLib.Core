/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: CompressionExtensions.cs 
*
* CompressionExtensions.cs is part of VNLib.Net.Compression which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Compression is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Net.Compression is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Net.Compression. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Buffers;
using System.Runtime.InteropServices;

using VNLib.Net.Http;

namespace VNLib.Net.Compression
{
    internal static class CompressionExtensions
    {
        /// <summary>
        /// Compresses a block using the compressor context pointer provided
        /// </summary>
        /// <param name="nativeLib"></param>
        /// <param name="compressorInstance">A pointer to the compressor context</param>
        /// <param name="output">A buffer to write the result to</param>
        /// <param name="input">The input block of memory to compress</param>
        /// <param name="finalBlock">A value that indicates if a flush is requested</param>
        /// <returns>The results of the compression operation</returns>
        public static unsafe CompressionResult CompressBlock(
            this LibraryWrapper nativeLib,
            IntPtr compressorInstance,
            Memory<byte> output,
            ReadOnlyMemory<byte> input,
            bool finalBlock
        )
        {
            /*
             * Since .NET only supports int32 size memory blocks
             * we dont need to worry about integer overflow.
             * 
             * Output sizes can never be larger than input 
             * sizes (read/written)
             */

            //Create the operation struct
            CompressionOperation operation = default;

            operation.flush = finalBlock ? 1 : 0;

            checked
            {
                //get pointers to the input and output buffers
                using MemoryHandle inPtr = input.Pin();
                using MemoryHandle outPtr = output.Pin();

                //Configure the input and output buffers
                operation.inputBuffer = inPtr.Pointer;
                operation.inputSize = (uint)input.Length;

                operation.outputBuffer = outPtr.Pointer;
                operation.outputSize = (uint)output.Length;

                //Call the native compress function
                nativeLib!.CompressBlock(compressorInstance, &operation);
               
                return new()
                {
                    BytesRead = (int)operation.bytesRead,
                    BytesWritten = (int)operation.bytesWritten
                };
            }
        }

        /// <summary>
        /// Compresses a block using the compressor context pointer provided
        /// </summary>
        /// <param name="nativeLib"></param>
        /// <param name="compressorInstance">A pointer to the compressor context</param>
        /// <param name="output">A buffer to write the result to</param>
        /// <param name="input">The input block of memory to compress</param>
        /// <param name="finalBlock">A value that indicates if a flush is requested</param>
        /// <returns>The results of the compression operation</returns>
        public static unsafe CompressionResult CompressBlock(
            this LibraryWrapper nativeLib,
            IntPtr compressorInstance,
            Span<byte> output,
            ReadOnlySpan<byte> input,
            bool finalBlock
        )
        {
            /*
             * Since .NET only supports int32 size memory blocks
             * we dont need to worry about integer overflow.
             * 
             * Output sizes can never be larger than input 
             * sizes (read/written)
             */

            //Create the operation struct
            CompressionOperation operation = default;
            operation.flush = finalBlock ? 1 : 0;

            checked
            {
                fixed (byte* inputPtr = &MemoryMarshal.GetReference(input),
                    outPtr = &MemoryMarshal.GetReference(output))
                {
                    //Configure the input and output buffers
                    operation.inputBuffer = inputPtr;
                    operation.inputSize = (uint)input.Length;

                    operation.outputBuffer = outPtr;
                    operation.outputSize = (uint)output.Length;

                    //Call the native compress function
                    nativeLib!.CompressBlock(compressorInstance, &operation);
                
                    return new()
                    {
                        BytesRead = (int)operation.bytesRead,
                        BytesWritten = (int)operation.bytesWritten
                    };
                }
            }
        }
    }
}
