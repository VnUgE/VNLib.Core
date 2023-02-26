/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ObjectRental.cs 
*
* ObjectRental.cs is part of VNLib.Utils which is part of the larger 
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
using System.Diagnostics;
using System.Collections.Generic;

namespace VNLib.Utils.Memory.Caching
{
    //TODO: implement lock-free object tracking

    /// <summary>
    /// Provides concurrent storage for reusable objects to be rented and returned. This class
    /// and its members is thread-safe
    /// </summary>
    /// <typeparam name="T">The data type to reuse</typeparam>
    public class ObjectRental<T> : ObjectRental, IObjectRental<T>, ICacheHolder where T: class
    {
        /// <summary>
        /// The initial data-structure capacity if quota is not defined
        /// </summary>
        public const int INITIAL_STRUCTURE_SIZE = 50;

        protected readonly object StorageLock;
        protected readonly Stack<T> Storage;
        protected readonly HashSet<T> ContainsStore;

        protected readonly Action<T>? ReturnAction;
        protected readonly Action<T>? RentAction;
        protected readonly Func<T> Constructor;
        /// <summary>
        /// Is the object type in the current store implement the Idisposable interface?
        /// </summary>
        protected readonly bool IsDisposableType;

        /// <summary>
        /// The maximum number of objects that will be cached.
        /// Once this threshold has been reached, objects are 
        /// no longer stored
        /// </summary>
        protected readonly int QuotaLimit;

#pragma warning disable CS8618 //Internal constructor does not set the constructor function
        private ObjectRental(int quota)
#pragma warning restore CS8618 
        {
            //alloc new stack for rentals
            Storage = new(Math.Max(quota, INITIAL_STRUCTURE_SIZE));
            //Hashtable for quick lookups
            ContainsStore = new(Math.Max(quota, INITIAL_STRUCTURE_SIZE));
            //Semaphore slim to provide exclusive access
            StorageLock = new ();
            //Store quota, if quota is -1, set to int-max to "disable quota"
            QuotaLimit = quota == 0 ? int.MaxValue : quota;
            //Determine if the type is disposeable and store a local value
            IsDisposableType = typeof(IDisposable).IsAssignableFrom(typeof(T));
        }

        /// <summary>
        /// Creates a new <see cref="ObjectRental{T}"/> store with the rent/return callback methods
        /// </summary>
        /// <param name="constructor">The type initializer</param>
        /// <param name="rentCb">The pre-retnal preperation action</param>
        /// <param name="returnCb">The pre-return cleanup action</param>
        /// <param name="quota">The maximum number of elements to cache in the store</param>
        protected internal ObjectRental(Func<T> constructor, Action<T>? rentCb, Action<T>? returnCb, int quota) : this(quota)
        {
            this.RentAction = rentCb;
            this.ReturnAction = returnCb;
            this.Constructor = constructor;
        }

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual T Rent()
        {
            Check();
            //See if we have an available object, if not return a new one by invoking the constructor function
            T? rental = default;

            //Enter Lock
            lock (StorageLock)
            {
                //See if the store contains an item ready to use
                if (Storage.TryPop(out T? item))
                {
                    rental = item;
                    //Remove the item from the hash table
                    ContainsStore.Remove(item);
                }
            }

            //If no object was removed from the store, create a new one
            rental ??= Constructor();
            //If rental cb is defined, invoke it
            RentAction?.Invoke(rental);
            return rental;
        }

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual void Return(T item)
        {
            _ = item ?? throw new ArgumentNullException(nameof(item));

            Check();
            
            //Invoke return callback if set
            ReturnAction?.Invoke(item);

            //Keeps track to know if the element was added or need to be cleaned up
            bool wasAdded = false;

            lock (StorageLock)
            {
                //Check quota limit
                if (Storage.Count < QuotaLimit)
                {
                    //Store item if it doesnt exist already
                    if (ContainsStore.Add(item))
                    {
                        //Store the object
                        Storage.Push(item);
                    }
                    //Set the was added flag
                    wasAdded = true;
                }
            }

            if (!wasAdded && IsDisposableType)
            {
                //If the element was not added and is disposeable, we can dispose the element
                (item as IDisposable)!.Dispose();
                //Write debug message
                Debug.WriteLine("Object rental disposed an object over quota");
            }
        }

        /// <remarks>
        /// NOTE: If <typeparamref name="T"/> implements <see cref="IDisposable"/>
        /// interface, this method does nothing
        /// </remarks>
        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual void CacheClear()
        {
            Check();

            //If the type is disposeable, cleaning can be a long process, so defer to hard clear
            if (IsDisposableType)
            {
                return;
            }

            lock(StorageLock)
            {
                //Clear stores
                ContainsStore.Clear();
                Storage.Clear();
            }
        }

        /// <summary>
        /// Gets all the elements in the store as a "snapshot"
        /// while holding the lock
        /// </summary>
        /// <returns></returns>
        protected T[] GetElementsWithLock()
        {
            T[] result;

            lock (StorageLock)
            {
                //Enumerate all items to the array
                result = Storage.ToArray();
            }

            return result;
        }
     

        /// <inheritdoc/>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual void CacheHardClear()
        {
            Check();
           
            /*
             * If the type is disposable, we need to collect all the stored items
             * and dispose them individually. We need to spend as little time in
             * the lock as possbile (busywaiting...) so get the array and exit 
             * the lock after clearing. Then we can dispose the elements.
             * 
             * If the type is not disposable, we don't need to get the items 
             * and we can just call CacheClear()
             */

            if (IsDisposableType)
            {
                T[] result;

                //Enter Lock
                lock (StorageLock)
                {
                    //Enumerate all items to the array
                    result = Storage.ToArray();

                    //Clear stores
                    ContainsStore.Clear();
                    Storage.Clear();
                }

                //Dispose all elements
                foreach (T element in result)
                {
                    (element as IDisposable)!.Dispose();
                }
            }
            else
            {
                CacheClear();
            }
        }
        ///<inheritdoc/>
        protected override void Free()
        {
            //If the element type is disposable, dispose all elements on a hard clear
            if (IsDisposableType)
            {
                //Get all elements
                foreach (T element in Storage.ToArray())
                {
                    (element as IDisposable)!.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets all the elements in the store currently. 
        /// </summary>
        /// <returns>The current elements in storage as a "snapshot"</returns>
        public virtual T[] GetItems() => GetElementsWithLock();
    }
   
}