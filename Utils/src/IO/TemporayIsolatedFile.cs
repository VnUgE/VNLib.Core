/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: TemporayIsolatedFile.cs 
*
* TemporayIsolatedFile.cs is part of VNLib.Utils which is part of the larger 
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
    /// Allows for temporary files to be generated, used, then removed from an <see cref="IsolatedStorageFile"/>
    /// </summary>
    public sealed class TemporayIsolatedFile : BackingStream<IsolatedStorageFileStream>
    {
        private readonly IsolatedStorageDirectory Storage;
        private readonly string Filename; 
        /// <summary>
        /// Creates a new temporary filestream within the specified <see cref="IsolatedStorageFile"/>
        /// </summary>
        /// <param name="storage">The file store to genreate temporary files within</param>
        public TemporayIsolatedFile(IsolatedStorageDirectory storage)
        {
            //Store ref
            this.Storage = storage;
            //Creaet a new random filename
            this.Filename = Path.GetRandomFileName();
            //try to created a new file within the isolaged storage
            this.BaseStream = storage.CreateFile(this.Filename);
        }
        protected override void OnClose()
        {
            //Remove the file from the storage
            Storage.DeleteFile(this.Filename);
        }
    }
}