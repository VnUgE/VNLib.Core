/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpVersion.cs 
*
* HttpVersion.cs is part of VNLib.Net.Http which is part of the larger 
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
    /// HTTP protocol version
    /// </summary>
    [Flags]
    public enum HttpVersion 
    {
        /// <summary>
        /// Enum empty type
        /// </summary>
        None,
        /// <summary>
        /// Http Version 0.9
        /// </summary>
        Http09 = 0x01,
        /// <summary>
        /// Http Version 1
        /// </summary>
        Http1 = 0x02, 
        /// <summary>
        /// Http Version 1.1
        /// </summary>
        Http11 = 0x04, 
        /// <summary>
        /// Http Version 2.0
        /// </summary>
        Http2 = 0x08, 
        /// <summary>
        /// Http Version 3.0
        /// </summary>
        Http3 = 0x10
    }
}