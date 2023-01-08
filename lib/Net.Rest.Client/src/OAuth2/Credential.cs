/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: Credential.cs 
*
* Credential.cs is part of VNLib.Net.Rest.Client which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Rest.Client is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Net.Rest.Client is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Net.Rest.Client. If not, see http://www.gnu.org/licenses/.
*/

using System;

using VNLib.Utils.Memory;

namespace VNLib.Net.Rest.Client.OAuth2
{
    /// <summary>
    /// Creates a disposeable "protected" credential object.
    /// </summary>
    /// <remarks>
    /// Properties used after the instance has been disposed are
    /// undefined
    /// </remarks>
    public class Credential : PrivateStringManager
    {
        /// <summary>
        /// The credential username parameter
        /// </summary>
        public string UserName => this[0];
        /// <summary>
        /// The credential's password 
        /// </summary>
        public string Password => this[1];

        private Credential(string uname, string pass) : base(2)
        {
            this[0] = uname;
            this[1] = pass;
        }

        /// <summary>
        /// Creates a new protected <see cref="Credential"/> 
        /// that owns the memory to the supplied strings
        /// </summary>
        /// <param name="username">The username string to consume</param>
        /// <param name="password">The password string to consume</param>
        /// <returns>A new protected credential object</returns>
        public static Credential CreateUnsafe(in string username, in string password)
        {
            return new Credential(username, password);
        }
        /// <summary>
        /// Creates a new "safe" <see cref="Credential"/> by copying 
        /// the values of the supplied credential properites
        /// </summary>
        /// <param name="username">The username value to copy</param>
        /// <param name="password">The password value to copy</param>
        /// <returns>A new protected credential object</returns>
        public static Credential Create(ReadOnlySpan<char> username, ReadOnlySpan<char> password)
        {
            return new Credential(username.ToString(), password.ToString());
        }
    }
}
