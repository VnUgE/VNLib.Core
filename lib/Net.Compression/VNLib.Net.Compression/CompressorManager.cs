﻿/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Text.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Utils.Logging;

namespace VNLib.Net.Compression
{

    /// <summary>
    /// A compressor manager that implements the IHttpCompressorManager interface, for runtime loading.
    /// </summary>
    public sealed class CompressorManager : IHttpCompressorManager
    {       

        private LibraryWrapper? _nativeLib;
        private CompressionLevel _compLevel =  CompressionLevel.Fastest;

        /// <summary>
        /// Called by the VNLib.Webserver during startup to initiialize the compressor.
        /// </summary>
        /// <param name="log">The application log provider</param>
        /// <param name="config">The json configuration element</param>
        public void OnLoad(ILogProvider? log, JsonElement? config)
        {          
            //Get the compression element
            if (config.HasValue && config.Value.TryGetProperty("vnlib.net.compression", out JsonElement compEl))
            {
                //Try to get the user specified compression level
                if (compEl.TryGetProperty("level", out JsonElement lEl))
                {
                    _compLevel = (CompressionLevel)lEl.GetUInt16();
                }

                //Allow the user to specify the path to the native library
                if (compEl.TryGetProperty("lib_path", out JsonElement libEl) 
                    && libEl.ValueKind == JsonValueKind.String)
                {
                    string libPath = libEl.GetString()!;

                    log?.Debug("Attempting to load native compression library from: {lib}", libPath);

                    _nativeLib = LibraryWrapper.LoadLibrary(libPath, DllImportSearchPath.SafeDirectories);

                    log?.Debug(
                         "Loaded default native compression library. Supports {sup} with compression level {l} ",
                         GetSupportedMethods(),
                         _compLevel.ToString()
                     );

                    return;
                }

                log?.Debug("'lib_path' was not specified in compression config, falling back to default paths");
            }

            //Fall back system environment variables

            log?.Debug("Attempting to load the default native compression library");

            _nativeLib = LibraryWrapper.LoadDefault();

            log?.Debug(
                "Loaded default native compression library. Supports {sup} with compression level {l} ",
                GetSupportedMethods(),
                _compLevel.ToString()
            );
        }

        ///<inheritdoc/>
        public CompressionMethod GetSupportedMethods()
        {
            if(_nativeLib is null)
            {
                throw new InvalidOperationException("The native library has not been loaded yet.");
            }

            return _nativeLib.GetSupportedMethods();
        }

        ///<inheritdoc/>
        public object AllocCompressor() => new Compressor();

        ///<inheritdoc/>
        public int InitCompressor(object compressorState, CompressionMethod compMethod)
        {
            DebugThrowIfNull(compressorState, nameof(compressorState));
            Compressor compressor = Unsafe.As<Compressor>(compressorState);

            //Instance should be null during initialization calls
            Debug.Assert(compressor.Instance == IntPtr.Zero, "Init was called but and old compressor instance was not properly freed");

            //Alloc the compressor, let native lib raise exception for supported methods
            compressor.Instance = _nativeLib!.AllocateCompressor(compMethod, _compLevel);

            //Return the compressor block size
            return (int)_nativeLib!.GetBlockSize(compressor.Instance);
        }

        ///<inheritdoc/>
        public void DeinitCompressor(object compressorState)
        {
            DebugThrowIfNull(compressorState, nameof(compressorState));
            Compressor compressor = Unsafe.As<Compressor>(compressorState);

            if(compressor.Instance == IntPtr.Zero)
            {
                throw new InvalidOperationException("This compressor instance has not been initialized, cannot free compressor");
            }

            //Free compressor instance
            _nativeLib!.FreeCompressor(compressor.Instance);

            //Clear pointer after successful free
            compressor.Instance = IntPtr.Zero;
        }

        ///<inheritdoc/>
        public int Flush(object compressorState, Memory<byte> output)
        {
            DebugThrowIfNull(compressorState, nameof(compressorState));
            Compressor compressor = Unsafe.As<Compressor>(compressorState);

            if (compressor.Instance == IntPtr.Zero)
            {
                throw new InvalidOperationException("This compressor instance has not been initialized, cannot free compressor");
            }

            //Force a flush until no more data is available
            CompressionResult result = _nativeLib!.CompressBlock(compressor.Instance, output, default, true);
            return result.BytesWritten;
        }

        ///<inheritdoc/>
        public CompressionResult CompressBlock(object compressorState, ReadOnlyMemory<byte> input, Memory<byte> output) 
        {
            DebugThrowIfNull(compressorState, nameof(compressorState));
            Compressor compressor = Unsafe.As<Compressor>(compressorState);

            if (compressor.Instance == IntPtr.Zero)
            {
                throw new InvalidOperationException("This compressor instance has not been initialized, cannot free compressor");
            }

            //Compress the block
            return _nativeLib!.CompressBlock(compressor.Instance, output, input, false);
        }

        /*
         * This compressor manager instance is designed to tbe used by a webserver instance,
         * (or multiple) as internal calls. We can assume the library compression calls 
         * are "correct" and should never pass null objects to these function calls.
         * 
         * Its also a managed type and if null will still raise a null ref exception
         * if the instances are null. This is just cleaner for debugging purposes.
         */

        [Conditional("DEBUG")]
        private static void DebugThrowIfNull<T>(T? obj, string name) => ArgumentNullException.ThrowIfNull(obj, name);

        /*
         * A class to contain the compressor state
         */
        private sealed class Compressor
        {
            public IntPtr Instance;
        }
       
    }
}