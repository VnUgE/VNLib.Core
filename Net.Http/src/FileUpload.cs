/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Text;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

using static VNLib.Net.Http.Core.CoreBufferHelpers;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Represents an file that was received as an entity body, either using Multipart/FormData or as the entity body itself
    /// </summary>
    public readonly struct FileUpload 
    {
        /// <summary>
        /// Content type of uploaded file
        /// </summary>
        public readonly ContentType ContentType;
        /// <summary>
        /// Name of file uploaded
        /// </summary>
        public readonly string FileName;
        /// <summary>
        /// The file data captured on upload
        /// </summary>
        public readonly Stream FileData;
        
        private readonly bool OwnsHandle;

        /// <summary>
        /// Allocates a new binary buffer, encodes, and copies the specified data to a new <see cref="FileUpload"/>
        /// structure of the specified content type
        /// </summary>
        /// <param name="data">The string data to copy</param>
        /// <param name="dataEncoding">The encoding instance to encode the string data from</param>
        /// <param name="filename">The name of the file</param>
        /// <param name="ct">The content type of the file data</param>
        /// <returns>The <see cref="FileUpload"/> container</returns>
        internal static FileUpload FromString(ReadOnlySpan<char> data, Encoding dataEncoding, string filename, ContentType ct)
        {
            //get number of bytes 
            int bytes = dataEncoding.GetByteCount(data);
            //get a buffer from the HTTP heap
            MemoryHandle<byte> buffHandle = HttpPrivateHeap.Alloc<byte>(bytes);
            try
            {
                //Convert back to binary
                bytes = dataEncoding.GetBytes(data, buffHandle);

                //Create a new memory stream encapsulating the file data
                VnMemoryStream vms = VnMemoryStream.ConsumeHandle(buffHandle, bytes, true);

                //Create new upload wrapper
                return new (vms, filename, ct, true);
            }
            catch
            {
                //Make sure the hanle gets disposed if there is an error
                buffHandle.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Initialzes a new <see cref="FileUpload"/> structure from the specified data
        /// and file information.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="filename"></param>
        /// <param name="ct"></param>
        /// <param name="ownsHandle"></param>
        public FileUpload(Stream data, string filename, ContentType ct, bool ownsHandle)
        {
            FileName = filename;
            ContentType = ct;
            //Store handle ownership
            OwnsHandle = ownsHandle;
            //Store the stream
            FileData = data;
        }

        /// <summary>
        /// Releases any memory the current instance holds if it owns the handles
        /// </summary>
        internal readonly void Free()
        {
            //Dispose the handle if we own it
            if (OwnsHandle)
            {
                //This should always be synchronous
                FileData.Dispose();
            }
        }
    }
}