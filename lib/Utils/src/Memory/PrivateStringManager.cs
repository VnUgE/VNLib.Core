/*
* Copyright (c) 2025 Vaughn Nugent
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
    /// <remarks>
    /// Create a new instance with fixed array size
    /// </remarks>
    /// <param name="elements">Number of elements to protect</param>
    public class PrivateStringManager(int elements) : VnDisposeable
    {
        private readonly StringRef[] ProtectedElements = new StringRef[elements];

        /// <summary>
        /// Gets or sets a string reference into the protected elements store
        /// </summary>
        /// <param name="index">The table index to store the string</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <returns>Reference to string associated with the index</returns>
        protected string? this[int index]
        {
            get
            {
                Check();
                return ProtectedElements[index].Value;
            }
            set
            {
                Check();
                SetValue(index, value);
            }
        }

        private void SetValue(int index, string? value)
        {
            //Try to get the old reference and erase it
            ref StringRef strRef = ref ProtectedElements[index];
            strRef.Erase();

            //Assign new string reference
            strRef = StringRef.Create(value);
        }

        /// <summary>
        /// Gets a copy of the string at the specified index. The 
        /// value returned is safe from erasure and is an independent
        /// string
        /// </summary>
        /// <param name="index">The index to get the copy of the string at</param>
        /// <returns>The copied string instance</returns>
        protected string? CopyStringAtIndex(int index)
        {
            Check();
            ref readonly StringRef str = ref ProtectedElements[index];
            
            if(str.Value is null)
            {
                //Pass null
                return null;
            }
            else if (str.IsInterned)
            {
                /*
                 * If string is interned, it is safe to return the
                 * string reference as it will not be erased
                 */
                return str.Value;
            }
            else
            {
                //Copy to new clr string
                return str.Value.AsSpan().ToString();
            }
        }

        ///<inheritdoc/>
        protected override void Free() 
            => Array.ForEach(ProtectedElements, static p => p.Erase());

        /// <summary>
        /// Erases the contents of the supplied string if it
        /// is safe to do so. If the string is interned, it will
        /// not be erased, nor will a null string
        /// </summary>
        /// <param name="str">The reference to the string to zero</param>
        public static void EraseString(string? str) 
            => StringRef.Create(str).Erase();

        private readonly struct StringRef(string? value, bool isInterned)
        {
            public readonly string? Value = value;
            public readonly bool IsInterned = isInterned;

            public readonly void Erase()
            {
                /*
                 * Only erase if the string is not interned
                 * and is not null
                 */
                if (Value is not null && !IsInterned)
                {
                    MemoryUtil.UnsafeZeroMemory<char>(Value);
                }
            }

            internal static StringRef Create(string? str) => str is null 
                ? new(value: null, isInterned: false)
                : new(str, string.IsInterned(str) != null);
        }
    }
}
