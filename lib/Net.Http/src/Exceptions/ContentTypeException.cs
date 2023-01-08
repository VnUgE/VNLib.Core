/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ContentTypeException.cs 
*
* ContentTypeException.cs is part of VNLib.Net.Http which is part of the larger 
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

namespace VNLib.Net.Http
{
    /// <summary>
    /// Thrown when the application attempts to submit a response to a client 
    /// when the client does not accept the given content type
    /// </summary>
    public sealed class ContentTypeUnacceptableException:FormatException 
    {
        public ContentTypeUnacceptableException(string message) : base(message) {}

        public ContentTypeUnacceptableException()
        {}

        public ContentTypeUnacceptableException(string message, Exception innerException) : base(message, innerException)
        {}
    }
}