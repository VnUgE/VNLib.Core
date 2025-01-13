/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: UserEncodedData.cs 
*
* UserEncodedData.cs is part of VNLib.Plugins.Essentials which 
* is part of the larger VNLib collection of libraries and utilities.
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
using System.Text.Json;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Essentials.Users
{

    /// <summary>
    /// Helper methods for serializing objects into a user's account object using
    /// json serialization and base64url safe encoding
    /// </summary>
    public static class UserEncodedData
    {
        /// <summary>
        /// Decodes a base64url encoded string into an object of type T
        /// </summary>
        /// <typeparam name="T">The desired stored object type</typeparam>
        /// <param name="encodedData">The previously encoded string data to decode</param>
        /// <returns>The decoded object instance if data was successfully recovered</returns>
        public static T? Decode<T>(ReadOnlySpan<char> encodedData) where T : class
        {
            if (encodedData.IsEmpty)
            {
                return null;
            }

            //Output buffer will always be smaller than actual input data due to base64 encoding
            using UnsafeMemoryHandle<byte> binBuffer = MemoryUtil.UnsafeAllocNearestPage(encodedData.Length, zero: true);

            ERRNO bytes = VnEncoding.Base64UrlDecode(encodedData, binBuffer.Span);

            if (!bytes)
            {
                return null;
            }

            //Deserialize the objects directly from binary data
            return JsonSerializer.Deserialize<T>(
                utf8Json: binBuffer.AsSpan(0, bytes),
                options: Statics.SR_OPTIONS
            );
        }

        /// <summary>
        /// Recovers encoded items from the user's account object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="store">The data store to read encoded data from</param>
        /// <param name="index">The property index in the user fields to recover the objects from</param>
        /// <returns>The encoded properties from the desired user index</returns>
        public static T? Decode<T>(IIndexable<string, string> store, string index) where T : class
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(index);

            return Decode<T>(store[index]);
        }      

        /// <summary>
        /// Writes a set of items to the user's account object, encoded in base64
        /// </summary>
        /// <typeparam name="T">The class instance to encode</typeparam>
        /// <param name="instance">The object instance to encode and store</param>
        public static string? Encode<T>(T? instance) where T : class
        {
            if (instance is null)
            {
                return null;
            }

            //Use a memory stream to serialize the items safely
            using VnMemoryStream ms = new(MemoryUtil.Shared, bufferSize: 1024, zero: false);

            JsonSerializer.Serialize(ms, instance, Statics.SR_OPTIONS);

            return VnEncoding.Base64UrlEncode(ms.AsSpan(), includePadding: false);
        }

        /// <summary>
        /// Writes a set of items to the user's account object, encoded in base64
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="store"></param>
        /// <param name="index">The store index to write the encoded string data to</param>
        /// <param name="instance">The object instance to encode and store</param>
        public static void Encode<T>(IIndexable<string, string> store, string index, T? instance) where T : class
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(index);

            store[index] = Encode(instance)!;
        }
    }
}
