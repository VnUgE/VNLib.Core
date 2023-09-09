/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IAsyncResourceStateHandler.cs 
*
* IAsyncResourceStateHandler.cs is part of VNLib.Utils which is part of 
* the larger VNLib collection of libraries and utilities.
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

using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// Represents a resource update handler that processes updates asynchronously
    /// when requested by a resource holder
    /// </summary>
    public interface IAsyncResourceStateHandler
    {
        /// <summary>
        /// Updates the resource in it's backing store
        /// </summary>
        /// <param name="resource">The instance of the handler that is requesting the update</param>
        /// <param name="state">The wrapped resource to update</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that completes when the resource data has successfully been updated</returns>
        Task UpdateAsync(AsyncUpdatableResource resource, object state, CancellationToken cancellation);

        /// <summary>
        /// Deletes the resource from it's backing store
        /// </summary>
        /// <param name="resource">The instance of the source data to delete</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that completes when the resource has been deleted</returns>
        Task DeleteAsync(AsyncUpdatableResource resource, CancellationToken cancellation);
    }
}
