/*
* Copyright (c) 2023 Vaughn Nugent
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

namespace VNLib.Utils.Resources
{

    /// <summary>
    /// Implemented by a resource that is backed by an external data store, that when modified or deleted will 
    /// be reflected to the backing store.
    /// </summary>
    public abstract class UpdatableResource : BackedResourceBase, IExclusiveResource
    {
        /// <summary>
        /// Gets the <see cref="IResourceStateHandler"/> that will be invoked when the resource is released
        /// </summary>
        protected abstract IResourceStateHandler Handler { get; }

        /// <inheritdoc/>
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
                Handler.Delete(this);
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
        /// <para>
        /// Writes the current state of the the resource to the backing store
        /// immediatly by invoking the specified callback. 
        /// </para>
        /// <para>
        /// Only call this method if your store supports multiple state updates
        /// </para>
        /// </summary>
        protected virtual void FlushPendingChanges()
        {
            //Get the resource
            object resource = GetResource();
            //Invoke update callback
            Handler.Update(this, resource);
            //Clear modified flag
            Modified = false;
        }
    }
}