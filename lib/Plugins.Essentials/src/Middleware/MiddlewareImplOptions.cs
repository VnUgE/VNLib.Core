/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: MiddlewareImplOptions.cs 
*
* MiddlewareImplOptions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;


namespace VNLib.Plugins.Essentials.Middleware
{
    /// <summary>
    /// Implementation flags for midleware implementations
    /// </summary>
    [Flags]
    public enum MiddlewareImplOptions
    {
        /// <summary>
        /// No flags
        /// </summary>
        None = 0x00,
        /// <summary>
        /// Prioritizes a middleware instance in the chain because
        /// it is required for security purposes
        /// </summary>
        SecurityCritical = 0x01
    }
}
