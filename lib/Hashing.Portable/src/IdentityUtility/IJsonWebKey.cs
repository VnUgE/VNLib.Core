/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: IJsonWebKey.cs 
*
* IJsonWebKey.cs is part of VNLib.Hashing.Portable which is part 
* of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Hashing.IdentityUtility
{
    /// <summary>
    /// An abstraction for basic JsonWebKey operations
    /// </summary>
    public interface IJsonWebKey
    {
        /// <summary>
        /// The key usage, may be Siganture, or Encryption
        /// </summary>
        JwkKeyUsage KeyUse { get; }

        /// <summary>
        /// The cryptographic algorithm this key is to be used for
        /// </summary>
        string Algorithm { get; }

        /// <summary>
        /// Gets miscelaneous key properties on demand. May return null results if the property 
        /// is not defined in the current key
        /// </summary>
        /// <param name="propertyName">The name of the key property to get</param>
        /// <returns>The value at the key property</returns>
        string? GetKeyProperty(string propertyName);
    }
}
