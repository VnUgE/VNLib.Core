/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ExternalResourceBase.cs 
*
* ExternalResourceBase.cs is part of VNLib.Utils which is part of the larger 
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
    /// A base class for a resource that is backed by an external data store. 
    /// Implements the <see cref="IResource"/> interfaceS
    /// </summary>
    public abstract class ExternalResourceBase : IResource
    {
        const int IsReleasedFlag = 1 << 0;
        const int IsDeletedFlag = 1 << 2;
        const int IsModifiedFlag = 1 << 3;

        private uint _flags;

        ///<inheritdoc/>
        public bool IsReleased
        {
            get => (_flags & IsReleasedFlag) > 0;
            protected set => _flags |= IsReleasedFlag;
        }

        /// <summary>
        /// A value indicating whether the instance should be deleted when released
        /// </summary>
        protected bool Deleted
        {
            get => (_flags & IsDeletedFlag) > 0;
            set => _flags |= IsDeletedFlag;
        }

        /// <summary>
        /// Marks the resource for deletion from backing store during closing events
        /// </summary>
        public virtual void Delete() => _flags |= IsDeletedFlag;

        /// <summary>
        /// A value indicating whether the instance should be updated when released
        /// </summary>
        protected bool Modified
        {
            get => (_flags & IsModifiedFlag) > 0;
            set => _flags |= IsModifiedFlag;
        }    
    }
}