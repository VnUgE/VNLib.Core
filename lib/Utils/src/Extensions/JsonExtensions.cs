/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: JsonExtensions.cs 
*
* JsonExtensions.cs is part of VNLib.Utils which is part of the larger 
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
using System.Text.Json;
using System.Collections.Generic;

using VNLib.Utils.IO;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Specifies how to parse a timespan value from a <see cref="JsonDocument"/> element
    /// </summary>
    public enum TimeParseType
    {
        /// <summary>
        /// Parses the value for <see cref="TimeSpan"/> as milliseconds
        /// </summary>
        Milliseconds,
        /// <summary>
        /// Parses the value for <see cref="TimeSpan"/> as seconds
        /// </summary>
        Seconds,
        /// <summary>
        /// Parses the value for <see cref="TimeSpan"/> as milliseconds
        /// </summary>
        Minutes,
        /// <summary>
        /// Parses the value for <see cref="TimeSpan"/> as milliseconds
        /// </summary>
        Hours,
        /// <summary>
        /// Parses the value for <see cref="TimeSpan"/> as milliseconds
        /// </summary>
        Days,
        /// <summary>
        /// Parses the value for <see cref="TimeSpan"/> as milliseconds
        /// </summary>
        Ticks
    }
    
    /// <summary>
    /// Provides extension methods for JSON serialization and deserialization operations
    /// </summary>
    public static class JsonExtensions
    {
        /// <summary>
        /// Converts a JSON encoded binary data to an object of the specified type
        /// </summary>
        /// <typeparam name="T">Output type of the object</typeparam>
        /// <param name="utf8bin"></param>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use during de-serialization</param>
        /// <returns>The new object or default if the string is null or empty</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        [Obsolete("Unused and unsupported, will be removed in a future release.")]
        public static T? AsJsonObject<T>(this ReadOnlySpan<byte> utf8bin, JsonSerializerOptions? options = null)
        {
            return utf8bin.IsEmpty ? default : JsonSerializer.Deserialize<T>(utf8bin, options);
        }

        /// <summary>
        /// Converts a JSON encoded binary data to an object of the specified type
        /// </summary>
        /// <typeparam name="T">Output type of the object</typeparam>
        /// <param name="utf8bin"></param>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use during de-serialization</param>
        /// <returns>The new object or default if the string is null or empty</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        [Obsolete("Unused and unsupported, will be removed in a future release.")]
        public static T? AsJsonObject<T>(this ReadOnlyMemory<byte> utf8bin, JsonSerializerOptions? options = null)
        {
            return utf8bin.IsEmpty ? default : JsonSerializer.Deserialize<T>(utf8bin.Span, options);
        }

        /// <summary>
        /// Converts a JSON encoded binary data to an object of the specified type
        /// </summary>
        /// <typeparam name="T">Output type of the object</typeparam>
        /// <param name="utf8bin"></param>
        /// <param name="options"><see cref="JsonSerializerOptions"/> to use during de-serialization</param>
        /// <returns>The new object or default if the string is null or empty</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        [Obsolete("Unused and unsupported, will be removed in a future release.")]
        public static T? AsJsonObject<T>(this byte[] utf8bin, JsonSerializerOptions? options = null)
        {
            return utf8bin == null ? default : JsonSerializer.Deserialize<T>(utf8bin.AsSpan(), options);
        }

        /// <summary>
        /// Parses a json encoded string to a json documen
        /// </summary>
        /// <param name="jsonString"></param>
        /// <param name="options"></param>
        /// <returns>If the json string is null, returns null, otherwise the json document around the data</returns>
        /// <exception cref="JsonException"></exception>
        [Obsolete("Unused and unsupported, will be removed in a future release.")]
        public static JsonDocument? AsJsonDocument(this string jsonString, JsonDocumentOptions options = default)
        {
            return jsonString == null ? null : JsonDocument.Parse(jsonString, options);
        }

        /// <summary>
        /// Shortcut extension to <see cref="JsonElement.GetProperty(string)"/> and returns a string 
        /// only if the property exists and is a string value.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="propertyName">The name of the property to get the string value of</param>
        /// <returns>If the property exists, and it a string json kind, returns the string stored at that property</returns>
        public static string? GetPropString(this JsonElement element, string propertyName)
        {
            // Try to get the propery element and ensure it is a string
            return element.TryGetProperty(propertyName, out JsonElement el) 
                && el.ValueKind == JsonValueKind.String 
                ? el.GetString() 
                : null;
        }

        /// <summary>
        /// Shortcut extension to <see cref="JsonElement.GetProperty(string)"/> and returns a string 
        /// only if the property exists and is a string value.
        /// </summary>
        /// <param name="conf"></param>
        /// <param name="propertyName">The name of the property to get the string value of</param>
        /// <returns>If the property exists, and it a string json kind, returns the string stored at that property</returns>
        public static string? GetPropString(this IReadOnlyDictionary<string, JsonElement> conf, string propertyName)
        {
            return conf.TryGetValue(propertyName, out JsonElement el)
                && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }

        /// <summary>
        /// Shortcut extension to <see cref="JsonElement.GetProperty(string)"/> and returns a string 
        /// </summary>
        /// <param name="conf"></param>
        /// <param name="propertyName">The name of the property to get the string value of</param>
        /// <returns>If the property exists, returns the string stored at that property</returns>
        [Obsolete("Use the IReadOnlyDictionary overload instead, this will be removed in a future release.")]
        public static string? GetPropString(this IDictionary<string, JsonElement> conf, string propertyName) 
            => GetPropString((IReadOnlyDictionary<string, JsonElement>)conf, propertyName);

        /// <summary>
        /// Merges the current <see cref="JsonDocument"/> with another <see cref="JsonDocument"/> to 
        /// create a new document of combined properties
        /// </summary>
        /// <param name="initial"></param>
        /// <param name="other">The <see cref="JsonDocument"/> to combine with the first document</param>
        /// <param name="initalName">The name of the new element containing the initial document data</param>
        /// <param name="secondName">The name of the new element containing the additional document data</param>
        /// <returns>A new document with a parent root containing the combined objects</returns>
        public static JsonDocument Merge(this JsonDocument initial, JsonDocument other, string initalName, string secondName)
        {
            ArgumentNullException.ThrowIfNull(initial);
            ArgumentNullException.ThrowIfNull(other);

            return Merge(
                initial.RootElement, 
                other.RootElement, 
                initalName, 
                secondName
            );
        }

        /// <summary>
        /// Merges the current <see cref="JsonElement"/> with another <see cref="JsonElement"/> to 
        /// create a new document of combined properties
        /// </summary>
        /// <param name="initial"></param>
        /// <param name="other">The <see cref="JsonElement"/> to combine with the first document</param>
        /// <param name="initalName">The name of the new element containing the initial document data</param>
        /// <param name="secondName">The name of the new element containing the additional document data</param>
        /// <returns>A new document with a parent root containing the combined objects</returns>
        public static JsonDocument Merge(this in JsonElement initial, in JsonElement other, string initalName, string secondName)
        {
            //Open a new memory buffer to write to
            using VnMemoryStream ms = new();         
           
            using (Utf8JsonWriter writer = new(ms))
            {                
                writer.WriteStartObject();

                //Write the first object property
                writer.WritePropertyName(initalName);               
                initial.WriteTo(writer);

                //Write the second object property
                writer.WritePropertyName(secondName);               
                other.WriteTo(writer);
               
                writer.WriteEndObject();
            }

            //rewind the buffer
            _ = ms.Seek(0, System.IO.SeekOrigin.Begin);

            //Parse the stream into the new document and return it
            return JsonDocument.Parse(ms);
        }

        /// <summary>
        /// Parses a number value into a <see cref="TimeSpan"/> of the specified time
        /// </summary>
        /// <param name="el"></param>
        /// <param name="type">The <see cref="TimeParseType"/> the value represents</param>
        /// <returns>The <see cref="TimeSpan"/> of the value</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static TimeSpan GetTimeSpan(this JsonElement el, TimeParseType type)
        {
            return type switch
            {
                TimeParseType.Milliseconds => TimeSpan.FromMilliseconds(el.GetDouble()),
                TimeParseType.Seconds => TimeSpan.FromSeconds(el.GetDouble()),
                TimeParseType.Minutes => TimeSpan.FromMinutes(el.GetDouble()),
                TimeParseType.Hours => TimeSpan.FromHours(el.GetDouble()),
                TimeParseType.Days => TimeSpan.FromDays(el.GetDouble()),
                TimeParseType.Ticks => TimeSpan.FromTicks(el.GetInt64()),
                _ => throw new NotSupportedException(),
            };
        }
    }
}