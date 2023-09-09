/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: SafeArgon2Library.cs 
*
* SafeArgon2Library.cs is part of VNLib.Hashing.Portable which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Hashing.Portable is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Hashing.Portable is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Hashing.Portable. If not, see http://www.gnu.org/licenses/.
*/

using System;

using VNLib.Utils.Native;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing
{
    /// <summary>
    /// Represents a handle to a <see cref="SafeLibraryHandle"/>'s 
    /// native method for hashing data with Argon2
    /// </summary>
    public class SafeArgon2Library : IArgon2Library, IDisposable
    {
        /*
        * The native library method delegate type
        */
        [SafeMethodName("argon2id_ctx")]
        delegate int Argon2InvokeHash(IntPtr context);

        private readonly SafeMethodHandle<Argon2InvokeHash> methodHandle;

        /// <summary>
        /// The safe library handle to the native library
        /// </summary>
        public SafeLibraryHandle LibHandle { get; }

        internal SafeArgon2Library(SafeLibraryHandle lib)
        {
            LibHandle = lib;
            //Get the native method
            methodHandle = lib.GetMethod<Argon2InvokeHash>();
        }

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public int Argon2Hash(IntPtr context)
        {
            LibHandle.ThrowIfClosed();
            return methodHandle.Method!.Invoke(context);
        }

        /// <summary>
        /// Disposes the library handle and method handle
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ///<inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                methodHandle.Dispose();
                LibHandle.Dispose();
            }           
        }
    }
}