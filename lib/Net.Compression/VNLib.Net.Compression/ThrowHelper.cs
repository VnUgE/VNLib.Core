/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: ThrowHelper.cs 
*
* ThrowHelper.cs is part of VNLib.Net.Compression which is part of 
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

using VNLib.Utils;

namespace VNLib.Net.Compression
{
    internal static class ThrowHelper
    {
        /*
         * Error codes correspond to constants 
         * in the native compression library
         */
        enum NativeErrorType
        {
            ErrInvalidPtr = -1,
            ErrOutOfMemory = -2,
            
            ErrCompTypeNotSupported = -9,
            ErrCompLevelNotSupported = -10,
            ErrInvalidInput = -11,
            ErrInvalidOutput = -12,

            ErrGzInvalidState = -16,
            ErrGzOverflow = -17,

            ErrBrInvalidState = -24
        }

        /// <summary>
        /// Determines if the specified result is an error and throws an exception if it is
        /// </summary>
        /// <param name="result"></param>
        /// <exception cref="NativeCompressionException"></exception>
        public static void ThrowIfError(ERRNO result)
        {
            //Check for no error
            if(result > 0)
            {
                return;
            }

            switch ((NativeErrorType)(int)result)
            {
                case NativeErrorType.ErrInvalidPtr:
                    throw new NativeCompressionException("A pointer to a compressor instance was null");
                case NativeErrorType.ErrOutOfMemory:
                    throw new NativeCompressionException("An operation falied because the system is out of memory");
                case NativeErrorType.ErrCompTypeNotSupported:
                    throw new NotSupportedException("The desired compression method is not supported by the native library");
                case NativeErrorType.ErrCompLevelNotSupported:
                    throw new NotSupportedException("The desired compression level is not supported by the native library");
                case NativeErrorType.ErrInvalidInput:
                    throw new NativeCompressionException("The input buffer was null and the input size was greater than 0");
                case NativeErrorType.ErrInvalidOutput:
                    throw new NativeCompressionException("The output buffer was null and the output size was greater than 0");
                case NativeErrorType.ErrGzInvalidState:
                    throw new NativeCompressionException("A gzip operation failed because the compressor state is invalid (null compressor pointer)");
                case NativeErrorType.ErrGzOverflow:
                    throw new NativeCompressionException("A gzip operation failed because the output buffer is too small");
                case NativeErrorType.ErrBrInvalidState:
                    throw new NativeCompressionException("A brotli operation failed because the compressor state is invalid (null compressor pointer)");
                default:
                    break;
            }
        }
    }
}