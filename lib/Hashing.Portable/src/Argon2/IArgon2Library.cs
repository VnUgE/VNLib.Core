/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: IArgon2Library.cs 
*
* IArgon2Library.cs is part of VNLib.Hashing.Portable which is part of the larger 
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


namespace VNLib.Hashing
{
    /// <summary>
    /// Represents a native Argon2 library that can be used to hash an argon2 context.
    /// </summary>
    public interface IArgon2Library
    {
        /// <summary>
        /// Hashes the data in the <paramref name="context"/> and returns the result
        /// of the operation as an Argon2 error code.
        /// </summary>
        /// <param name="context">A pointer to a valid argon2 hash context</param>
        /// <returns>The argon2 status code result</returns>
        int Argon2Hash(IntPtr context);
    }
}