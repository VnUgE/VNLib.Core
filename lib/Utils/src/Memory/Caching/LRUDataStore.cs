/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: LRUDataStore.cs 
*
* LRUDataStore.cs is part of VNLib.Utils which is part of the larger 
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace VNLib.Utils.Memory.Caching
{
    /// <summary>
    /// A Least Recently Used store base class for E2E O(1) operations
    /// </summary>
    /// <typeparam name="TKey">A key used for O(1) lookups</typeparam>
    /// <typeparam name="TValue">A value to store</typeparam>
    public abstract class LRUDataStore<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IReadOnlyCollection<TValue>, IEnumerable<KeyValuePair<TKey, TValue>> 
        where TKey: notnull
    {
        /// <summary>
        /// A lookup table that provides O(1) access times for key-value lookups
        /// </summary>
        protected Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> LookupTable { get; }
        /// <summary>
        /// A linked list that tracks the least recently used item. 
        /// New items (or recently accessed items) are moved to the end of the list.
        /// The head contains the least recently used item
        /// </summary>
        protected LinkedList<KeyValuePair<TKey, TValue>> List { get; }

        /// <summary>
        /// Initializes an empty <see cref="LRUDataStore{TKey, TValue}"/>
        /// </summary>
        protected LRUDataStore()
        {
            LookupTable = new();
            List = new();
        }

        /// <summary>
        /// Initializes an empty <see cref="LRUDataStore{TKey, TValue}"/> and sets
        /// the lookup table's inital capacity
        /// </summary>
        /// <param name="initialCapacity">LookupTable initial capacity</param>
        protected LRUDataStore(int initialCapacity)
        {
            LookupTable = new(initialCapacity);
            List = new();
        }

        /// <summary>
        /// Initializes an empty <see cref="LRUDataStore{TKey, TValue}"/> and uses the 
        /// specified keycomparison 
        /// </summary>
        /// <param name="keyComparer">A <see cref="IEqualityComparer{T}"/> used by the Lookuptable to compare keys</param>
        protected LRUDataStore(IEqualityComparer<TKey> keyComparer)
        {
            LookupTable = new(keyComparer);
            List = new();
        }

        /// <summary>
        /// Initializes an empty <see cref="LRUDataStore{TKey, TValue}"/> and uses the 
        /// specified keycomparison, and sets the lookup table's initial capacity
        /// </summary>
        /// <param name="initialCapacity">LookupTable initial capacity</param>
        /// <param name="keyComparer">A <see cref="IEqualityComparer{T}"/> used by the Lookuptable to compare keys</param>
        protected LRUDataStore(int initialCapacity, IEqualityComparer<TKey> keyComparer)
        {
            LookupTable = new(initialCapacity, keyComparer);
            List = new();
        }

        /// <summary>
        /// Gets or sets a value within the LRU cache.
        /// </summary>
        /// <param name="key">The key identifying the value</param>
        /// <returns>The value stored at the given key</returns>
        /// <remarks>Items are promoted in the store when accessed</remarks>
        public virtual TValue this[TKey key] 
        {
            get
            {
                return TryGetValue(key, out TValue? value)
                    ? value
                    : throw new KeyNotFoundException("The item or its key were not found in the LRU data store");
            }
            set
            {
                //If a node by the same key in the store exists, just replace its value
                if(LookupTable.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>>? oldNode))
                {
                    //Remove the node before re-adding it
                    List.Remove(oldNode);

                    //Reuse the node
                    oldNode.ValueRef = new KeyValuePair<TKey, TValue>(key, value);

                    //Move the item to the back of the list
                    List.AddLast(oldNode);
                }
                else
                {
                    //Node does not exist yet so create new one
                    Add(key, value);
                }
            }
        }
        ///<inheritdoc/>
        public ICollection<TKey> Keys => LookupTable.Keys;

        ///<summary>
        /// Not supported
        /// </summary>
        ///<exception cref="NotImplementedException"></exception>
        public virtual ICollection<TValue> Values => throw new NotSupportedException("Values are not stored in an independent collection, as they are not directly mutable");
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => LookupTable.Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => List.Select(static node => node.Value);
        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => List.Select(static node => node.Value).GetEnumerator();

        /// <summary>
        /// Gets the number of items within the LRU store
        /// </summary>
        public int Count => List.Count;
        
        ///<inheritdoc/>
        public abstract bool IsReadOnly { get; }

        /// <summary>
        /// Adds the specified record to the store and places it at the end of the LRU queue
        /// </summary>
        /// <param name="key">The key identifying the record</param>
        /// <param name="value">The value to store at the key</param>
        public void Add(TKey key, TValue value)
        {
            //Create new kvp lookup ref
            KeyValuePair<TKey, TValue> lookupRef = new(key, value);
            //Insert the lookup
            Add(in lookupRef);
        }

        ///<inheritdoc/>
        public bool Remove(in KeyValuePair<TKey, TValue> item) => Remove(item.Key);
        ///<inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => List.GetEnumerator();
        ///<inheritdoc/>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => List.CopyTo(array, arrayIndex);
        ///<inheritdoc/>
        public virtual bool ContainsKey(TKey key) => LookupTable.ContainsKey(key);
        ///<inheritdoc/>
        public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => List.GetEnumerator();

        /// <summary>
        /// Adds the specified record to the store and places it at the end of the LRU queue
        /// </summary>
        /// <param name="item">The item to add</param>
        public virtual void Add(in KeyValuePair<TKey, TValue> item)
        {
            //Init new ll node
            LinkedListNode<KeyValuePair<TKey, TValue>> newNode = new(item);
            //Insert the new node 
            LookupTable.Add(item.Key, newNode);
            //Add to the end of the linked list
            List.AddLast(newNode);
        }

        /// <summary>
        /// Removes all elements from the LRU store
        /// </summary>
        public virtual void Clear()
        {
            //Clear lists
            LookupTable.Clear();
            List.Clear();
        }
        /// <summary>
        /// Determines if the <see cref="KeyValuePair{TKey, TValue}"/> exists in the store
        /// </summary>
        /// <param name="item">The record to search for</param>
        /// <returns>True if the key was found in the store and the value equals the stored value, false otherwise</returns>
        public virtual bool Contains(in KeyValuePair<TKey, TValue> item)
        {
            if (LookupTable.TryGetValue(item.Key, out LinkedListNode<KeyValuePair<TKey, TValue>>? lookup))
            {
                return lookup.Value.Value?.Equals(item.Value) ?? false;
            }
            return false;
        }
        ///<inheritdoc/>
        public virtual bool Remove(TKey key)
        {
            //Remove the item from the lookup table and if it exists, remove the node from the list
            if(LookupTable.Remove(key, out LinkedListNode<KeyValuePair<TKey, TValue>>? node))
            {
                //Remove the new from the list
                List.Remove(node);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Tries to get a value from the store with its key. Found items are promoted
        /// </summary>
        /// <param name="key">The key identifying the value</param>
        /// <param name="value">The found value</param>
        /// <returns>A value indicating if the element was found in the store</returns>
        public virtual bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value)
        {
            //Lookup the 
            if (LookupTable.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>>? val))
            {
                //Remove the value from the list and add it to the front of the list
                List.Remove(val);
                List.AddLast(val);
                value = val.Value.Value!;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Adds the specified record to the store and places it at the end of the LRU queue
        /// </summary>
        /// <param name="item">The item to add</param>
        public virtual void Add(KeyValuePair<TKey, TValue> item) => Add(in item);

        ///<inheritdoc/>
        public virtual bool Contains(KeyValuePair<TKey, TValue> item) => Contains(in item);

        ///<inheritdoc/>
        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(in item);       
    }
}
