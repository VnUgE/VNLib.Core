/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IPasswordHashingProvider.cs 
*
* IPasswordHashingProvider.cs is part of VNLib.Plugins.Essentials which 
* is part of the larger VNLib collection of libraries and utilities.
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
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// Represents a common abstraction for password hashing providers/libraries
    /// </summary>
    public interface IPasswordHashingProvider
    {
        /// <summary>
        /// Verifies a password against its previously encoded hash.
        /// </summary>
        /// <param name="passHash">Previously hashed password</param>
        /// <param name="password">Raw password to compare against</param>
        /// <returns>true if bytes derrived from password match the hash, false otherwise</returns>
        /// <exception cref="NotSupportedException"></exception>
        bool Verify(ReadOnlySpan<char> passHash, ReadOnlySpan<char> password);

        /// <summary>
        /// Verifies a password against its previously encoded hash.
        /// </summary>
        /// <param name="passHash">Previously hashed password in binary</param>
        /// <param name="password">Raw password to compare against the hash</param>
        /// <returns>true if bytes derrived from password match the hash, false otherwise</returns>
        /// <exception cref="NotSupportedException"></exception>
        bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password);

        /// <summary>
        /// Hashes the specified character encoded password to it's secured hashed form.
        /// </summary>
        /// <param name="password">The character encoded password to encrypt</param>
        /// <returns>A <see cref="PrivateString"/> containing the new password hash.</returns>
        /// <exception cref="NotSupportedException"></exception>
        PrivateString Hash(ReadOnlySpan<char> password);

        /// <summary>
        /// Hashes the specified binary encoded password to it's secured hashed form.
        /// </summary>
        /// <param name="password">The binary encoded password to encrypt</param>
        /// <returns>A <see cref="PrivateString"/> containing the new password hash.</returns>
        /// <exception cref="NotSupportedException"></exception>
        PrivateString Hash(ReadOnlySpan<byte> password);

        /// <summary>
        /// Exposes a lower level for producing a password hash and writing it to the output buffer
        /// </summary>
        /// <param name="password">The raw password to encrypt</param>
        /// <param name="hashOutput">The output buffer to write encoded data into</param>
        /// <returns>The number of bytes written to the hash buffer, or 0/false if the hashing operation failed</returns>
        /// <exception cref="NotSupportedException"></exception>
        ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput);
    }
}