/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: HttpRangeType.cs 
*
* HttpRangeType.cs is part of VNLib.Net.Http which is part of the larger 
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
    /// An enumeration of http range types to observe from http requests
    /// </summary>
    [Flags]
    public enum HttpRangeType
    {
        /// <summary>
        /// DO NOT USE, NOT VALID
        /// </summary>
        None = 0,
        /// <summary>
        /// A range of bytes from the start of the resource
        /// </summary>
        FromStart = 1,
        /// <summary>
        /// A range of bytes from the end of the resource
        /// </summary>
        FromEnd = 2,
        /// <summary>
        /// A full range of bytes from the start to the end of the resource
        /// </summary>
        FullRange = 3
    }
}