/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: CacheType.cs
*
* CacheType.cs is part of VNLib.Net.Http which is part of the larger 
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
    /// HTTP response entity cache flags
    /// </summary>
    [Flags]
    public enum CacheType
    {
        /// <summary>
        /// Default cache type
        /// </summary>
        None = 0x00,
        /// <summary>
        /// Adds 'no-cache' to Cache-Control header
        /// </summary>
        NoCache = 0x01, 
        /// <summary>
        /// Adds 'private' to Cache-Control header
        /// </summary>
        Private = 0x02,
        /// <summary>
        /// Adds 'public' to Cache-Control header
        /// </summary>
        Public = 0x04,
        /// <summary>
        /// Adds 'no-store' to Cache-Control header
        /// </summary>
        NoStore = 0x08,
        /// <summary>
        /// Sets the must-revalidate cache flag
        /// </summary>
        Revalidate = 0x10
    }
}
