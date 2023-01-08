/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ICacheHolder.cs 
*
* ICacheHolder.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory.Caching
{
    /// <summary>
    /// Exposes basic control of classes that manage private caches
    /// </summary>
    public interface ICacheHolder
    {
        /// <summary>
        /// Clears all held caches without causing application stopping effects. 
        /// </summary>
        /// <remarks>This is a safe "light" cache clear</remarks>
        void CacheClear();
        /// <summary>
        /// Performs all necessary actions to clear all held caches immediatly.
        /// </summary>
        /// <remarks>A "hard" cache clear/reset regardless of cost</remarks>
        void CacheHardClear();
    }
}