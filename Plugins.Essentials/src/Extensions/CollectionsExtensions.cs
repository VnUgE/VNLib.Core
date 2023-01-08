/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: CollectionsExtensions.cs 
*
* CollectionsExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace VNLib.Plugins.Essentials.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class CollectionsExtensions
    {
        /// <summary>
        /// Gets a value by the specified key if it exsits and the value is not null/empty
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="key">Key associated with the value</param>
        /// <param name="value">Value associated with the key</param>
        /// <returns>True of the key is found and is not noll/empty, false otherwise</returns>
        public static bool TryGetNonEmptyValue(this IReadOnlyDictionary<string, string> dict, string key, [MaybeNullWhen(false)] out string value)
        {
            if (dict.TryGetValue(key, out string? val) && !string.IsNullOrWhiteSpace(val))
            {
                value = val;
                return true;
            }
            value = null;
            return false;
        }
        /// <summary>
        /// Determines if an argument was set in a <see cref="IReadOnlyDictionary{TKey, TValue}"/> by comparing 
        /// the value stored at the key, to the type argument
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="key">The argument's key</param>
        /// <param name="argument">The argument to compare against</param>
        /// <returns>
        /// True if the key was found, and the value at the key is equal to the type parameter. False if the key is null/empty, or the 
        /// value does not match the specified type
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool IsArgumentSet(this IReadOnlyDictionary<string, string> dict, string key, ReadOnlySpan<char> argument)
        {
            //Try to get the value from the dict, if the value is null casting it to span (implicitly) should stop null excpetions and return false
            return dict.TryGetValue(key, out string? value) && string.GetHashCode(argument) == string.GetHashCode(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            return dict.TryGetValue(key, out TValue? value) ? value : default;
        }
    }
}