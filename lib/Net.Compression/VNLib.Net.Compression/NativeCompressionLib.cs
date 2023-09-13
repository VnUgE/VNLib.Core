/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: NativeCompressionLib.cs 
*
* NativeCompressionLib.cs is part of VNLib.Net.Compression which is part of 
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

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Extensions;

namespace VNLib.Net.Compression
{
    /// <summary>
    /// A referrence native compression library implementation. Allows for creating compressor instances
    /// from a native dll.
    /// </summary>
    public sealed class NativeCompressionLib : VnDisposeable, INativeCompressionLib
    {
        private readonly LibraryWrapper _library;

        private NativeCompressionLib(LibraryWrapper nativeLib) => _library = nativeLib;

        ///<inheritdoc/>
        protected override void Free() => _library.Dispose();

        /// <summary>
        /// Loads the native compression DLL at the specified file path and search pattern
        /// </summary>
        /// <param name="libPath">The path (relative or absolute) path to the native dll to load</param>
        /// <param name="searchPath">The dll search pattern</param>
        /// <returns>A new <see cref="NativeCompressionLib"/> library handle</returns>
        public static NativeCompressionLib LoadLibrary(string libPath, DllImportSearchPath searchPath)
        {
            LibraryWrapper wrapper = LibraryWrapper.LoadLibrary(libPath, searchPath);
            return new NativeCompressionLib(wrapper);
        }

        ///<inheritdoc/>
        ///<exception cref="NativeCompressionException"></exception>
        public CompressionMethod GetSupportedMethods()
        {
            Check();
            return _library.GetSupportedMethods();
        }

        ///<inheritdoc/>
        ///<exception cref="NativeCompressionException"></exception>
        public INativeCompressor AllocCompressor(CompressionMethod method, CompressionLevel level)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope

            SafeHandle libHandle = AllocSafeCompressorHandle(method, level);
            return new Compressor(_library, libHandle);

#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        ///<inheritdoc/>
        ///<exception cref="NativeCompressionException"></exception>
        public SafeHandle AllocSafeCompressorHandle(CompressionMethod method, CompressionLevel level)
        {
            Check();
            //Alloc compressor then craete a safe handle
            IntPtr comp = _library.AllocateCompressor(method, level);
            return new SafeCompressorHandle(_library, comp);
        }

        internal sealed record class Compressor(LibraryWrapper LibComp, SafeHandle CompressorHandle) : INativeCompressor
        {

            ///<inheritdoc/>
            public CompressionResult Compress(ReadOnlyMemory<byte> input, Memory<byte> output)
            {
                CompressorHandle.ThrowIfClosed();
                IntPtr compressor = CompressorHandle.DangerousGetHandle();
                return LibComp.CompressBlock(compressor, output, input, false);
            }

            ///<inheritdoc/>
            public void Dispose() => CompressorHandle.Dispose();

            ///<inheritdoc/>
            public int Flush(Memory<byte> buffer)
            {
                CompressorHandle.ThrowIfClosed();
                IntPtr compressor = CompressorHandle.DangerousGetHandle();
                CompressionResult result = LibComp.CompressBlock(compressor, buffer, ReadOnlyMemory<byte>.Empty, true);
                return result.BytesWritten;
            }

            ///<inheritdoc/>
            public uint GetBlockSize()
            {
                CompressorHandle.ThrowIfClosed();
                IntPtr compressor = CompressorHandle.DangerousGetHandle();
                return LibComp.GetBlockSize(compressor);
            }

            ///<inheritdoc/>
            public uint GetCompressedSize(uint size)
            {
                CompressorHandle.ThrowIfClosed();
                IntPtr compressor = CompressorHandle.DangerousGetHandle();
                return (uint)LibComp.GetOutputSize(compressor, size, 1);
            }

            ///<inheritdoc/>
            public CompressionLevel GetCompressionLevel()
            {
                CompressorHandle.ThrowIfClosed();
                IntPtr compressor = CompressorHandle.DangerousGetHandle();
                return LibComp.GetCompressorLevel(compressor);
            }

            ///<inheritdoc/>
            public CompressionMethod GetCompressionMethod()
            {
                CompressorHandle.ThrowIfClosed();
                IntPtr compressor = CompressorHandle.DangerousGetHandle();
                return LibComp.GetCompressorType(compressor);
            }
        }
    }
}