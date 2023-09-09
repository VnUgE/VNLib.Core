/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IResourceStateHandler.cs 
*
* IResourceStateHandler.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Resources
{
    /// <summary>
    /// Implemented by a resource that is backed by an external data store, that when modified or deleted will
    /// be reflected to the backing store.
    /// </summary>
    public interface IResourceStateHandler
    {
        /// <summary>
        /// Called when a resource update has been requested
        /// </summary>
        /// <param name="resource">The <see cref="UpdatableResource"/> to be updated</param>
        /// <param name="data">The wrapped state data to update</param>
        void Update(UpdatableResource resource, object data);

        /// <summary>
        /// Called when a resource delete has been requested
        /// </summary>
        /// <param name="resource">The <see cref="UpdatableResource"/> to be deleted</param>
        /// <exception cref="ResourceDeleteFailedException"></exception>
        void Delete(UpdatableResource resource);
    }
}