/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: CacheExtensions.cs 
*
* CacheExtensions.cs is part of VNLib.Utils which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Utils.Memory.Caching;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Cache collection extensions
    /// </summary>
    public static class CacheExtensions
    {
        /// <summary>
        /// <para>
        /// Stores a new record. If an old record exists, the records are compared, 
        /// if they are not equal, the old record is evicted and the new record is stored
        /// </para>
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T">A cachable object</typeparam>
        /// <param name="store"></param>
        /// <param name="key">The unique key identifying the record</param>
        /// <param name="record">The record to store</param>
        /// <remarks>
        /// Locks on the store parameter to provide mutual exclusion for non thread-safe 
        /// data structures.
        /// </remarks>
        public static void StoreRecord<TKey, T>(this IDictionary<TKey, T> store, TKey key, T record) where T : ICacheable
        {
            ArgumentNullException.ThrowIfNull(store);

            T ?oldRecord = default;
            lock (store)
            {
                //See if an old record exists
                if (!store.Remove(key, out oldRecord) || oldRecord == null)
                {
                    //Old record doesnt exist, store and return
                    store[key] = record;
                    return;
                }
                //See if the old and new records and the same record
                if (oldRecord.Equals(record))
                {
                    //records are equal, so we can exit
                    return;
                }
                //Old record is not equal, so we can store the new record and evict the old on
                store[key] = record;
            }
            //Call evict on the old record
            oldRecord.Evicted();
        }
        /// <summary>
        /// <para>
        /// Stores a new record and updates the expiration date. If an old record exists, the records
        /// are compared, if they are not equal, the old record is evicted and the new record is stored
        /// </para>
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T">A cachable object</typeparam>
        /// <param name="store"></param>
        /// <param name="key">The unique key identifying the record</param>
        /// <param name="record">The record to store</param>
        /// <param name="validFor">The new expiration time of the record</param>
        /// <remarks>
        /// Locks on the store parameter to provide mutual exclusion for non thread-safe 
        /// data structures.
        /// </remarks>
        public static void StoreRecord<TKey, T>(this IDictionary<TKey, T> store, TKey key, T record, TimeSpan validFor) where T : ICacheable
        {
            //Update the expiration time
            record.Expires = DateTime.UtcNow.Add(validFor);
            //Store
            StoreRecord(store, key, record);
        }
        /// <summary>
        /// <para>
        /// Returns a stored record if it exists and is not expired. If the record exists
        /// but has expired, it is evicted.
        /// </para>
        /// <para>
        /// If a record is evicted, the return value evaluates to -1 and the value parameter
        /// is set to the old record if the caller wished to inspect the record after the 
        /// eviction method completes
        /// </para>
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T">A cachable object</typeparam>
        /// <param name="store"></param>
        /// <param name="key"></param>
        /// <param name="value">The record</param>
        /// <returns>
        /// Gets a value indicating the reults of the operation. 0 if the record is not found, -1 if expired, 1 if 
        /// record is valid
        /// </returns>
        /// <remarks>
        /// Locks on the store parameter to provide mutual exclusion for non thread-safe 
        /// data structures.
        /// </remarks>
        public static ERRNO TryGetOrEvictRecord<TKey, T>(this IDictionary<TKey, T> store, TKey key, out T? value) where T : ICacheable
        {
            ArgumentNullException.ThrowIfNull(store);

            value = default;
            //Cache current date time before entering the lock
            DateTime now = DateTime.UtcNow;
            //Get value
            lock (store)
            {
                //try to get the value
                if (!store.TryGetValue(key, out value))
                {
                    //not found
                    return 0;
                }
                //Not expired
                if (value.Expires > now)
                {
                    return true;
                }
                //Remove from store
                _ = store.Remove(key);
            }
            //Call the evict func
            value.Evicted();
            return -1;
        }
        /// <summary>
        /// Updates the expiration date on a record to the specified time if it exists, regardless 
        /// of its validity
        /// </summary>
        /// <typeparam name="TKey">Diction key type</typeparam>
        /// <typeparam name="T">A cachable object</typeparam>
        /// <param name="store"></param>
        /// <param name="key">The unique key identifying the record to update</param>
        /// <param name="extendedTime">The expiration time (time added to <see cref="DateTime.UtcNow"/>)</param>
        /// <remarks>
        /// Locks on the store parameter to provide mutual exclusion for non thread-safe 
        /// data structures.
        /// </remarks>
        public static void UpdateRecord<TKey, T>(this IDictionary<TKey, T> store, TKey key, TimeSpan extendedTime) where T : ICacheable
        {
            //Cacl the expiration time
            DateTime expiration = DateTime.UtcNow.Add(extendedTime);
            lock (store)
            {
                //Update the expiration time if the record exists
                if (store.TryGetValue(key, out T? record) && record != null)
                {
                    record.Expires = expiration;
                }
            }
        }
        /// <summary>
        /// Evicts a stored record from the store. If the record is found, the eviction 
        /// method is executed
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="store"></param>
        /// <param name="key">The unique key identifying the record</param>
        /// <returns>True if the record was found and evicted</returns>
        public static bool EvictRecord<TKey, T>(this IDictionary<TKey, T> store, TKey key) where T : ICacheable
        {
            ArgumentNullException.ThrowIfNull(store);

            T? record = default;
            lock (store)
            {
                //Try to remove the record
                if (!store.Remove(key, out record) || record == null)
                {
                    //No record found or null
                    return false;
                }
            }
            //Call eviction mode
            record.Evicted();
            return true;
        }
        /// <summary>
        /// Evicts all expired records from the store
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T"></typeparam>
        public static void CollectRecords<TKey, T>(this IDictionary<TKey, T> store) where T : ICacheable 
            => CollectRecords(store, DateTime.UtcNow);

        /// <summary>
        /// Evicts all expired records from the store
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="store"></param>
        /// <param name="validAfter">A time that specifies the time which expired records should be evicted</param>
        public static void CollectRecords<TKey, T>(this IDictionary<TKey, T> store, DateTime validAfter) where T : ICacheable
        {
            ArgumentNullException.ThrowIfNull(store);
            //Build a query to get the keys that belong to the expired records
            IEnumerable<KeyValuePair<TKey, T>> expired = store.Where(s => s.Value.Expires < validAfter);
            //temp list for expired records
            IEnumerable<T> evicted;
            //Take lock on store
            lock (store)
            {
                KeyValuePair<TKey, T>[] kvp = expired.ToArray();
                //enumerate to array so values can be removed while the lock is being held
                foreach (KeyValuePair<TKey, T> pair in kvp)
                {
                    //remove the record and call the eviction method
                    _ = store.Remove(pair);
                }
                //select values while lock held
                evicted = kvp.Select(static v => v.Value);
            }
            //Iterrate over evicted records and call evicted method
            foreach (T ev in evicted)
            {
                ev.Evicted();
            }
        }

        /// <summary>
        /// Allows for mutually exclusive use of a <see cref="ICacheable"/> record with a 
        /// state parameter
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TState"></typeparam>
        /// <param name="store"></param>
        /// <param name="key">The unique key identifying the record</param>
        /// <param name="state">A user-token type state parameter to pass to the use callback method</param>
        /// <param name="useCtx">A callback method that will be passed the record to use within an exclusive context</param>
        public static void UseRecord<TKey, T, TState>(this IDictionary<TKey, T> store, TKey key, TState state, Action<T, TState> useCtx) where T: ICacheable
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(useCtx);

            lock (store)
            {
                //If the record exists
                if(store.TryGetValue(key, out T? record))
                {
                    //Use it within the lock statement
                    useCtx(record, state);
                }
            }
        }
        /// <summary>
        /// Allows for mutually exclusive use of a <see cref="ICacheable"/> 
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="store"></param>
        /// <param name="key">The unique key identifying the record</param>
        /// <param name="useCtx">A callback method that will be passed the record to use within an exclusive context</param>
        public static void UseRecord<TKey, T>(this IDictionary<TKey, T> store, TKey key, Action<T> useCtx) where T : ICacheable
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(useCtx);

            lock (store)
            {
                //If the record exists
                if (store.TryGetValue(key, out T? record))
                {
                    //Use it within the lock statement
                    useCtx(record);
                }
            }
        }
        /// <summary>
        /// Allows for mutually exclusive use of a <see cref="ICacheable"/> record with a 
        /// state parameter, only if the found record is valid
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TState"></typeparam>
        /// <param name="store"></param>
        /// <param name="key">The unique key identifying the record</param>
        /// <param name="state">A user-token type state parameter to pass to the use callback method</param>
        /// <param name="useCtx">A callback method that will be passed the record to use within an exclusive context</param>
        /// <remarks>If the record is found, but is expired, the record is evicted from the store. The callback is never invoked</remarks>
        public static void UseIfValid<TKey, T, TState>(
            this IDictionary<TKey, T> store, 
            TKey key, 
            TState state, 
            Action<T, TState> useCtx
        ) where T : ICacheable
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(useCtx);

            DateTime now = DateTime.UtcNow;
            T? record;
            lock (store)
            {
                //If the record exists, check if its valid
                if (store.TryGetValue(key, out record) && record.Expires < now)
                {
                    //Use it within the lock statement
                    useCtx(record, state);
                    return;
                }
                //Record is no longer valid
                _ = store.Remove(key);
            }
            //Call evicted method
            record?.Evicted();
        }
        /// <summary>
        /// Allows for mutually exclusive use of a <see cref="ICacheable"/> record with a 
        /// state parameter, only if the found record is valid
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="store"></param>
        /// <param name="key">The unique key identifying the record</param>
        /// <param name="useCtx">A callback method that will be passed the record to use within an exclusive context</param>
        /// <remarks>If the record is found, but is expired, the record is evicted from the store. The callback is never invoked</remarks>
        public static void UseIfValid<TKey, T>(this IDictionary<TKey, T> store, TKey key, Action<T> useCtx) where T : ICacheable
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(useCtx);

            DateTime now = DateTime.UtcNow;
            T? record;
            lock (store)
            {
                //If the record exists, check if its valid
                if (store.TryGetValue(key, out record) && record.Expires < now)
                {
                    //Use it within the lock statement
                    useCtx(record);
                    return;
                }
                //Record is no longer valid
                _ = store.Remove(key);
            }
            //Call evicted method
            record?.Evicted();
        }
    }
}
