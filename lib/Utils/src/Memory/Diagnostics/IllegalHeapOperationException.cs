/*
* Copyright (c) 2022 Vaughn Nugent
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
        public IllegalHeapOperationException(string message) : base(message)
        {}

        public IllegalHeapOperationException(string message, Exception innerException) : base(message, innerException)
        {}

        public IllegalHeapOperationException()
        {}
    }
}
