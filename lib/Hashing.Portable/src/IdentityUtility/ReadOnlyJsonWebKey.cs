/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Text.Json;
using System.Collections.Generic;

using VNLib.Utils;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing.IdentityUtility
{
    /// <summary>
    /// A readonly Json Web Key (JWK) data structure that may be used for signing 
    /// or verifying messages.
    /// </summary>
    public sealed class ReadOnlyJsonWebKey : VnDisposeable, IJsonWebKey
    {
        private readonly JsonElement _jwk;
        private readonly JsonDocument? _doc;

        /// <summary>
        /// Creates a new instance of <see cref="ReadOnlyJsonWebKey"/> from a <see cref="JsonElement"/>.
        /// This will call <see cref="JsonElement.Clone"/> on the element and store an internal copy
        /// </summary>
        /// <param name="keyElement">The <see cref="JsonElement"/> to create the <see cref="ReadOnlyJsonWebKey"/> from</param>
        public ReadOnlyJsonWebKey(in JsonElement keyElement)
        {
            _jwk = keyElement.Clone();
            //Set initial values
            KeyId = _jwk.GetPropString("kid");
            KeyType = _jwk.GetPropString("kty");
            Algorithm = _jwk.GetPropString("alg");
            Use = _jwk.GetPropString("use");

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
        /// Creates a new instance of <see cref="ReadOnlyJsonWebKey"/> from a raw utf8 encoded json 
        /// binary sequence
        /// </summary>
        /// <param name="rawValue">The utf8 encoded json binary sequence</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="JsonException"></exception>
        public ReadOnlyJsonWebKey(ReadOnlySpan<byte> rawValue)
        {
            //Pare the raw value
            Utf8JsonReader reader = new (rawValue);
            _doc = JsonDocument.ParseValue(ref reader);
            //store element
            _jwk = _doc.RootElement;

            //Set initial values
            KeyId = _jwk.GetPropString("kid");
            KeyType = _jwk.GetPropString("kty");
            Algorithm = _jwk.GetPropString("alg");
            Use = _jwk.GetPropString("use");

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
        /// The key identifier
        /// </summary>
        public string? KeyId { get; }
        /// <summary>
        /// The key type
        /// </summary>
        public string? KeyType { get; }
        /// <summary>
        /// The key algorithm
        /// </summary>
        public string? Algorithm { get; }
        /// <summary>
        /// The key "use" value
        /// </summary>
        public string? Use { get; }

        /// <summary>
        /// Returns the JWT header that matches this key
        /// </summary>
        public IReadOnlyDictionary<string, string?> JwtHeader { get; }
       
        ///<inheritdoc/>
        public JwkKeyUsage KeyUse { get; }

        ///<inheritdoc/>
        public string? GetKeyProperty(string propertyName) => _jwk.GetPropString(propertyName);

        ///<inheritdoc/>
        protected override void Free()
        {
            _doc?.Dispose();
        }

    }
}
