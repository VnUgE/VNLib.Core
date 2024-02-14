/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IFSChangeHandler.cs 
*
* IFSChangeHandler.cs is part of VNLib.Utils which is part of the larger 
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

using System.IO;

namespace VNLib.Utils.IO
{
    /// <summary>
    /// Represents an object that can handle file system change events
    /// </summary>
    public interface IFSChangeHandler
    {
        /// <summary>
        /// Raised when a file is changed in the filesystem
        /// </summary>
        /// <param name="e">The change event</param>
        void OnFileChanged(FileSystemEventArgs e);
    }
}