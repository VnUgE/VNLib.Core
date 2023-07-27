/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Compression
* File: NativeCompressionException.cs 
*
* NativeCompressionException.cs is part of VNLib.Net.Compression which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Compression is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Net.Compression is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Net.Compression. If not, see http://www.gnu.org/licenses/.
*/

using System;

using VNLib.Utils.Native;

namespace VNLib.Net.Compression
{
    internal sealed class NativeCompressionException : NativeLibraryException
    {
        public NativeCompressionException()
        { }

        public NativeCompressionException(string message) : base(message)
        { }

        public NativeCompressionException(string message, Exception innerException) : base(message, innerException)
        { }
    }
}