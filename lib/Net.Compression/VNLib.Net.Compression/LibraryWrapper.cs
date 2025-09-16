/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: LibraryWrapper.cs 
*
* LibraryWrapper.cs is part of VNLib.Net.Compression which is part of 
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
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Native;
using VNLib.Utils.Extensions;

using VNLib.Net.Http;

namespace VNLib.Net.Compression
{
    /*
     * Configure the delegate methods for the native library
     * 
     * All calling conventions are set to Cdecl because the native 
     * library is compiled with Cdecl on all platforms.
     */

    [SafeMethodName("GetSupportedCompressors")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate CompressionMethod GetSupportedMethodsDelegate();

    [SafeMethodName("GetCompressorBlockSize")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate long GetBlockSizeDelegate(IntPtr compressor);

    [SafeMethodName("GetCompressorType")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate CompressionMethod GetCompressorTypeDelegate(IntPtr compressor);

    [SafeMethodName("GetCompressorLevel")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate CompressionLevel GetCompressorLevelDelegate(IntPtr compressor);

    [SafeMethodName("GetCompressedSize")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate long GetCompressedSizeDelegate(IntPtr compressor, ulong uncompressedSize, int flush);

    [SafeMethodName("CompressBlock")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    unsafe delegate int CompressBlockDelegate(IntPtr compressor, CompressionOperation* operation);    

    /*
     *  V2 API
     */

    [SafeMethodName("CompressionAllocState")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    unsafe delegate int CompressionAllocStateDelegate(void** state);

    [SafeMethodName("CompressionFreeState")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int CompressionFreeStateDelegate(IntPtr state);

    [SafeMethodName("CompressionAllocCompressor")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int CompressionAllocCompressorDelegate(IntPtr state, CompressionMethod type, CompressionLevel level);

    [SafeMethodName("CompressionFreeCompressor")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int CompressionFreeCompressorDelegate(IntPtr state);

    /// <summary>
    /// <para>
    /// Represents a wrapper that provides access to the native compression library
    /// specified by a file path. 
    /// </para>
    /// <para>
    /// NOTE: This library is not meant to be freed, its meant to be loaded at runtime
    /// and used for the lifetime of the application.
    /// </para>
    /// </summary>
    internal sealed class LibraryWrapper : IDisposable
    {
        private readonly SafeLibraryHandle _lib;
        private MethodTable _methodTable;

        public string LibFilePath { get; }

        private LibraryWrapper(SafeLibraryHandle lib, string path, in MethodTable methodTable)
        {
            _lib =  lib;
            _methodTable = methodTable;
            LibFilePath = path;
        }

        /// <summary>
        /// Loads the native library at the specified path into the current process
        /// </summary>
        /// <param name="filePath">The path to the native library to load</param>
        /// <param name="searchType"></param>
        /// <returns>The native library wrapper</returns>
        public static LibraryWrapper LoadLibrary(string filePath, DllImportSearchPath searchType)
        {
            //Load the library into the current process
            SafeLibraryHandle lib = SafeLibraryHandle.LoadLibrary(filePath, searchType);
            
            try
            {
                //build the method table
                MethodTable methods = new()
                {
                    GetMethods = lib.DangerousGetFunction<GetSupportedMethodsDelegate>(),

                    GetBlockSize = lib.DangerousGetFunction<GetBlockSizeDelegate>(),

                    GetCompType = lib.DangerousGetFunction<GetCompressorTypeDelegate>(),

                    GetCompLevel = lib.DangerousGetFunction<GetCompressorLevelDelegate>(),

                    GetOutputSize = lib.DangerousGetFunction<GetCompressedSizeDelegate>(),

                    Compress = lib.DangerousGetFunction<CompressBlockDelegate>(),

                    //v2 alloc/free api
                    AllocState = lib.DangerousGetFunction<CompressionAllocStateDelegate>(),
                    FreeState = lib.DangerousGetFunction<CompressionFreeStateDelegate>(),

                    AllocComp = lib.DangerousGetFunction<CompressionAllocCompressorDelegate>(),
                    FreeComp = lib.DangerousGetFunction<CompressionFreeCompressorDelegate>(),
                };

                return new (lib, filePath, in methods);
            }
            catch
            {
                lib.Dispose();
                throw;
            }
        }


        /// <summary>
        /// Loads the default native compression library defined by 
        /// process environment variables
        /// </summary>
        /// <returns>A new <see cref="LibraryWrapper"/> library handle</returns>
        public static LibraryWrapper LoadDefault()
        {
            string? libPath = Environment.GetEnvironmentVariable(NativeCompressionLib.SharedLibFilePathEnv)
                ?? NativeCompressionLib.SharedLibDefaultName;

            return LoadLibrary(libPath, DllImportSearchPath.SafeDirectories);
        }

        /// <summary>
        /// Gets an enum value of the supported compression methods by the underlying library
        /// </summary>
        /// <returns>The supported compression methods</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompressionMethod GetSupportedMethods() => _methodTable.GetMethods();

        /*
         * Block size is stored as a uint32 in the native library
         * compressor struct
         */

        /// <summary>
        /// Gets the block size of the specified compressor or 0 if 
        /// compressor does not hint it's optimal block size
        /// </summary>
        /// <param name="compressor">A pointer to the compressor instance </param>
        /// <returns>A integer value of the compressor block size</returns>
        /// <exception cref="NativeCompressionException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetBlockSize(IntPtr compressor)
        {
            long result = _methodTable.GetBlockSize(compressor);
            ThrowHelper.ThrowIfError(result);
            return (uint)result;
        }

        /// <summary>
        /// Gets the compressor type of the specified compressor
        /// </summary>
        /// <param name="compressor">A pointer to the compressor instance</param>
        /// <returns>A enum value that represents the compressor type</returns>
        /// <exception cref="NativeCompressionException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompressionMethod GetCompressorType(IntPtr compressor)
        {
            CompressionMethod result = _methodTable.GetCompType(compressor);
            ThrowHelper.ThrowIfError((long)result);
            return result;
        }

        /// <summary>
        /// Gets the compression level of the specified compressor
        /// </summary>
        /// <param name="compressor">A pointer to the compressor instance</param>
        /// <returns>The <see cref="CompressionLevel"/> of the current compressor</returns>
        /// <exception cref="NativeCompressionException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompressionLevel GetCompressorLevel(IntPtr compressor)
        {
            CompressionLevel result = _methodTable.GetCompLevel(compressor);
            ThrowHelper.ThrowIfError((long)result);
            return result;
        }
       

        /// <summary>
        /// V2 api allows for expicit allocation of compressor state for reuse. Must
        /// be freed with <see cref="CompressionFreeState(IntPtr)"/>
        /// </summary>
        /// <returns>A pointer to the allocated compressor state</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe IntPtr CompressionAllocState()
        {
            void* state = null;
            
            int result = _methodTable.AllocState(&state);
         
            ThrowHelper.ThrowIfError(result);
            
            Debug.Assert(state != null, "Expected compressor state pointer to be assinged on successful allocation");
            
            return (IntPtr)state;

        }

        /// <summary>
        /// Frees a previously allocated compressor state instance previously allocated
        /// by <see cref="CompressionAllocState()"/>. Must be freed to avoid memory leaks.
        /// </summary>
        /// <param name="state"></param>
        /// <remarks>
        /// If a compressor instance was allocated as part of the current state, it will be 
        /// freed automatically.
        /// </remarks>
        /// <exception cref="NativeCompressionException">
        /// If a compressor was allocated and had an error during free operation 
        /// </exception>"
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompressionFreeState(IntPtr state)
        {
            if (state == IntPtr.Zero)
            {
                return;
            }

            int result = _methodTable.FreeState(state);
            ThrowHelper.ThrowIfError(result);
        }

        /// <summary>
        /// Allocates a new compressor instance of the specified type and compression level
        /// </summary>
        /// <param name="state">A pointer to an allocated compression state memory</param>
        /// <param name="type">The compressor type to allocate</param>
        /// <param name="level">The desired compression level</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompressionAllocCompressor(IntPtr state, CompressionMethod type, CompressionLevel level)
        {
            if (state == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(state), "Compressor state pointer cannot be null");
            }

            int result = _methodTable.AllocComp(state, type, level);            
            ThrowHelper.ThrowIfError(result);
        }

        /// <summary>
        /// Frees a previously allocated compressor instance allocated by 
        /// <see cref="CompressionAllocCompressor(IntPtr, CompressionMethod, CompressionLevel)"/>
        /// </summary>
        /// <param name="state">A pointer to a state instance to free the allocated compressor on</param>
        /// <remarks>
        /// Subsiquent calls to this method with the same state pointer will be a no-op.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompressionFreeCompressor(IntPtr state)
        {
            if (state == IntPtr.Zero)
            {
                return;
            }

            int result = _methodTable.FreeComp(state);
            ThrowHelper.ThrowIfError(result);
        }       

        /// <summary>
        /// Frees the specified compressor instance without raising exceptions
        /// </summary>
        /// <param name="state">A pointer to the valid compressor instance to free</param>
        /// <returns>A value indicating the result of the free operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int FreeSafeCompressor(IntPtr state) => _methodTable.FreeState(state);

        /// <summary>
        /// Determines the output size of a given input size and flush mode for the specified compressor
        /// </summary>
        /// <param name="compressor">A pointer to the compressor instance</param>
        /// <param name="inputSize">The size of the input block to compress</param>
        /// <param name="flush">A value that specifies a flush operation</param>
        /// <returns>Returns the size of the required output buffer</returns>
        /// <permission cref="OverflowException"></permission>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="NativeCompressionException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetOutputSize(IntPtr compressor, ulong inputSize, int flush)
        {
            long result = _methodTable.GetOutputSize(compressor, inputSize, flush);
            ThrowHelper.ThrowIfError(result);
            return (ulong)result;
        }

        /// <summary>
        /// Compresses a block of data using the specified compressor instance
        /// </summary>
        /// <param name="compressor">The compressor instance used to compress data</param>
        /// <param name="operation">A pointer to the compression operation structure</param>
        /// <returns>The result of the operation</returns>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="NativeLibraryException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int CompressBlock(IntPtr compressor, CompressionOperation* operation)
        {
            int result = _methodTable.Compress(compressor, operation);
            ThrowHelper.ThrowIfError(result);
            return result;
        }

        ///<inheritdoc/>
        ~LibraryWrapper()
        {
            _methodTable = default;
            _lib.Dispose();
        }

        /// <summary>
        /// Manually releases the library
        /// </summary>
        public void Dispose()
        {
            _methodTable = default;
            _lib.Dispose();
            GC.SuppressFinalize(this);
        }

        private readonly struct MethodTable
        {
            public GetSupportedMethodsDelegate GetMethods { get; init; }

            public GetBlockSizeDelegate GetBlockSize { get; init; }

            public GetCompressorTypeDelegate GetCompType { get; init; }

            public GetCompressorLevelDelegate GetCompLevel { get; init; }

            public CompressionAllocStateDelegate AllocState { get; init; }

            public CompressionFreeStateDelegate FreeState { get; init; }

            public CompressionAllocCompressorDelegate AllocComp { get; init; }

            public CompressionFreeCompressorDelegate FreeComp { get; init; }

            public GetCompressedSizeDelegate GetOutputSize { get; init; }

            public CompressBlockDelegate Compress { get; init; }
        }
    }
}