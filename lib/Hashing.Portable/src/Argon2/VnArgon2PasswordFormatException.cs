﻿/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: VnArgon2PasswordFormatException.cs 
*
* VnArgon2PasswordFormatException.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
    /// Raised if a verify operation determined the supplied password hash is not in a valid format for this library
    /// </summary>
    public class VnArgon2PasswordFormatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the VnArgon2PasswordFormatException class with a specified error message
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        public VnArgon2PasswordFormatException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the VnArgon2PasswordFormatException class with a specified error message and inner exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public VnArgon2PasswordFormatException(string message, Exception innerException) : base(message, innerException)
        { }
    }
}