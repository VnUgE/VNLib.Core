/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ISimpleFilesystem.cs 
*
* ISimpleFilesystem.cs is part of VNLib.Utils which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Utils.IO
{

    /// <summary>
    /// Represents an opaque storage interface that abstracts simple storage operations
    /// ignorant of the underlying storage system.
    /// </summary>
    public interface ISimpleFilesystem
    {
        /// <summary>
        /// Gets the full public file path for the given relative file path
        /// </summary>
        /// <param name="filePath">The relative file path of the item to get the full path for</param>
        /// <returns>The full relative file path</returns>
        string GetExternalFilePath(string filePath);

        /// <summary>
        /// Deletes a file from the storage system asynchronously
        /// </summary>
        /// <param name="filePath">The path to the file to delete</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that represents and asynchronous work</returns>
        ValueTask DeleteFileAsync(string filePath, CancellationToken cancellation);

        /// <summary>
        /// Writes a file from the stream to the given file location
        /// </summary>
        /// <param name="filePath">The path to the file to write to</param>
        /// <param name="data">The file data to stream</param>
        /// <param name="contentType">The content type of the file to write</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that represents and asynchronous work</returns>
        ValueTask WriteFileAsync(string filePath, Stream data, string contentType, CancellationToken cancellation);

        /// <summary>
        /// Reads a file from the storage system at the given path asynchronously
        /// </summary>
        /// <param name="filePath">The file to read</param>
        /// <param name="output">The stream to write the file output to</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The number of bytes read, -1 if the operation failed</returns>
        ValueTask<long> ReadFileAsync(string filePath, Stream output, CancellationToken cancellation);

        /// <summary>
        /// Reads a file from the storage system at the given path asynchronously
        /// </summary>
        /// <param name="filePath">The file to read</param>
        /// <param name="options">The file options to pass to the file open mechanism</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A stream of the file data</returns>
        ValueTask<Stream?> OpenFileAsync(string filePath, FileAccess options, CancellationToken cancellation);
    }
}
