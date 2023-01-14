/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ISecretProvider.cs 
*
* ISecretProvider.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using VNLib.Utils;

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// Provides a password hashing secret aka pepper.
    /// </summary>
    public interface ISecretProvider
    {
        /// <summary>
        /// The size of the buffer to use when retrieving the secret
        /// </summary>
        int BufferSize { get; }

        /// <summary>
        /// Writes the secret to the buffer and returns the number of bytes
        /// written to the buffer
        /// </summary>
        /// <param name="buffer">The buffer to write the secret data to</param>
        /// <returns>The number of secret bytes written to the buffer</returns>
        ERRNO GetSecret(Span<byte> buffer);
    }
}