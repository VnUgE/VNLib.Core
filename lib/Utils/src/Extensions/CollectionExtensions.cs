/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: CollectionExtensions.cs 
*
* CollectionExtensions.cs is part of VNLib.Utils which is part of the larger 
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
using System.Collections.Generic;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Provides collection extension methods
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Gets a previously-stored base32 encoded value-type from the lookup and returns its initialized structure from
        /// the value stored
        /// </summary>
        /// <typeparam name="TKey">The key type used to index the lookup</typeparam>
        /// <typeparam name="TValue">An unmanaged structure type</typeparam>
        /// <param name="lookup"></param>
        /// <param name="key">The key used to identify the value</param>
        /// <returns>The initialized structure, or default if the lookup returns null/empty string</returns>
        public static TValue GetValueType<TKey, TValue>(this IIndexable<TKey, string> lookup, TKey key) where TValue : unmanaged where TKey : notnull
        {
            if (lookup is null)
            {
                throw new ArgumentNullException(nameof(lookup));
            }
            //Get value
            string value = lookup[key];
            //If the string is set, recover the value and return it
            return string.IsNullOrWhiteSpace(value) ? default : VnEncoding.FromBase32String<TValue>(value);
        }

        /// <summary>
        /// Serializes a value-type in base32 encoding and stores it at the specified key
        /// </summary>
        /// <typeparam name="TKey">The key type used to index the lookup</typeparam>
        /// <typeparam name="TValue">An unmanaged structure type</typeparam>
        /// <param name="lookup"></param>
        /// <param name="key">The key used to identify the value</param>
        /// <param name="value">The value to serialze</param>
        public static void SetValueType<TKey, TValue>(this IIndexable<TKey, string> lookup, TKey key, TValue value) where TValue : unmanaged where TKey : notnull
        {
            //encode string from value type and store in lookup
            lookup[key] = VnEncoding.ToBase32String(value);
        }
        /// <summary>
        /// Executes a handler delegate on every element of the list within a try-catch block
        /// and rethrows exceptions as an <see cref="AggregateException"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="handler">An <see cref="Action"/> handler delegate to complete some operation on the elements within the list</param>
        /// <exception cref="AggregateException"></exception>
        public static void TryForeach<T>(this IEnumerable<T> list, Action<T> handler)
        {
            List<Exception>? exceptionList = null;
            foreach(T item in list)
            {
                try
                {
                    handler(item);
                }
                catch(Exception ex)
                {
                    //Init new list and add the exception
                    exceptionList ??= new();
                    exceptionList.Add(ex);
                }
            }
            //Raise aggregate exception for all caught exceptions
            if(exceptionList?.Count > 0)
            {
                throw new AggregateException(exceptionList);
            }
        }
    }
}
