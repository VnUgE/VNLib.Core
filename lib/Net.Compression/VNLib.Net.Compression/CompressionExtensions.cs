/*
* Copyright (c) 2023 Vaughn Nugent
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
        /// <param name="comp">A pointer to the compressor context</param>
        /// <param name="output">A buffer to write the result to</param>
        /// <param name="input">The input block of memory to compress</param>
        /// <param name="finalBlock">A value that indicates if a flush is requested</param>
        /// <returns>The results of the compression operation</returns>
        public static unsafe CompressionResult CompressBlock(this LibraryWrapper nativeLib, IntPtr comp, Memory<byte> output, ReadOnlyMemory<byte> input, bool finalBlock)
        {
            /*
             * Since .NET only supports int32 size memory blocks
             * we dont need to worry about integer overflow.
             * 
             * Output sizes can never be larger than input 
             * sizes (read/written)
             */

            //get pointers to the input and output buffers
            using MemoryHandle inPtr = input.Pin();
            using MemoryHandle outPtr = output.Pin();

            //Create the operation struct
            CompressionOperation operation;
            CompressionOperation* op = &operation;

            op->flush = finalBlock ? 1 : 0;
            op->bytesRead = 0;
            op->bytesWritten = 0;

            //Configure the input and output buffers
            op->inputBuffer = inPtr.Pointer;
            op->inputSize = (uint)input.Length;

            op->outputBuffer = outPtr.Pointer;
            op->outputSize = (uint)output.Length;

            //Call the native compress function
            nativeLib!.CompressBlock(comp, &operation);

            //Return the number of bytes written
            return new()
            {
                BytesRead = (int)op->bytesRead,
                BytesWritten = (int)op->bytesWritten
            };
        }

        /// <summary>
        /// Compresses a block using the compressor context pointer provided
        /// </summary>
        /// <param name="nativeLib"></param>
        /// <param name="comp">A pointer to the compressor context</param>
        /// <param name="output">A buffer to write the result to</param>
        /// <param name="input">The input block of memory to compress</param>
        /// <param name="finalBlock">A value that indicates if a flush is requested</param>
        /// <returns>The results of the compression operation</returns>
        public static unsafe CompressionResult CompressBlock(this LibraryWrapper nativeLib, IntPtr comp, Span<byte> output, ReadOnlySpan<byte> input, bool finalBlock)
        {
            /*
             * Since .NET only supports int32 size memory blocks
             * we dont need to worry about integer overflow.
             * 
             * Output sizes can never be larger than input 
             * sizes (read/written)
             */

            fixed(byte* inputPtr = &MemoryMarshal.GetReference(input),
                outPtr = &MemoryMarshal.GetReference(output))
            {
                //Create the operation struct
                CompressionOperation operation;
                CompressionOperation* op = &operation;

                op->flush = finalBlock ? 1 : 0;
                op->bytesRead = 0;
                op->bytesWritten = 0;

                //Configure the input and output buffers
                op->inputBuffer = inputPtr;
                op->inputSize = (uint)input.Length;

                op->outputBuffer = outPtr;
                op->outputSize = (uint)output.Length;

                //Call the native compress function
                nativeLib!.CompressBlock(comp, &operation);

                //Return the number of bytes written
                return new()
                {
                    BytesRead = (int)op->bytesRead,
                    BytesWritten = (int)op->bytesWritten
                };
            }
        }
    }
}
