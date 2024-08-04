/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: FileCache.cs 
*
* FileCache.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Net;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.IO;

namespace VNLib.WebServer
{
    /// <summary>
    /// File the server will keep in memory and return to user when a specified error code is requested
    /// </summary>
    internal sealed class FileCache : VnDisposeable, IFSChangeHandler
    {
        private readonly string _filePath;

        public readonly HttpStatusCode Code;

        private Lazy<byte[]> _templateData;
           

        /// <summary>
        /// Catch an http error code and return the selected file to user
        /// </summary>
        /// <param name="code">Http status code to catch</param>
        /// <param name="filePath">Path to file contating data to return to use on status code</param>
        private FileCache(HttpStatusCode code, string filePath)
        {
            Code = code;
            _filePath = filePath;
            _templateData = new(LoadTemplateData);
        }

        private byte[] LoadTemplateData()
        {
            //Get file data as binary
            return File.ReadAllBytes(_filePath);
        }

        /// <summary>
        /// Gets a <see cref="IMemoryResponseReader"/> wrapper that may read a copy of the 
        /// file representation
        /// </summary>
        /// <returns>The <see cref="IMemoryResponseReader"/> wrapper around the file data</returns>
        public IMemoryResponseReader GetReader() => new MemReader(_templateData.Value);


        void IFSChangeHandler.OnFileChanged(FileSystemEventArgs e)
        {
            //Update lazy loader for new file update
            _templateData = new(LoadTemplateData);
        }

        protected override void Free()
        {
            //Unsubscribe from file watcher
            FileWatcher.Unsubscribe(_filePath, this);
        }

        /// <summary>
        /// Create a new file cache for a specific error code
        /// and begins listening for changes to the file
        /// </summary>
        /// <param name="code">The status code to produce the file for</param>
        /// <param name="filePath">The path to the file to read</param>
        /// <returns>The new <see cref="FileCache"/> instance if the file exists and is readable, null otherwise</returns>
        public static FileCache? Create(HttpStatusCode code, string filePath)
        {
            //If the file does not exist, return null
            if(!FileOperations.FileExists(filePath))
            {
                return null;
            }

            FileCache ff = new(code, filePath);

            //Subscribe to file changes
            FileWatcher.Subscribe(filePath, ff);

            return ff;
        }

        private sealed class MemReader : IMemoryResponseReader
        {
            private readonly byte[] _memory;

            private int _written;

            public int Remaining { get; private set; }

            internal MemReader(byte[] data)
            {
                //Store ref as memory
                _memory = data;
                Remaining = data.Length;
            }

            public void Advance(int written)
            {
                _written += written;
                Remaining -= written;
            }

            void IMemoryResponseReader.Close() { }

            ReadOnlyMemory<byte> IMemoryResponseReader.GetMemory() => _memory.AsMemory(_written, Remaining);
        }
    }
}