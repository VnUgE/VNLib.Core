/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: IFBMMessage.cs 
*
* IFBMMessage.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

using VNLib.Net.Http;

namespace VNLib.Net.Messaging.FBM
{
    /// <summary>
    /// Represents basic Fixed Buffer Message protocol operations
    /// </summary>
    public interface IFBMMessage
    {
        /// <summary>
        /// The unique id of the message (nonzero)
        /// </summary>
        int MessageId { get; }
        /// <summary>
        /// Writes a data body to the message of the specified content type
        /// </summary>
        /// <param name="body">The body of the message to copy</param>
        /// <param name="contentType">The content type of the message body</param>
        /// <exception cref="OutOfMemoryException"></exception>
        void WriteBody(ReadOnlySpan<byte> body, ContentType contentType = ContentType.Binary);
        /// <summary>
        /// Appends an arbitrary header to the current request buffer
        /// </summary>
        /// <param name="header">The header id</param>
        /// <param name="value">The value of the header</param>
        /// <exception cref="OutOfMemoryException"></exception>
        void WriteHeader(byte header, ReadOnlySpan<char> value);
        /// <summary>
        /// Appends an arbitrary header to the current request buffer
        /// </summary>
        /// <param name="header">The <see cref="HeaderCommand"/> of the header</param>
        /// <param name="value">The value of the header</param>
        /// <exception cref="OutOfMemoryException"></exception>
        void WriteHeader(HeaderCommand header, ReadOnlySpan<char> value);
    }
}