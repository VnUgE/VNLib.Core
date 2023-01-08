/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: IVirtualEndpoint.cs 
*
* IVirtualEndpoint.cs is part of VNLib.Plugins which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins. If not, see http://www.gnu.org/licenses/.
*/

using System.Threading.Tasks;

namespace VNLib.Plugins
{

    /// <summary>
    /// Represents a virtual page which provides processing on an entity
    /// </summary>
    /// <typeparam name="TEntity">The entity type to process</typeparam>
    public interface IVirtualEndpoint<TEntity> : IEndpoint
    {
        /// <summary>
        /// The handler method for processing the specified location.
        /// </summary>
        /// <param name="entity">The current connection/session </param>
        /// <returns>A <see cref="VfReturnType"/> specifying how the caller should continue processing the request</returns>
        public ValueTask<VfReturnType> Process(TEntity entity);
    }
}