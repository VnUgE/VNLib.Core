/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: IHmacStream.cs 
*
* IHmacStream.cs is part of VNLib.Hashing.Portable which is part of the larger 
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

namespace VNLib.Hashing.Native.MonoCypher
{
    /// <summary>
    /// A base interface for a streaming (incremental) HMAC
    /// authenticated hash function
    /// </summary>
    public interface IHmacStream: IHashStream
    {
        /// <summary>
        /// The maximum key size allowed for this stream
        /// </summary>
        int MaxKeySize { get; }

        /// <summary>
        /// Initializes this stream with the specified key
        /// </summary>
        /// <param name="key">A reference to the first byte of key data to import</param>
        /// <param name="keySize">The size of the key buffer</param>
        /// <exception cref="System.ArgumentException"></exception>
        void Initialize(ref readonly byte key, byte keySize);
    }
}