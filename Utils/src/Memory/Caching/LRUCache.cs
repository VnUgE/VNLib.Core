/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: LRUCache.cs 
*
* LRUCache.cs is part of VNLib.Utils which is part of the larger 
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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace VNLib.Utils.Memory.Caching
{
    /// <summary>
    /// A base class for a Least Recently Used cache 
    /// </summary>
    /// <typeparam name="TKey">The key for O(1) lookups</typeparam>
    /// <typeparam name="TValue">The value to store within cache</typeparam>
    public abstract class LRUCache<TKey, TValue> : LRUDataStore<TKey, TValue> where TKey : notnull
    {
        ///<inheritdoc/>
        protected LRUCache()
        {}
        ///<inheritdoc/>
        protected LRUCache(int initialCapacity) : base(initialCapacity)
        {}
        ///<inheritdoc/>
        protected LRUCache(IEqualityComparer<TKey> keyComparer) : base(keyComparer)
        {}
        ///<inheritdoc/>
        protected LRUCache(int initialCapacity, IEqualityComparer<TKey> keyComparer) : base(initialCapacity, keyComparer)
        {}

        /// <summary>
        /// The maximum number of items to store in LRU cache
        /// </summary>
        protected abstract int MaxCapacity { get; }

        /// <summary>
        /// Adds a new record to the LRU cache 
        /// </summary>
        /// <param name="item">A <see cref="KeyValuePair{TKey, TValue}"/> to add to the cache store</param>
        public override void Add(KeyValuePair<TKey, TValue> item)
        {
            //See if the store is at max capacity and an item needs to be evicted
            if(Count == MaxCapacity)
            {
                //A record needs to be evicted before a new record can be added

                //Get the oldest node from the list to reuse its instance and remove the old value
                LinkedListNode<KeyValuePair<TKey, TValue>> oldNode = List.First!; //not null because count is at max capacity so an item must be at the end of the list
                //Store old node value field
                KeyValuePair<TKey, TValue> oldRecord = oldNode.Value;
                //Remove from lookup
                LookupTable.Remove(oldRecord.Key);
                //Remove the node
                List.RemoveFirst();
                //Reuse the old ll node
                oldNode.Value = item;
                //add lookup with new key
                LookupTable.Add(item.Key, oldNode);
                //Add to end of list
                List.AddLast(oldNode);
                //Invoke evicted method
                Evicted(oldRecord);
            }
            else
            {
                //Add new item to the list
                base.Add(item);
            }
        }
        /// <summary>
        /// Attempts to get a value by the given key. 
        /// </summary>
        /// <param name="key">The key identifying the value to store</param>
        /// <param name="value">The value to store</param>
        /// <returns>A value indicating if the value was found in the store</returns>
        public override bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value)
        {
            //See if the cache contains the value
            if(base.TryGetValue(key, out value))
            {
                //Cache hit
                return true;
            }
            //Cache miss
            if(CacheMiss(key, out value))
            {
                //Lookup hit
                //Add the record to the store (eviction will happen as necessary
                Add(key, value);
                return true;
            }
            //Record does not exist
            return false;
        }
        /// <summary>
        /// Invoked when a record is evicted from the cache
        /// </summary>
        /// <param name="evicted">The record that is being evicted</param>
        protected abstract void Evicted(KeyValuePair<TKey, TValue> evicted);
        /// <summary>
        /// Invoked when an entry was requested and was not found in cache.
        /// </summary>
        /// <param name="key">The key identifying the record to lookup</param>
        /// <param name="value">The found value matching the key</param>
        /// <returns>A value indicating if the record was found</returns>
        protected abstract bool CacheMiss(TKey key, [NotNullWhen(true)] out TValue? value);
    }
}
