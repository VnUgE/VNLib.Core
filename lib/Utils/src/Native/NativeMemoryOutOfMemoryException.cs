﻿/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: NativeMemoryOutOfMemoryException.cs 
*
* NativeMemoryOutOfMemoryException.cs is part of VNLib.Utils which is 
* part of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Utils.Native
{
    /// <summary>
    /// Raised when a memory allocation or resize failed because there is 
    /// no more memory available
    /// </summary>
    public class NativeMemoryOutOfMemoryException : OutOfMemoryException
    {
        public NativeMemoryOutOfMemoryException(string message) : base(message)
        { }

        public NativeMemoryOutOfMemoryException(string message, Exception innerException) : base(message, innerException)
        { }

        public NativeMemoryOutOfMemoryException()
        { }

        /// <summary>
        /// Throws an <see cref="NativeMemoryOutOfMemoryException"/> if the pointer is null
        /// </summary>
        /// <param name="value">The pointer value to test</param>
        /// <param name="message">The message to use if the pointer is null</param>
        /// <exception cref="NativeMemoryOutOfMemoryException"></exception>
        public static void ThrowIfNullPointer(nint value, string? message = null)
        {
            if (value == 0)
            {
                throw new NativeMemoryOutOfMemoryException(message ?? "Failed to allocate or reallocte memory region");
            }
        }
    }
}
