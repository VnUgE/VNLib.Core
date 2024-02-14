/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: DirectFileStream.cs 
*
* DirectFileStream.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

using VNLib.Utils;
using VNLib.Net.Http;

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

namespace VNLib.Plugins.Essentials.Content
{

    /*
     *  Why does this file exist? Well, in .NET streaming files is slow as crap.
     *  It is by-far the largest bottleneck in this framework. 
     *  
     *  I wanted more direct control over file access for future file performance 
     *  improvments. For now using the RandomAccess class bypasses the internal 
     *  buffering used by the filestream class. I saw almost no difference in
     *  per-request performance but a slight reduction in processor usage across
     *  profiling sessions. This class also makes use of the new IHttpStreamResponse
     *  interface.
     */

    internal sealed class DirectFileStream(SafeFileHandle fileHandle) : VnDisposeable, IHttpStreamResponse
    {
        private long _position;

        /// <summary>
        /// Gets the current file pointer position
        /// </summary>
        public long Position => _position;

        /// <summary>
        /// Gets the length of the file
        /// </summary>
        public readonly long Length = RandomAccess.GetLength(fileHandle);

        ///<inheritdoc/>
        public async ValueTask<int> ReadAsync(Memory<byte> buffer)
        {
            //Read data from the file into the buffer, using the current position as the starting offset
            long read = await RandomAccess.ReadAsync(fileHandle, buffer, _position, default);

            _position += read;

            return (int)read;
        }

        ///<inheritdoc/>
        public ValueTask DisposeAsync()
        {
            //Interal dispose
            Dispose();
            return ValueTask.CompletedTask;
        }

        ///<inheritdoc/>
        protected override void Free() => fileHandle.Dispose();

        /// <summary>
        /// Equivalent to <see cref="FileStream.Seek(long, SeekOrigin)"/> but for a 
        /// <see cref="DirectFileStream"/>
        /// </summary>
        /// <param name="offset">The number in bytes to see the stream position to</param>
        /// <param name="origin">The offset origin</param>
        public void Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    ArgumentOutOfRangeException.ThrowIfNegative(offset);
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(_position + offset, Length);
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, 0);
                    _position = Length + offset;
                    break;
            }
        }

        /// <summary>
        /// Opens a file for direct access with default options
        /// </summary>
        /// <param name="fileName">The name of the file to open</param>
        /// <returns>The new direct file-stream</returns>
        public static DirectFileStream Open(string fileName) 
            => new(File.OpenHandle(fileName, options: FileOptions.SequentialScan | FileOptions.Asynchronous));
    }
}