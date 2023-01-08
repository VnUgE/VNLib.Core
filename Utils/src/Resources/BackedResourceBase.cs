/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: BackedResourceBase.cs 
*
* BackedResourceBase.cs is part of VNLib.Utils which is part of the larger 
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
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace VNLib.Utils.Resources
{
    /// <summary>
    /// A base class for a resource that is backed by an external data store. 
    /// Implements the <see cref="IResource"/> interfaceS
    /// </summary>
    public abstract class BackedResourceBase : IResource
    {
        ///<inheritdoc/>
        public bool IsReleased { get; protected set; }

        /// <summary>
        /// Optional <see cref="JsonSerializerOptions"/> to be used when serializing
        /// the resource
        /// </summary>
        internal protected virtual JsonSerializerOptions? JSO { get; }

        /// <summary>
        /// A value indicating whether the instance should be deleted when released
        /// </summary>
        protected bool Deleted { get; set; }
        /// <summary>
        /// A value indicating whether the instance should be updated when released
        /// </summary>
        protected bool Modified { get; set; }

        /// <summary>
        /// Checks if the resouce has been disposed and raises an exception if it is
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Check()
        {
            if (IsReleased)
            {
                throw new ObjectDisposedException("The resource has been disposed");
            }
        }

        /// <summary>
        /// Returns the JSON serializable resource to be updated during an update
        /// </summary>
        /// <returns>The resource to update</returns>
        protected abstract object GetResource();

        /// <summary>
        /// Marks the resource for deletion from backing store during closing events
        /// </summary>
        public virtual void Delete() => Deleted = true;
    }
}