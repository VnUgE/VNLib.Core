/*
* Copyright (c) 2026 Vaughn Nugent
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

using VNLib.Utils;
using VNLib.Utils.Native;
using VNLib.Utils.Extensions;
using VNLib.Utils.Resources;

namespace VNLib.Hashing
{
    /// <summary>
    /// Represents a handle to a <see cref="SafeLibraryHandle"/>'s 
    /// native method for hashing data with Argon2
    /// </summary>
    public class SafeArgon2Library : VnDisposeable, IArgon2Library
    {
        /*
        * The native library method delegate type
        */
        [SafeMethodName("argon2id_ctx")]
        delegate int Argon2InvokeHash(IntPtr context);

        private readonly Owned<SafeLibraryHandle> _lib;
        private readonly Argon2InvokeHash _invokeFn;

        /// <summary>
        /// The safe library handle to the native library
        /// </summary>
        public SafeLibraryHandle LibHandle => _lib.Value;

        /// <summary>
        /// Creates a new <see cref="SafeArgon2Library"/> wrapper around the 
        /// supplied native library handle and attempts to load the function 
        /// table. 
        /// </summary>
        /// <param name="lib"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="EntryPointNotFoundException">If the required functions are not exposed</exception>
        public SafeArgon2Library(Owned<SafeLibraryHandle> lib)
        {
            _lib = lib;

            //Get the native method
            _invokeFn = lib.Value.DangerousGetFunction<Argon2InvokeHash>();

            // Increment handle count. Can be TOCOU from loading function and now
            // that's okay because raising an exception unwinds any dependencies

            bool addRef = false;
            lib.Value.DangerousAddRef(ref addRef);
            if (!addRef)
            {
                throw new ArgumentException("Failed to increment library handle count");
            }            
        }

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public int Argon2Hash(IntPtr context)
        {
            Check();
            return _invokeFn.Invoke(context);
        }

        protected override void Free()
        {
            _lib.Value.DangerousRelease();
            _lib.Dispose();
        }
    }
}
