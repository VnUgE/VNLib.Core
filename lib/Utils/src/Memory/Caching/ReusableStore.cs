/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ReusableStore.cs 
*
* ReusableStore.cs is part of VNLib.Utils which is part of the larger 
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

namespace VNLib.Utils.Memory.Caching
{

    /// <summary>
    /// A reusable object store that extends <see cref="ObjectRental{T}"/>, that allows for objects to be reused heavily
    /// </summary>
    /// <typeparam name="T">A reusable object</typeparam>
    public class ReusableStore<T> : ObjectRental<T> where T : class, IReusable
    {
        internal ReusableStore(Func<T> constructor, int quota) :base(constructor, null, null, quota)
        {}
        ///<inheritdoc/>
        public override T Rent()
        {
            //Rent the object (or create it)
            T rental = base.Rent();
            //Invoke prepare function
            rental.Prepare();
            //return object
            return rental;
        }
        ///<inheritdoc/>
        public override void Return(T item)
        {
            /*
             * Clean up the item by invoking the cleanup function, 
             * and only return the item for reuse if the caller allows
             */
            if (item.Release())
            {
                base.Return(item);
            }
        }
    }
}