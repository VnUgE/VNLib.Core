/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IoExtensions.cs 
*
* IoExtensions.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using System.IO.IsolatedStorage;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using static VNLib.Utils.Memory.MemoryUtil;

//Disable configure await warning
#pragma warning disable CA2007

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Provieds extension methods for common IO operations
    /// </summary>
    public static class IoExtensions
    {
        /// <summary>
        /// Unlocks the entire file
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [UnsupportedOSPlatform("ios")]
        [UnsupportedOSPlatform("macos")]
        [UnsupportedOSPlatform("tvos")]
        public static void Unlock(this FileStream fs)
        {
            _ = fs ?? throw new ArgumentNullException(nameof(fs));
            //Unlock the entire file
            fs.Unlock(0, fs.Length);
        }

        /// <summary>
        /// Locks the entire file
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [UnsupportedOSPlatform("ios")]
        [UnsupportedOSPlatform("macos")]
        [UnsupportedOSPlatform("tvos")]
        public static void Lock(this FileStream fs)
        {
            _ = fs ?? throw new ArgumentNullException(nameof(fs));
            //Lock the entire length of the file
            fs.Lock(0, fs.Length);
        }

        /// <summary>
        /// Provides an async wrapper for copying data from the current stream to another using an unmanged 
        /// buffer.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest">The destination data stream to write data to</param>
        /// <param name="bufferSize">The size of the buffer to use while copying data. (Value will be clamped to the size of the stream if seeking is available)</param>
        /// <param name="heap">The <see cref="IUnmangedHeap"/> to allocate the buffer from</param>
        /// <param name="token">A token that may cancel asynchronous operations</param>
        /// <returns>A <see cref="ValueTask"/> that completes when the copy operation has completed</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static async ValueTask CopyToAsync(this Stream source, Stream dest, int bufferSize, IUnmangedHeap heap, CancellationToken token = default)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (heap is null)
            {
                throw new ArgumentNullException(nameof(heap));
            }

            if (source.CanSeek)
            {
                bufferSize = (int)Math.Min(source.Length, bufferSize);
            }
            //Alloc a buffer
            using IMemoryOwner<byte> buffer = heap.DirectAlloc<byte>(bufferSize);
            //Wait for copy to complete
            await CopyToAsync(source, dest, buffer.Memory, token);
        }

        /// <summary>
        /// Provides an async wrapper for copying data from the current stream to another with a 
        /// buffer from the <paramref name="heap"/>
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest">The destination data stream to write data to</param>
        /// <param name="bufferSize">The size of the buffer to use while copying data. (Value will be clamped to the size of the stream if seeking is available)</param>
        /// <param name="count">The number of bytes to copy from the current stream to destination stream</param>
        /// <param name="heap">The heap to alloc buffer from</param>
        /// <param name="token">A token that may cancel asynchronous operations</param>
        /// <returns>A <see cref="ValueTask"/> that completes when the copy operation has completed</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static async ValueTask CopyToAsync(this Stream source, Stream dest, long count, int bufferSize, IUnmangedHeap heap, CancellationToken token = default)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.CanSeek)
            {
                bufferSize = (int)Math.Min(source.Length, bufferSize);
            }
            //Alloc a buffer
            using IMemoryOwner<byte> buffer = heap.DirectAlloc<byte>(bufferSize);
            //Wait for copy to complete
            await CopyToAsync(source, dest, buffer.Memory, count, token);
        }

        /// <summary>
        /// Copies data from one stream to another, using self managed buffers. May allocate up to 2MB.
        /// </summary>
        /// <param name="source">Source stream to read from</param>
        /// <param name="dest">Destination stream to write data to</param>
        /// <param name="heap">The heap to allocate buffers from</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void CopyTo(this Stream source, Stream dest, IUnmangedHeap? heap = null)
        {
            if (dest is null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            
            if (!source.CanRead)
            {
                throw new ArgumentException("Source stream is unreadable", nameof(source));
            }

            if (!dest.CanWrite)
            {
                throw new ArgumentException("Destination stream is unwritable", nameof(dest));
            }
            heap ??= Shared;
            //Get a buffer size, maximum of 2mb buffer size if the stream supports seeking, otherwise, min buf size
            int bufSize = source.CanSeek ? (int)Math.Min(source.Length, MAX_BUF_SIZE) : MIN_BUF_SIZE;
            //Length must be 0, so return
            if (bufSize == 0)
            {
                return;
            }
            //Alloc a buffer
            using UnsafeMemoryHandle<byte> buffer = heap.UnsafeAlloc<byte>(bufSize);
            int read;
            do
            {
                //read
                read = source.Read(buffer.Span);
                //Guard
                if (read == 0)
                {
                    break;
                }
                //write only the data that was read (slice)
                dest.Write(buffer.Span[..read]);
            } while (true);
        }

        /// <summary>
        /// Copies data from one stream to another, using self managed buffers. May allocate up to 2MB.
        /// </summary>
        /// <param name="source">Source stream to read from</param>
        /// <param name="dest">Destination stream to write data to</param>
        /// <param name="count">Number of bytes to read/write</param>
        /// <param name="heap">The heap to allocate buffers from</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void CopyTo(this Stream source, Stream dest, long count, IUnmangedHeap? heap = null)
        {
            if (dest is null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            
            if (!source.CanRead)
            {
                throw new ArgumentException("Source stream is unreadable", nameof(source));
            }
            if (!dest.CanWrite)
            {
                throw new ArgumentException("Destination stream is unwritable", nameof(dest));
            }
            //Set default heap
            heap ??= Shared;
            //Get a buffer size, maximum of 2mb buffer size if the stream supports seeking, otherwise, min buf size
            int bufSize = source.CanSeek ? (int)Math.Min(source.Length, MAX_BUF_SIZE) : MIN_BUF_SIZE;
            //Length must be 0, so return
            if (bufSize == 0)
            {
                return;
            }
            //Alloc a buffer
            using UnsafeMemoryHandle<byte> buffer = heap.UnsafeAlloc<byte>(bufSize);
            //wrapper around offset pointer
            long total = 0;
            int read;
            do
            {
                Span<byte> wrapper = buffer.Span[..(int)Math.Min(bufSize, (count - total))];
                //read
                read = source.Read(wrapper);
                //Guard
                if (read == 0)
                {
                    break;
                }
                //write only the data that was read (slice)
                dest.Write(wrapper[..read]);
                //Update total
                total += read;
            } while (true);
        }

        /// <summary>
        /// Copies data from the current stream to the destination stream using the supplied memory buffer
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest">The destination data stream to write data to</param>
        /// <param name="buffer">The buffer to use when copying data</param>
        /// <param name="token">A token that may cancel asynchronous operations</param>
        ///  <returns>A <see cref="ValueTask"/> that completes when the copy operation has completed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static async ValueTask CopyToAsync(this Stream source, Stream dest, Memory<byte> buffer, CancellationToken token = default)
        {
            if (dest is null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            
            //Make sure source can be read from, and dest can be written to
            if (!source.CanRead)
            {
                throw new ArgumentException("Source stream is unreadable", nameof(source));
            }
            if (!dest.CanWrite)
            {
                throw new ArgumentException("Destination stream is unwritable", nameof(dest));
            }
            //Read in loop
            int read;
            while (true)
            {
                //read
                read = await source.ReadAsync(buffer, token);
                //Guard
                if (read == 0)
                {
                    break;
                }
                //write only the data that was read (slice)
                await dest.WriteAsync(buffer[..read], token);
            }
        }

        /// <summary>
        /// Copies data from the current stream to the destination stream using the supplied memory buffer
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest">The destination data stream to write data to</param>
        /// <param name="buffer">The buffer to use when copying data</param>
        /// <param name="count">The number of bytes to copy from the current stream to destination stream</param>
        /// <param name="token">A token that may cancel asynchronous operations</param>
        /// <returns>A <see cref="ValueTask"/> that completes when the copy operation has completed</returns>
        /// <exception cref="ArgumentException"></exception>
        public static async ValueTask CopyToAsync(this Stream source, Stream dest, Memory<byte> buffer, long count, CancellationToken token = default)
        {
            if (dest is null)
            {
                throw new ArgumentNullException(nameof(dest));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            
            //Make sure source can be read from, and dest can be written to
            if (!source.CanRead)
            {
                throw new ArgumentException("Source stream is unreadable", nameof(source));
            }
            if (!dest.CanWrite)
            {
                throw new ArgumentException("Destination stream is unwritable", nameof(dest));
            }
            /*
             * Track total count so we copy the exect number of 
             * bytes from the source
             */
            long total = 0;
            int read;
            while (true)
            {
                //get offset wrapper of the total buffer or remaining count
                Memory<byte> offset = buffer[..(int)Math.Min(buffer.Length, count - total)];
                //read
                read = await source.ReadAsync(offset, token);
                //Guard
                if (read == 0)
                {
                    break;
                }
                //write only the data that was read (slice)
                await dest.WriteAsync(offset[..read], token);
                //Update total
                total += read;
            }
        }

        /// <summary>
        /// Opens a file within the current directory
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="fileName">The name of the file to open</param>
        /// <param name="mode">The <see cref="FileMode"/> to open the file with</param>
        /// <param name="access">The <see cref="FileAccess"/> to open the file with</param>
        /// <param name="share"></param>
        /// <param name="bufferSize">The size of the buffer to read/write with</param>
        /// <param name="options"></param>
        /// <returns>The <see cref="FileStream"/> of the opened file</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FileStream OpenFile(this DirectoryInfo dir,
            string fileName,
            FileMode mode,
            FileAccess access,
            FileShare share = FileShare.None,
            int bufferSize = 4096,
            FileOptions options = FileOptions.None)
        {
            _ = dir ?? throw new ArgumentNullException(nameof(dir));
            string fullPath = Path.Combine(dir.FullName, fileName);
            return new FileStream(fullPath, mode, access, share, bufferSize, options);
        }
       
        /// <summary>
        /// Deletes the speicifed file from the current directory
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="fileName">The name of the file to delete</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeleteFile(this DirectoryInfo dir, string fileName)
        {
            _ = dir ?? throw new ArgumentNullException(nameof(dir));
            string fullPath = Path.Combine(dir.FullName, fileName);
            File.Delete(fullPath);
        }
        
        /// <summary>
        /// Determines if a file exists within the current directory
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="fileName">The name of the file to search for</param>
        /// <returns>True if the file is found and the user has permission to access the file, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FileExists(this DirectoryInfo dir, string fileName)
        {
            _ = dir ?? throw new ArgumentNullException(nameof(dir));
            string fullPath = Path.Combine(dir.FullName, fileName);
            return FileOperations.FileExists(fullPath);
        }


        /// <summary>
        /// Creates a new scope for the given filesystem. All operations will be offset by the given path
        /// within the parent filesystem.
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="offsetPath">The base path to prepend to all requests</param>
        /// <returns>A new <see cref="ISimpleFilesystem"/> with a new filesystem directory scope</returns>
        public static ISimpleFilesystem CreateNewScope(this ISimpleFilesystem fs, string offsetPath) => new FsScope(fs, offsetPath);

        private sealed record class FsScope(ISimpleFilesystem Parent, string OffsetPath) : ISimpleFilesystem
        {

            ///<inheritdoc/>
            public Task DeleteFileAsync(string filePath, CancellationToken cancellation)
            {
                string path = Path.Combine(OffsetPath, filePath);
                return Parent.DeleteFileAsync(path, cancellation);
            }

            ///<inheritdoc/>
            public string GetExternalFilePath(string filePath)
            {
                string path = Path.Combine(OffsetPath, filePath);
                return Parent.GetExternalFilePath(path);
            }

            ///<inheritdoc/>
            public Task<long> ReadFileAsync(string filePath, Stream output, CancellationToken cancellation)
            {
                string path = Path.Combine(OffsetPath, filePath);
                return Parent.ReadFileAsync(path, output, cancellation);
            }

            ///<inheritdoc/>
            public Task WriteFileAsync(string filePath, Stream data, string contentType, CancellationToken cancellation)
            {
                string path = Path.Combine(OffsetPath, filePath);
                return Parent.WriteFileAsync(path, data, contentType, cancellation);
            }
        }

        /// <summary>
        /// The maximum buffer size to use when copying files
        /// </summary>
        public const long MaxCopyBufferSize = 0x10000; //64k

        /// <summary>
        /// The minimum buffer size to use when copying files
        /// </summary>
        public const long MinCopyBufferSize = 0x1000; //4k

        /// <summary>
        /// Creates a new <see cref="ISimpleFilesystem"/> wrapper for the given <see cref="IsolatedStorageDirectory"/>
        /// <para>
        /// Buffers are clamped to <see cref="MaxCopyBufferSize"/> and <see cref="MinCopyBufferSize"/>
        /// </para>
        /// </summary>
        /// <param name="dir"></param>
        /// <returns>A <see cref="ISimpleFilesystem"/> wrapper around the <see cref="IsolatedStorageDirectory"/></returns>
        public static ISimpleFilesystem CreateSimpleFs(this IsolatedStorageDirectory dir) => new IsolatedStorageSimpleFs(dir);

        private sealed record class IsolatedStorageSimpleFs(IsolatedStorageDirectory Directory) : ISimpleFilesystem
        {
            ///<inheritdoc/>
            public string GetExternalFilePath(string filePath) => Directory.GetFullFilePath(filePath);

            ///<inheritdoc/>
            public Task DeleteFileAsync(string filePath, CancellationToken cancellation)
            {
                Directory.DeleteFile(filePath);
                return Task.CompletedTask;
            }

            ///<inheritdoc/>
            public async Task WriteFileAsync(string filePath, Stream data, string contentType, CancellationToken cancellation)
            {
                //For when I forget and increase the buffer size
                Debug.Assert(MaxCopyBufferSize < int.MaxValue, "MaxCopyBufferSize is too large to be cast to an int");

                //Try to calculate a buffer size
                long bufferSize = data.CanSeek ? Math.Clamp(data.Length, MinCopyBufferSize, MaxCopyBufferSize) : MinCopyBufferSize;

                //Open the local file
                await using IsolatedStorageFileStream output = Directory.OpenFile(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                //Copy file
                await CopyToAsync(data, output, (int)bufferSize, Shared, cancellation);

                //All done
            }

            ///<inheritdoc/>
            public async Task<long> ReadFileAsync(string filePath, Stream output, CancellationToken cancellation)
            {
                //For when I forget and increase the buffer size
                Debug.Assert(MaxCopyBufferSize < int.MaxValue, "MaxCopyBufferSize is too large to be cast to an int");

                //Open the local file
                await using IsolatedStorageFileStream local = Directory.OpenFile(filePath, FileMode.Open, FileAccess.Read);

                //Try to calculate a buffer size
                long bufferSize = Math.Clamp(local.Length, MinCopyBufferSize, MaxCopyBufferSize);

                //Copy file
                await CopyToAsync(local, output, (int)bufferSize, Shared, cancellation);

                return local.Length;
            }

        }
    }
}