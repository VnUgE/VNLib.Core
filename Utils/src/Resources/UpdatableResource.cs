/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: UpdatableResource.cs 
*
* UpdatableResource.cs is part of VNLib.Utils which is part of the larger 
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

using VNLib.Utils.IO;

namespace VNLib.Utils.Resources
{
    /// <summary>
    /// A callback delegate used for updating a <see cref="UpdatableResource"/>
    /// </summary>
    /// <param name="source">The <see cref="UpdatableResource"/> to be updated</param>
    /// <param name="data">The serialized data to be stored/updated</param>
    /// <exception cref="ResourceUpdateFailedException"></exception>
    public delegate void UpdateCallback(object source, Stream data);
    /// <summary>
    /// A callback delegate invoked when a <see cref="UpdatableResource"/> delete is requested
    /// </summary>
    /// <param name="source">The <see cref="UpdatableResource"/> to be deleted</param>
    /// <exception cref="ResourceDeleteFailedException"></exception>
    public delegate void DeleteCallback(object source);

    /// <summary>
    /// Implemented by a resource that is backed by an external data store, that when modified or deleted will 
    /// be reflected to the backing store.
    /// </summary>
    public abstract class UpdatableResource : BackedResourceBase, IExclusiveResource
    {
        /// <summary>
        /// The update callback method to invoke during a release operation
        /// when the resource is updated.
        /// </summary>
        protected abstract UpdateCallback UpdateCb { get; }
        /// <summary>
        /// The callback method to invoke during a realease operation
        /// when the resource should be deleted
        /// </summary>
        protected abstract DeleteCallback DeleteCb { get; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ResourceDeleteFailedException"></exception>
        /// <exception cref="ResourceUpdateFailedException"></exception>
        public virtual void Release()
        {
            //If resource has already been realeased, return
            if (IsReleased)
            {
                return;
            }
            //If deleted flag is set, invoke the delete callback
            if (Deleted)
            {
                DeleteCb(this);
            }
            //If the state has been modifed, flush changes to the store
            else if (Modified)
            {
                FlushPendingChanges();
            }
            //Set the released value
            IsReleased = true;
        }

        /// <summary>
        /// Writes the current state of the the resource to the backing store
        /// immediatly by invoking the specified callback. 
        /// <br></br>
        /// <br></br>
        /// Only call this method if your store supports multiple state updates
        /// </summary>
        protected virtual void FlushPendingChanges()
        {
            //Get the resource
            object resource = GetResource();
            //Open a memory stream to store data in
            using VnMemoryStream data = new();
            //Serialize and write to stream
            VnEncoding.JSONSerializeToBinary(resource, data, resource.GetType(), JSO);
            //Reset stream to begining
            _ = data.Seek(0, SeekOrigin.Begin);
            //Invoke update callback
            UpdateCb(this, data);
            //Clear modified flag
            Modified = false;
        }
    }
}