﻿/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: SafeMethodHandle.cs 
*
* SafeMethodHandle.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.Resources;

namespace VNLib.Utils.Native
{
    /// <summary>
    /// Represents a handle to a <see cref="SafeLibraryHandle"/>'s 
    /// native method
    /// </summary>
    /// <typeparam name="T">The native method deelgate type</typeparam>
    public class SafeMethodHandle<T> : OpenHandle where T : Delegate
    {
        private T? _method;
        private readonly SafeLibraryHandle Library;

        internal SafeMethodHandle(SafeLibraryHandle lib, T method)
        {
            Library = lib;
            _method = method;
        }

        /// <summary>
        /// A delegate to the native method
        /// </summary>
        public T? Method => _method;

        ///<inheritdoc/>
        protected override void Free()
        {
            //Release the method 
            _method = default;
            //Decrement lib handle count
            Library.DangerousRelease();
        }

        /// <summary>
        /// Releases the library handle on finalization
        /// </summary>
#pragma warning disable CA1063 // Implement IDisposable Correctly
        ~SafeMethodHandle()
        {
            //Make sure the library is released on finalization
            Library.DangerousRelease();
        }
#pragma warning restore CA1063 // Implement IDisposable Correctly

    }
}
