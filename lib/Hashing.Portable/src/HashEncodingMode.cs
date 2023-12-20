/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: HashEncodingMode.cs 
*
* HashEncodingMode.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
    /// The binary hash encoding type
    /// </summary>
    [Flags]
    public enum HashEncodingMode
    {
        /// <summary>
        /// Specifies the Base64 character encoding
        /// </summary>
        Base64 = 64,
        /// <summary>
        /// Specifies the hexadecimal character encoding
        /// </summary>
        Hexadecimal = 16,
        /// <summary>
        /// Specifies the Base32 character encoding
        /// </summary>
        Base32 = 32,
        /// <summary>
        /// Specifies the base64 URL safe character encoding
        /// </summary>
        Base64Url = 128
    }
}
