/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils;
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
    delegate int GetBlockSizeDelegate(IntPtr compressor);

    [SafeMethodName("GetCompressorType")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate CompressionMethod GetCompressorTypeDelegate(IntPtr compressor);

    [SafeMethodName("GetCompressorLevel")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate CompressionLevel GetCompressorLevelDelegate(IntPtr compressor);

    [SafeMethodName("AllocateCompressor")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate IntPtr AllocateCompressorDelegate(CompressionMethod type, CompressionLevel level);

    [SafeMethodName("FreeCompressor")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int FreeCompressorDelegate(IntPtr compressor);

    [SafeMethodName("GetCompressedSize")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int GetCompressedSizeDelegate(IntPtr compressor, int uncompressedSize, int flush);

    [SafeMethodName("CompressBlock")]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    unsafe delegate int CompressBlockDelegate(IntPtr compressor, CompressionOperation* operation);

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
    internal sealed class LibraryWrapper 
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
                    GetMethods = lib.DangerousGetMethod<GetSupportedMethodsDelegate>(),

                    GetBlockSize = lib.DangerousGetMethod<GetBlockSizeDelegate>(),

                    GetCompType = lib.DangerousGetMethod<GetCompressorTypeDelegate>(),

                    GetCompLevel = lib.DangerousGetMethod<GetCompressorLevelDelegate>(),

                    Alloc = lib.DangerousGetMethod<AllocateCompressorDelegate>(),

                    Free = lib.DangerousGetMethod<FreeCompressorDelegate>(),

                    GetOutputSize = lib.DangerousGetMethod<GetCompressedSizeDelegate>(),

                    Compress = lib.DangerousGetMethod<CompressBlockDelegate>()
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
        /// Gets an enum value of the supported compression methods by the underlying library
        /// </summary>
        /// <returns>The supported compression methods</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompressionMethod GetSupportedMethods() => _methodTable.GetMethods();

        /// <summary>
        /// Gets the block size of the specified compressor
        /// </summary>
        /// <param name="compressor">A pointer to the compressor instance </param>
        /// <returns>A integer value of the compressor block size</returns>
        /// <exception cref="NativeCompressionException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBlockSize(IntPtr compressor)
        {
            int result = _methodTable.GetBlockSize(compressor);
            ThrowHelper.ThrowIfError((ERRNO)result);
            return result;
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
            ThrowHelper.ThrowIfError((int)result);
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
            ThrowHelper.ThrowIfError((int)result);
            return result;
        }

        /// <summary>
        /// Allocates a new compressor instance of the specified type and compression level
        /// </summary>
        /// <param name="type">The compressor type to allocate</param>
        /// <param name="level">The desired compression level</param>
        /// <returns>A pointer to the newly allocated compressor instance</returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="NativeCompressionException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr AllocateCompressor(CompressionMethod type, CompressionLevel level)
        {
            IntPtr result = _methodTable.Alloc(type, level);
            ThrowHelper.ThrowIfError(result);
            return result;
        }

        /// <summary>
        /// Frees the specified compressor instance
        /// </summary>
        /// <param name="compressor">A pointer to the valid compressor instance to free</param>
        /// <returns>A value indicating the result of the free operation</returns>
        /// <exception cref="NativeCompressionException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FreeCompressor(IntPtr compressor)
        {
            int result = _methodTable.Free(compressor);
            ThrowHelper.ThrowIfError(result);
            if(result == 0)
            {
                throw new NativeCompressionException("Failed to free the compressor instance");
            }
        }

        /// <summary>
        /// Frees the specified compressor instance without raising exceptions
        /// </summary>
        /// <param name="compressor">A pointer to the valid compressor instance to free</param>
        /// <returns>A value indicating the result of the free operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FreeSafeCompressor(IntPtr compressor) => _methodTable.Free(compressor);

        /// <summary>
        /// Determines the output size of a given input size and flush mode for the specified compressor
        /// </summary>
        /// <param name="compressor">A pointer to the compressor instance</param>
        /// <param name="inputSize">The size of the input block to compress</param>
        /// <param name="flush">A value that specifies a flush operation</param>
        /// <returns>Returns the size of the required output buffer</returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="NativeCompressionException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOutputSize(IntPtr compressor, int inputSize, int flush)
        {
            int result = _methodTable.GetOutputSize(compressor, inputSize, flush);
            ThrowHelper.ThrowIfError(result);
            return result;
        }

        /// <summary>
        /// Compresses a block of data using the specified compressor instance
        /// </summary>
        /// <param name="compressor">The compressor instance used to compress data</param>
        /// <param name="operation">A pointer to the compression operation structure</param>
        /// <returns>The result of the operation</returns>
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
        internal void ManualRelease()
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

            public AllocateCompressorDelegate Alloc { get; init; }

            public FreeCompressorDelegate Free { get; init; }

            public GetCompressedSizeDelegate GetOutputSize { get; init; }

            public CompressBlockDelegate Compress { get; init; }
        }
    }
}