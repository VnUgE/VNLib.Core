/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IllegalHeapOperationException.cs 
*
* IllegalHeapOperationException.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory.Diagnostics
{
    /// <summary>
    /// An exception that represents an illegal memory operation during heap monitoring
    /// </summary>
    public class IllegalHeapOperationException : HeapDiagnosticException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IllegalHeapOperationException"/> class with a specified error message
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        public IllegalHeapOperationException(string message) : base(message)
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="IllegalHeapOperationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified</param>
        public IllegalHeapOperationException(string message, Exception innerException) : base(message, innerException)
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="IllegalHeapOperationException"/> class
        /// </summary>
        public IllegalHeapOperationException()
        {}
    }
}
