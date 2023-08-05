/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: JwtPayload.cs 
*
* JwtPayload.cs is part of VNLib.Hashing.Portable which is part of the larger 
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

using System.Collections.Generic;

using VNLib.Utils;

namespace VNLib.Hashing.IdentityUtility
{
    /// <summary>
    /// A fluent api structure for adding and committing claims to a <see cref="JsonWebToken"/>
    /// </summary>
    public readonly record struct JwtPayload : IIndexable<string, object>
    {
        private readonly Dictionary<string, object> Claims;
        private readonly JsonWebToken Jwt;

        internal JwtPayload(JsonWebToken jwt, int initialCapacity)
        {
            Jwt = jwt;
            Claims = new(initialCapacity);
        }

        ///<inheritdoc/>
        public readonly object this[string key]
        {
            get => Claims[key];
            set => Claims[key] = value;
        }

        /// <summary>
        /// Adds a claim name-value pair to the store
        /// </summary>
        /// <param name="claim">The clame name</param>
        /// <param name="value">The value of the claim</param>
        /// <returns>The chained response object</returns>
        public readonly JwtPayload AddClaim(string claim, object value)
        {
            Claims.Add(claim, value);
            return this;
        }

        /// <summary>
        /// Writes all claims to the <see cref="JsonWebToken"/> payload segment
        /// </summary>
        public readonly void CommitClaims()
        {
            Jwt.WritePayload(Claims);
            Claims.Clear();
        }
    }
}
