﻿/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: INonce.cs 
*
* INonce.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// Represents a object that performs storage and computation of nonce values
    /// </summary>
    public interface INonce
    {
        /// <summary>
        /// Generates a random nonce for the current instance and 
        /// returns a base32 encoded string.
        /// </summary>
        /// <param name="buffer">The buffer to write a copy of the nonce value to</param>
        void ComputeNonce(Span<byte> buffer);
        /// <summary>
        /// Compares the raw nonce bytes to the current nonce to determine 
        /// if the supplied nonce value is valid
        /// </summary>
        /// <param name="nonceBytes">The binary value of the nonce</param>
        /// <returns>True if the nonce values are equal, flase otherwise</returns>
        bool VerifyNonce(ReadOnlySpan<byte> nonceBytes);
    }
}
