/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: IAssemblyLoader.cs 
*
* IAssemblyLoader.cs is part of VNLib.Plugins.Runtime which is 
* part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Reflection;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Represents the core assembly loading functionality
    /// </summary>
    public interface IAssemblyLoader : IDisposable
    {
        /// <summary>
        /// Unloads the assembly loader if Config.Unloadable is true, otherwise does nothing
        /// </summary>
        void Unload();

        /// <summary>
        /// Prepares the loader for use
        /// </summary>
        void Load();

        /// <summary>
        /// Begins the loading process and recovers the default assembly
        /// </summary>
        /// <returns>The main assembly from the assembly file</returns>
        Assembly GetAssembly();
    }
}
