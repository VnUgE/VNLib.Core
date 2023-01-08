/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IObjectStorage.cs 
*
* IObjectStorage.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils
{
    /// <summary>
    /// This object will provide methods for storing and retreiving objects by key-value pairing
    /// </summary>
    public interface IObjectStorage
    {
        /// <summary>
        /// Attempts to retrieve the specified object from storage
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">Key for storage</param>
        /// <returns>The object in storage, or T.default if object is not found</returns>
        public T GetObject<T>(string key);

        /// <summary>
        /// Stores the specified object with the specified key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">Key paired with object</param>
        /// <param name="obj">Object to store</param>
        public void SetObject<T>(string key, T obj);
    }
}