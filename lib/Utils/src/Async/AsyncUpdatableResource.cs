/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: AsyncUpdatableResource.cs 
*
* AsyncUpdatableResource.cs is part of VNLib.Utils which is part of the larger 
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
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using VNLib.Utils.IO;
using VNLib.Utils.Resources;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// A callback delegate used for updating a <see cref="AsyncUpdatableResource"/>
    /// </summary>
    /// <param name="source">The <see cref="AsyncUpdatableResource"/> to be updated</param>
    /// <param name="data">The serialized data to be stored/updated</param>
    /// <exception cref="ResourceUpdateFailedException"></exception>
    public delegate Task AsyncUpdateCallback(object source, Stream data);
    /// <summary>
    /// A callback delegate invoked when a <see cref="AsyncUpdatableResource"/> delete is requested
    /// </summary>
    /// <param name="source">The <see cref="AsyncUpdatableResource"/> to be deleted</param>
    /// <exception cref="ResourceDeleteFailedException"></exception>
    public delegate Task AsyncDeleteCallback(object source);

    /// <summary>
    /// Implemented by a resource that is backed by an external data store, that when modified or deleted will 
    /// be reflected to the backing store.
    /// </summary>
    public abstract class AsyncUpdatableResource : BackedResourceBase, IAsyncExclusiveResource
    {
        protected abstract AsyncUpdateCallback UpdateCb { get; }
        protected abstract AsyncDeleteCallback DeleteCb { get; }

        /// <summary>
        /// Releases the resource and flushes pending changes to its backing store.
        /// </summary>
        /// <returns>A task that represents the async operation</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ResourceDeleteFailedException"></exception>
        /// <exception cref="ResourceUpdateFailedException"></exception>
        public virtual async ValueTask ReleaseAsync()
        {
            //If resource has already been realeased, return
            if (IsReleased)
            {
                return;
            }
            //If deleted flag is set, invoke the delete callback
            if (Deleted)
            {
                await DeleteCb(this).ConfigureAwait(true);
            }
            //If the state has been modifed, flush changes to the store
            else if (Modified)
            {
                await FlushPendingChangesAsync().ConfigureAwait(true);
            }
            //Set the released value
            IsReleased = true;
        }

        /// <summary>
        /// <para>
        /// Writes the current state of the the resource to the backing store
        /// immediatly by invoking the specified callback. 
        /// </para>
        /// <para>
        /// Only call this method if your store supports multiple state updates
        /// </para>
        /// </summary>
        protected virtual async Task FlushPendingChangesAsync()
        {
            //Get the resource
            object resource = GetResource();
            //Open a memory stream to store data in
            using VnMemoryStream data = new();
            //Serialize and write to stream
            VnEncoding.JSONSerializeToBinary(resource, data, resource.GetType(), base.JSO);
            //Reset stream to begining
            _ = data.Seek(0, SeekOrigin.Begin);
            //Invoke update callback
            await UpdateCb(this, data).ConfigureAwait(true);
            //Clear modified flag
            Modified = false;
        }
    }
}
