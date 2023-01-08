/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IIndexable.cs 
*
* IIndexable.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils
{
    /// <summary>
    /// Provides an interface that provides an indexer
    /// </summary>
    /// <typeparam name="TKey">The lookup Key</typeparam>
    /// <typeparam name="TValue">The lookup value</typeparam>
    public interface IIndexable<TKey, TValue>
    {
        /// <summary>
        /// Gets or sets the value at the specified index in the collection
        /// </summary>
        /// <param name="key">The key to lookup the value at</param>
        /// <returns>The value at the specified key</returns>
        TValue this[TKey key] { get; set;}
    }
}
