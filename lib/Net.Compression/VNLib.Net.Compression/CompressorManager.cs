/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: CompressorManager.cs 
*
* CompressorManager.cs is part of VNLib.Net.Compression which is part of 
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

/*
 * Notes:
 * 
 * This library implements the IHttpCompressorManager for dynamic library 
 * loading by the VNLib.Webserver (or any other application that implements 
 * the IHttpCompressorManager interface).
 * 
 * It implements an unspecified method called OnLoad that is exepcted to be 
 * called by the VNLib.Webserver during load time. This method is used to 
 * initialize the compressor and load the native library.
 */

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;

namespace VNLib.Net.Compression
{
    public sealed class CompressorManager : IHttpCompressorManager
    {
        const string NATIVE_LIB_NAME = "vnlib_compress.dll";
        const int MIN_BUF_SIZE_DEFAULT = 8192;

        private LibraryWrapper? _nativeLib;
        private CompressionLevel _compLevel;
        private int minOutBufferSize;

        /// <summary>
        /// Called by the VNLib.Webserver during startup to initiialize the compressor.
        /// </summary>
        /// <param name="log">The application log provider</param>
        /// <param name="configJsonString">The raw json configuration data</param>
        public void OnLoad(ILogProvider? log, JsonElement? config)
        {
            _compLevel = CompressionLevel.Optimal;
            minOutBufferSize = MIN_BUF_SIZE_DEFAULT;
            string libPath = NATIVE_LIB_NAME;

            if(config.HasValue)
            {
                //Get the compression element
                if(config.Value.TryGetProperty("vnlib.net.compression", out JsonElement compEl))
                {
                    //Try to get the user specified compression level
                    if(compEl.TryGetProperty("level", out JsonElement lEl))
                    {
                        _compLevel = (CompressionLevel)lEl.GetUInt16();
                    }

                    //Allow the user to specify the path to the native library
                    if(compEl.TryGetProperty("lib_path", out JsonElement libEl))
                    {
                        libPath = libEl.GetString() ?? NATIVE_LIB_NAME;
                    }

                    if(compEl.TryGetProperty("min_out_buf_size", out JsonElement minBufEl))
                    {
                        minOutBufferSize = minBufEl.GetInt32();
                    }
                }
            }

            log?.Debug("Attempting to load native compression library from: {lib}", libPath);

            //Load the native library
            _nativeLib = LibraryWrapper.LoadLibrary(libPath);

            log?.Debug("Loaded native compression library with compression level {l}", _compLevel.ToString());
        }

        ///<inheritdoc/>
        public CompressionMethod GetSupportedMethods()
        {
            if(_nativeLib == null)
            {
                throw new InvalidOperationException("The native library has not been loaded yet.");
            }

            return _nativeLib.GetSupportedMethods();
        }

        ///<inheritdoc/>
        public object AllocCompressor()
        {
            return new Compressor();
        }

        ///<inheritdoc/>
        public int InitCompressor(object compressorState, CompressionMethod compMethod)
        {
            //For now do not allow empty compression methods, later we should allow this to be used as a passthrough
            if(compMethod == CompressionMethod.None)
            {
                throw new ArgumentException("Compression method cannot be None", nameof(compMethod));
            }

            Compressor compressor = Unsafe.As<Compressor>(compressorState) ?? throw new ArgumentNullException(nameof(compressorState));

            //Instance should be null during initialization calls
            Debug.Assert(compressor.Instance == IntPtr.Zero);

            //Alloc the compressor
            compressor.Instance = _nativeLib!.AllocateCompressor(compMethod, _compLevel);

            //Return the compressor block size
            return _nativeLib!.GetBlockSize(compressor.Instance);
        }

        ///<inheritdoc/>
        public void DeinitCompressor(object compressorState)
        {
            Compressor compressor = Unsafe.As<Compressor>(compressorState) ?? throw new ArgumentNullException(nameof(compressorState));

            if(compressor.Instance == IntPtr.Zero)
            {
                throw new InvalidOperationException("This compressor instance has not been initialized, cannot free compressor");
            }

            //Free the output buffer
            if(compressor.OutputBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(compressor.OutputBuffer, true);
                compressor.OutputBuffer = null;
            }

            //Free compressor instance
            _nativeLib!.FreeCompressor(compressor.Instance);

            //Clear pointer after successful free
            compressor.Instance = IntPtr.Zero;
        }

        ///<inheritdoc/>
        public ReadOnlyMemory<byte> Flush(object compressorState)
        {
            Compressor compressor = Unsafe.As<Compressor>(compressorState) ?? throw new ArgumentNullException(nameof(compressorState));

            if (compressor.Instance == IntPtr.Zero)
            {
                throw new InvalidOperationException("This compressor instance has not been initialized, cannot free compressor");
            }

            //rent a new buffer of the minimum size if not already allocated
            compressor.OutputBuffer ??= ArrayPool<byte>.Shared.Rent(minOutBufferSize);

            //Force a flush until no more data is available
            int bytesWritten = CompressBlock(compressor.Instance, compressor.OutputBuffer, default, true);

            return compressor.OutputBuffer.AsMemory(0, bytesWritten);
        }

        ///<inheritdoc/>
        public ReadOnlyMemory<byte> CompressBlock(object compressorState, ReadOnlyMemory<byte> input, bool finalBlock)
        {
            Compressor compressor = Unsafe.As<Compressor>(compressorState) ?? throw new ArgumentNullException(nameof(compressorState));

            if (compressor.Instance == IntPtr.Zero)
            {
                throw new InvalidOperationException("This compressor instance has not been initialized, cannot free compressor");
            }

            /*
             * We only alloc the buffer on the first call because we can assume this is the 
             * largest input data the compressor will see, and the block size should be used
             * as a reference for callers. If its too small it will just have to be flushed
             */

            //See if the compressor has a buffer allocated
            if (compressor.OutputBuffer == null)
            {
                //Determine the required buffer size
                int bufferSize = _nativeLib!.GetOutputSize(compressor.Instance, input.Length, finalBlock ? 1 : 0);

                //clamp the buffer size to the minimum output buffer size
                bufferSize = Math.Max(bufferSize, minOutBufferSize);

                //rent a new buffer
                compressor.OutputBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            }

            //Compress the block
            int bytesWritten = CompressBlock(compressor.Instance, compressor.OutputBuffer, input, finalBlock);

            return compressor.OutputBuffer.AsMemory(0, bytesWritten);
        } 
        
        private unsafe int CompressBlock(IntPtr comp, byte[] output, ReadOnlyMemory<byte> input, bool finalBlock)
        {
            //get pointers to the input and output buffers
            using MemoryHandle inPtr = input.Pin();
            using MemoryHandle outPtr = MemoryUtil.PinArrayAndGetHandle(output, 0);

            //Create the operation struct
            CompressionOperation operation;
            CompressionOperation* op = &operation;

            op->flush = finalBlock ? 1 : 0;
            op->bytesRead = 0;
            op->bytesWritten = 0;

            //Configure the input and output buffers
            op->inputBuffer = inPtr.Pointer;
            op->inputSize = input.Length;

            op->outputBuffer = outPtr.Pointer;
            op->outputSize = output.Length;

            //Call the native compress function
            _nativeLib!.CompressBlock(comp, &operation);

            //Return the number of bytes written
            return op->bytesWritten;
        }
       

        /*
         * A class to contain the compressor state
         */
        private sealed class Compressor
        {
            public IntPtr Instance;

            public byte[]? OutputBuffer;
        }
       
    }
}