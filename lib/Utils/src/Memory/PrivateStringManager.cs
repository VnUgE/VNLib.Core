/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: PrivateStringManager.cs 
*
* PrivateStringManager.cs is part of VNLib.Utils which is part of the larger 
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


namespace VNLib.Utils.Memory
{
    /// <summary>
    /// When inherited by a class, provides a safe string storage that zeros a CLR string memory on disposal
    /// </summary>
    public class PrivateStringManager : VnDisposeable, ICloneable
    {
        /// <summary>
        /// Strings to be cleared when exiting
        /// </summary>
        private readonly string?[] ProtectedElements;
        /// <summary>
        /// Gets or sets a string referrence into the protected elements store
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <returns>Referrence to string associated with the index</returns>
        protected string? this[int index]
        {
            get
            {
                Check();
                return ProtectedElements[index];
            }
            set
            {
                Check();
                //Check to see if the string has been interned
                if (!string.IsNullOrEmpty(value) && string.IsInterned(value) != null)
                {
                    throw new ArgumentException($"The specified string has been CLR interned and cannot be stored in {nameof(PrivateStringManager)}");
                }
                //Clear the old value before setting the new one
                if (!string.IsNullOrEmpty(ProtectedElements[index]))
                {
                    Memory.UnsafeZeroMemory<char>(ProtectedElements[index]);
                }
                //set new value
                ProtectedElements[index] = value;
            }
        }
        /// <summary>
        /// Create a new instance with fixed array size
        /// </summary>
        /// <param name="elements">Number of elements to protect</param>
        public PrivateStringManager(int elements)
        {
            //Allocate the string array
            ProtectedElements = new string[elements];
        }
        ///<inheritdoc/>
        protected override void Free()
        {
            //Zero all strings specified
            for (int i = 0; i < ProtectedElements.Length; i++)
            {
                if (!string.IsNullOrEmpty(ProtectedElements[i]))
                {
                    //Zero the string memory
                    Memory.UnsafeZeroMemory<char>(ProtectedElements[i]);
                    //Set to null
                    ProtectedElements[i] = null;
                }
            }
        }

        /// <summary>
        /// Creates a deep copy for a new independent <see cref="PrivateStringManager"/> 
        /// </summary>
        /// <returns>A new independent <see cref="PrivateStringManager"/> instance</returns>
        /// <remarks>Be careful duplicating large instances, and make sure clones are properly disposed if necessary</remarks>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual object Clone()
        {
            Check();
            PrivateStringManager other = new (ProtectedElements.Length);
            //Copy all strings to the other instance
            for(int i = 0; i < ProtectedElements.Length; i++)
            {
                //Copy all strings and store their copies in the new array
                other.ProtectedElements[i] = this.ProtectedElements[i].AsSpan().ToString();
            }
            //return the new copy
            return other;
        }
    }
}
