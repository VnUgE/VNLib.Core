﻿/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ThreadLocalObjectStorage.cs 
*
* ThreadLocalObjectStorage.cs is part of VNLib.Utils which is part of the larger 
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
using System.Linq;
using System.Threading;

namespace VNLib.Utils.Memory.Caching
{
    /// <summary>
    /// Derrives from <see cref="ObjectRental{T}"/> to provide object rental syntax for <see cref="ThreadLocal{T}"/> 
    /// storage
    /// </summary>
    /// <typeparam name="T">The data type to store</typeparam>
    public class ThreadLocalObjectStorage<T> : ObjectRental<T> where T: class
    {
        
        /// <summary>
        /// The thread-local storage container for objects of type T
        /// </summary>
        protected ThreadLocal<T> Store { get; }

        internal ThreadLocalObjectStorage(Func<T> constructor, Action<T>? rentCb, Func<T, bool>? returnCb)
            :base(constructor, rentCb, returnCb, 0)
        {
            Store = new(Constructor);
        }

        /// <summary>
        /// "Rents" or creates an object for the current thread
        /// </summary>
        /// <returns>The new or stored instanced</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public override T Rent()
        {
            Check();
            //Get the tlocal value or init if null (and assign to thread-local slot)
            T value = Store.Value!;
            //Invoke the rent action if set
            RentAction?.Invoke(value);
            return value;
        }

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public override void Return(T item)
        {
            Check();

            //Invoke the rent action
            if(ReturnAction != null && ReturnAction.Invoke(item) == false)
            {
                //Assign a new value to the thread-local slot and clean-up the old one
                Store.Value = Constructor();

                DisposeIfDisposeable(item);
            }
        }

        ///<inheritdoc/>
        public override T[] GetItems() => Store.Values.ToArray();

        ///<inheritdoc/>
        protected override void Free()
        {
            Store.Dispose();
            base.Free();
        }
    }
}