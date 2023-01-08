/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: VnArgon2Exception.cs 
*
* VnArgon2Exception.cs is part of VNLib.Hashing.Portable which is part of the larger 
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

/*
 *  VnArgon2.cs
 *  Author: Vaughhn Nugent
 *  Date: July 17, 2021
 *  
 *  Dependencies Argon2.
 *  https://github.com/P-H-C/phc-winner-argon2
 *  
 */

using System;

namespace VNLib.Hashing
{
    /// <summary>
    /// Argon2 operational exception
    /// </summary>
    public class VnArgon2Exception : Exception
    {
        /// <summary>
        /// Argon2 error code that caused this exception
        /// </summary>
        public Argon2_ErrorCodes Errno { get; }
        public VnArgon2Exception(string message, Argon2_ErrorCodes errno) : base(message)
        {
            Errno = errno;
        }
        public override string Message => $"Argon 2 lib error, code {(int)Errno}, name {Enum.GetName(Errno)}, messsage {base.Message}";
    }
}