/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: ReadOnlyJsonWebKey.cs 
*
* ReadOnlyJsonWebKey.cs is part of VNLib.Hashing.Portable which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Hashing.Portable is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Hashing.Portable is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Hashing.Portable. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.Frozen;

using VNLib.Utils.Memory;

namespace VNLib.Hashing.IdentityUtility
{
    /// <summary>
    /// A readonly Json Web Key (JWK) data structure that may be used for signing 
    /// or verifying messages.
    /// </summary>
    public sealed class ReadOnlyJsonWebKey : IJsonWebKey
    {
        private readonly FrozenDictionary<string, string?> _properties;

        /// <summary>
        /// Creates a new instance of <see cref="ReadOnlyJsonWebKey"/> from a dictionary of 
        /// JWK string properties
        /// </summary>
        /// <param name="properties">The frozen dictionary instance of parsed JWK properties</param>
        public ReadOnlyJsonWebKey(FrozenDictionary<string, string?> properties)
        {
            ArgumentNullException.ThrowIfNull(properties);
            _properties = properties;

            //Create a JWT header from the values
            JwtHeader = new Dictionary<string, string?>()
            {
                { "alg" , Algorithm },
                { "typ" , "JWT" },
            };

            //Configure key usage
            KeyUse = (Use?.ToLower(null)) switch
            {
                "sig" => JwkKeyUsage.Signature,
                "enc" => JwkKeyUsage.Encryption,
                _ => JwkKeyUsage.None,
            };
        }

        /// <summary>
        /// Creates a new instance of <see cref="ReadOnlyJsonWebKey"/> from a <see cref="JsonElement"/>.
        /// This will call <see cref="JsonElement.Clone"/> on the element and store an internal copy
        /// </summary>
        /// <param name="keyElement">The <see cref="JsonElement"/> to create the <see cref="ReadOnlyJsonWebKey"/> from</param>
        public ReadOnlyJsonWebKey(ref readonly JsonElement keyElement)
            :this(
                 //Get only top-level string properties and store them in a dictionary
                 keyElement.EnumerateObject()
                    .Where(static k => k.Value.ValueKind == JsonValueKind.String)
                    .ToDictionary(static k => k.Name, v => v.Value.GetString(), StringComparer.OrdinalIgnoreCase)
                    .ToFrozenDictionary()
            )
        { }

        /// <summary>
        /// Creates a new instance of <see cref="ReadOnlyJsonWebKey"/> from a raw utf8 encoded json 
        /// binary sequence
        /// </summary>
        /// <param name="rawValue">The utf8 encoded json binary sequence</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="JsonException"></exception>
        public static ReadOnlyJsonWebKey FromUtf8Bytes(ReadOnlySpan<byte> rawValue)
        {
            Utf8JsonReader reader = new (rawValue);
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;
            return new ReadOnlyJsonWebKey(ref root);
        }

        /// <summary>
        /// Creates a new instance of <see cref="ReadOnlyJsonWebKey"/> from a raw utf8 encoded json
        /// memory segment
        /// </summary>
        /// <param name="rawValue">The utf8 encoded json binary sequence</param>
        /// <returns>The readonly JWK object</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="JsonException"></exception>
        public static ReadOnlyJsonWebKey FromUtf8Bytes(ReadOnlyMemory<byte> rawValue)
        {
            using JsonDocument doc = JsonDocument.Parse(rawValue);
            JsonElement root = doc.RootElement;
            return new ReadOnlyJsonWebKey(ref root);
        }

        /// <summary>
        /// Creates a new instance of <see cref="ReadOnlyJsonWebKey"/> from a json string
        /// </summary>
        /// <param name="jsonString">The json encoded string to recover the JWK from</param>
        /// <returns></returns>
        public static ReadOnlyJsonWebKey FromJsonString(string jsonString)
        {
            using JsonDocument doc = JsonDocument.Parse(jsonString);
            JsonElement root = doc.RootElement;
            return new ReadOnlyJsonWebKey(ref root);
        }

        /// <summary>
        /// The key identifier
        /// </summary>
        public string? KeyId => _properties.GetValueOrDefault("kid");

        /// <summary>
        /// The key type
        /// </summary>
        public string? KeyType => _properties.GetValueOrDefault("kty");

        /// <summary>
        /// The key algorithm
        /// </summary>
        public string? Algorithm => _properties.GetValueOrDefault("alg");

        /// <summary>
        /// The key "use" value
        /// </summary>
        public string? Use => _properties.GetValueOrDefault("use");

        /// <summary>
        /// Returns the JWT header that matches this key
        /// </summary>
        public IReadOnlyDictionary<string, string?> JwtHeader { get; }
       
        ///<inheritdoc/>
        public JwkKeyUsage KeyUse { get; }

        ///<inheritdoc/>
        public string? GetKeyProperty(string propertyName) => _properties.GetValueOrDefault(propertyName);

        /// <summary>
        /// Attemts to erase all property values from memory by securely writing over them with zeros
        /// </summary>
        public void EraseValues()
        {
            foreach(string? value in _properties.Values)
            {
                PrivateStringManager.EraseString(value);
            }
        }

    }
}
