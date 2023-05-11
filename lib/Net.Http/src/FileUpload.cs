/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: FileUpload.cs 
*
* FileUpload.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents an file that was received as an entity body, either using Multipart/FormData or as the entity body itself
    /// </summary>
    /// <param name="ContentType">
    /// Content type of uploaded file
    /// </param>
    /// <param name="FileData">
    /// The file data captured on upload
    /// </param>
    /// <param name="FileName">
    /// Name of file uploaded
    /// </param>
    /// <param name="DisposeStream">
    /// A value that indicates whether the stream should be disposed when the handle is freed
    /// </param>
    public readonly record struct FileUpload(Stream FileData, bool DisposeStream, ContentType ContentType, string? FileName)
    {
        /// <summary>
        /// Disposes the stream if the handle is owned
        /// </summary>
        public readonly void Free()
        {
            //Dispose the handle if we own it
            if (DisposeStream)
            {
                //This should always be synchronous
                FileData.Dispose();
            }
        }
    }
}