/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IsolatedStorageDirectory.cs 
*
* IsolatedStorageDirectory.cs is part of VNLib.Utils which is part of the larger 
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
using System.IO.IsolatedStorage;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Represents an open directory within an <see cref="IsolatedStorageFile"/> store for which files can be created, opened, or deleted.
    /// </summary>
    public sealed class IsolatedStorageDirectory : IsolatedStorage
    {
        private readonly string DirectoryPath;
        private readonly IsolatedStorageFile Storage;
        /// <summary>
        /// Creates a new <see cref="IsolatedStorageDirectory"/> within the specified file using the directory name.
        /// </summary>
        /// <param name="storage">A configured and open <see cref="IsolatedStorageFile"/></param>
        /// <param name="dir">The directory name to open or create within the store</param>
        public IsolatedStorageDirectory(IsolatedStorageFile storage, string dir)
        {
            this.Storage = storage;
            this.DirectoryPath = dir;
            //If the directory doesnt exist, create it
            if (!this.Storage.DirectoryExists(dir))
                this.Storage.CreateDirectory(dir);
        }
      
        private IsolatedStorageDirectory(IsolatedStorageDirectory parent, string dirName)
        {
            //Store ref to parent dir
            Parent = parent;
            //Referrence store
            this.Storage = parent.Storage;
            //Add the name of this dir to the end of the specified dir path
            this.DirectoryPath = Path.Combine(parent.DirectoryPath, dirName);
        }

        /// <summary>
        /// Creates a file by its path name within the currnet directory
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <returns>The open file</returns>
        /// <exception cref="IsolatedStorageException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        public IsolatedStorageFileStream CreateFile(string fileName)
        {
            return this.Storage.CreateFile(Path.Combine(DirectoryPath, fileName));
        }
        /// <summary>
        /// Removes a file from the current directory 
        /// </summary>
        /// <param name="fileName">The path of the file to remove</param>
        /// <exception cref="IsolatedStorageException"></exception>
        public void DeleteFile(string fileName)
        {
            this.Storage.DeleteFile(Path.Combine(this.DirectoryPath, fileName));
        }
        /// <summary>
        /// Opens a file that exists within the current directory
        /// </summary>
        /// <param name="fileName">Name with extension of the file</param>
        /// <param name="mode">File mode</param>
        /// <param name="access">File access</param>
        /// <returns>The open <see cref="IsolatedStorageFileStream"/> from the current directory</returns>
        public IsolatedStorageFileStream OpenFile(string fileName, FileMode mode, FileAccess access)
        {
            return this.Storage.OpenFile(Path.Combine(DirectoryPath, fileName), mode, access);
        }
        /// <summary>
        /// Opens a file that exists within the current directory
        /// </summary>
        /// <param name="fileName">Name with extension of the file</param>
        /// <param name="mode">File mode</param>
        /// <param name="access">File access</param>
        /// <param name="share">The file shareing mode</param>
        /// <returns>The open <see cref="IsolatedStorageFileStream"/> from the current directory</returns>
        public IsolatedStorageFileStream OpenFile(string fileName, FileMode mode, FileAccess access, FileShare share)
        {
            return this.Storage.OpenFile(Path.Combine(DirectoryPath, fileName), mode, access, share);
        }

        /// <summary>
        /// Determiens if the specified file path refers to an existing file within the directory
        /// </summary>
        /// <param name="fileName">The name of the file to search for</param>
        /// <returns>True if the file exists within the current directory</returns>
        /// <exception cref="ArgumentNullException"></exception> 
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="IsolatedStorageException"></exception>
        /// <exception cref="InvalidOperationException"></exception> 
        public bool FileExists(string fileName)
        {
            return this.Storage.FileExists(Path.Combine(this.DirectoryPath, fileName));
        }

        /// <summary>
        /// Removes the directory and its contents from the store
        /// </summary>
        public override void Remove()
        {
            Storage.DeleteDirectory(this.DirectoryPath);
        }

        ///<inheritdoc/>
        public override long AvailableFreeSpace => Storage.AvailableFreeSpace;
        ///<inheritdoc/>
        public override long Quota => Storage.Quota;
        ///<inheritdoc/>
        public override long UsedSize => Storage.UsedSize;
        ///<inheritdoc/>
        public override bool IncreaseQuotaTo(long newQuotaSize) => Storage.IncreaseQuotaTo(newQuotaSize);

        /// <summary>
        /// The parent <see cref="IsolatedStorageDirectory"/> this directory is a child within. null if there are no parent directories 
        /// above this dir
        /// </summary>

        public IsolatedStorageDirectory? Parent { get; }
#nullable disable

        /// <summary>
        /// Creates a child directory within the current directory
        /// </summary>
        /// <param name="directoryName">The name of the child directory</param>
        /// <returns>A new <see cref="IsolatedStorageDirectory"/> for which <see cref="IsolatedStorageFileStream"/>s can be opened/created</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public IsolatedStorageDirectory CreateChildDirectory(string directoryName)
        {
            return new IsolatedStorageDirectory(this, directoryName);
        }
    }
}