/*
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
using System.Text.Json.Serialization;

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
        private CompConfigurationJson _config = new();

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
                _config = compEl.Deserialize<CompConfigurationJson>()
                    ?? throw new InvalidOperationException("Failed to deserialize compression configuration");

                //Allow the user to specify the path to the native library
                if (!string.IsNullOrWhiteSpace(_config.LibPath))
                {
                    log?.Debug("Attempting to load native compression library from: {lp}", _config.LibPath);

                    _nativeLib = LibraryWrapper.LoadLibrary(_config.LibPath, DllImportSearchPath.SafeDirectories);

                    log?.Debug(
                         "Loaded default native compression library. Supports {sup} with options {l} ",
                         GetSupportedMethods(),
                         _config
                     );

                    return;
                }

                log?.Debug("'lib_path' was not specified in compression config, falling back to default paths");
            }

            //Fall back system environment variables

            log?.Debug("Attempting to load the default native compression library");

            _nativeLib = LibraryWrapper.LoadDefault();

            log?.Debug(
                "Loaded default native compression library. Supports {sup} with options {l} ",
                GetSupportedMethods(),
                _config
            );
        }

        ///<inheritdoc/>
        public CompressionMethod GetSupportedMethods()
        {
            if (_nativeLib is null)
            {
                throw new InvalidOperationException("The native library has not been loaded yet.");
            }

            return _nativeLib.GetSupportedMethods();
        }

        ///<inheritdoc/>
        public object AllocCompressor() => new Compressor();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureMemoryCommitted(Compressor compressor)
        {
            if (compressor.Instance == IntPtr.Zero)
            {
                compressor.Instance = _nativeLib!.CompressionAllocState();

                //Compressor values should still be defaults (or none) after the state is allocated
                Debug.Assert(_nativeLib.GetCompressorLevel(compressor.Instance) == default);
                Debug.Assert(_nativeLib.GetBlockSize(compressor.Instance) == 0);
                Debug.Assert(_nativeLib.GetCompressorType(compressor.Instance) == default);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureMemoryFreed(Compressor compressor)
        {
            _nativeLib!.CompressionFreeState(compressor.Instance);
            compressor.Instance = IntPtr.Zero;
            compressor.SupportsCommitApi = false;
        }

       

        /// <inheritdoc/>

        public void CommitMemory(object compressorState)
        {
            DebugThrowIfNull(compressorState, nameof(compressorState));
            Compressor compressor = Unsafe.As<Compressor>(compressorState);

            EnsureMemoryCommitted(compressor);

            compressor.SupportsCommitApi = true;   //Caller supports explicit commit/free
        }
        
        /// <inheritdoc/>
        public void DecommitMemory(object compressorState)
        {
            DebugThrowIfNull(compressorState, nameof(compressorState));
            Compressor compressor = Unsafe.As<Compressor>(compressorState);

            // Free the compressor state, will also free any compressor instance, will also guard for null pointers
            EnsureMemoryFreed(compressor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int InitCompressorInternal(Compressor compressor, CompressionMethod compMethod)
        {
            _nativeLib!.CompressionAllocCompressor(compressor.Instance, compMethod, _config.Level);
            return (int)_nativeLib!.GetBlockSize(compressor.Instance);
        }

        ///<inheritdoc/>
        public int InitCompressor(object compressorState, CompressionMethod compMethod)
        {
            DebugThrowIfNull(compressorState, nameof(compressorState));
            Compressor compressor = Unsafe.As<Compressor>(compressorState);

            /*
             * If the flag for the new commit API it set, it means they have already committed memory
             * and it's safe to allocate the compressor. If not, we need to ensure memory
             * is committed before allocating the compressor to maintain backwards compatibility.
             * 
             * If the allocation fails, we need to free the memory if we allocated it
             * ourselves to avoid memory leaks.
             */
            if (compressor.SupportsCommitApi)
            {
                return InitCompressorInternal(compressor, compMethod);
            }
            else
            {
                //Ensure memory is committed (legacy support)
                EnsureMemoryCommitted(compressor);

                try
                {
                    return InitCompressorInternal(compressor, compMethod);
                }
                catch
                {
                    EnsureMemoryFreed(compressor);
                    throw;
                }
            }
        }

        ///<inheritdoc/>
        public void DeinitCompressor(object compressorState)
        {
            DebugThrowIfNull(compressorState, nameof(compressorState));
            Compressor compressor = Unsafe.As<Compressor>(compressorState);

            if (compressor.Instance == IntPtr.Zero)
            {
                throw new InvalidOperationException("This compressor instance has not been initialized, cannot free compressor");
            }

            /*
             * Iff the caller uses the commit API, we can assume they will free the memory
             * explicitly. If not, we need to free the memory here to avoid leaks. It maintains
             * ABI backwards compatibility with older servers.
             * 
             * Calling memory free will cause the compressor instance to be freed as well in
             * the native library.
             */

            if (compressor.SupportsCommitApi)
            {
                _nativeLib!.CompressionFreeCompressor(compressor.Instance);

                // After only the compressor is freed, the instance should still be valid but reset to defaults
                Debug.Assert(_nativeLib.GetCompressorLevel(compressor.Instance) == default);
                Debug.Assert(_nativeLib.GetBlockSize(compressor.Instance) == 0);
                Debug.Assert(_nativeLib.GetCompressorType(compressor.Instance) == default);
            }
            else
            {
                EnsureMemoryFreed(compressor);
            }
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

            Debug.Assert(
                output.Length > 0, 
                "Output buffer must be at least 1 byte in length to flush data"
            );

            //Force a flush until no more data is available
            CompressionResult result = _nativeLib!.CompressBlock(
                compressor.Instance, 
                output, 
                input: default, 
                finalBlock: true
            );

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
            return _nativeLib!.CompressBlock(
                compressor.Instance, 
                output, 
                input, 
                finalBlock: false
            );
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

            /// <summary>
            /// In order to make the new commit ABI backwards compatible with older compressors,
            /// this value keeps track of how the memory was allocated. If a caller calls CommitMemory,
            /// it means they support the new explicit api.
            /// 
            /// If the user doesn't support the commit API, memory must be freed on deinit call so state 
            /// memory does not leak.
            /// </summary>
            public bool SupportsCommitApi;

#if DEBUG
            ~Compressor()
            {
                //Debug.Assert(Instance == IntPtr.Zero, "Memory leak detected. Compressor state was not freed before finalization");
            }
#endif

        }


        private sealed record CompConfigurationJson
        {
            [JsonPropertyName("level")]
            public CompressionLevel Level { get; init; } = CompressionLevel.Fastest;

            [JsonPropertyName("lib_path")]
            public string? LibPath { get; init; } = null;            
        }
    }
}
