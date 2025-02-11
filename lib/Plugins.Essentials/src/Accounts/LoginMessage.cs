/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: LoginMessage.cs 
*
* LoginMessage.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Text.Json.Serialization;

using VNLib.Utils.Memory;

namespace VNLib.Plugins.Essentials.Accounts
{
    /// <summary>
    /// A uniform JSON login message for the 
    /// accounts provider to use
    /// </summary>
    /// <remarks>
    /// NOTE: This class derrives from <see cref="PrivateStringManager"/>
    /// and should be disposed properly
    /// </remarks>
    public class LoginMessage : PrivateStringManager, IClientSecInfo
    {

        /// <summary>
        /// A property 
        /// </summary>
        [JsonPropertyName("username")]
        public string? UserName { get; set; }

        /// <summary>
        /// A protected string property that 
        /// may represent a user's password
        /// </summary>
        [JsonPropertyName("password")]
        public string? Password
        {
            get => base[0];
            set => base[0] = value;
        }

        [JsonPropertyName("localtime")]
        public string Lt
        {
            get => LocalTime.ToString("O");
            //Try to parse the supplied time string, and use the datetime.min if the time string is invalid
            set => LocalTime = DateTimeOffset.TryParse(value, out DateTimeOffset local) ? local : DateTimeOffset.MinValue;
        }
       
        /// <summary>
        /// Represents the clients local time in a <see cref="DateTime"/> struct
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset LocalTime { get; set; }

        /// <summary>
        /// The clients specified local-language
        /// </summary>
        [JsonPropertyName("locallanguage")]
        public string? LocalLanguage { get; set; }

        /// <summary>
        /// The clients shared public key used for encryption, this property is not protected
        /// </summary>
        [JsonPropertyName("pubkey")]
        public string? ClientPublicKey { get; set; }

        /// <summary>
        /// The clients browser id if shared
        /// </summary>
        [JsonPropertyName("clientid")]
        public string? ClientId { get; set; }

        /// <summary>
        /// Initailzies a new <see cref="LoginMessage"/> and its parent <see cref="PrivateStringManager"/> 
        /// base
        /// </summary>
        public LoginMessage() : this(1) { }

        /// <summary>
        /// Allows for derrives classes to have multple protected
        /// string elements 
        /// </summary>
        /// <param name="protectedElementSize">
        /// The number of procted string elements required
        /// </param>
        /// <remarks>
        /// NOTE: <paramref name="protectedElementSize"/> must be at-least 1
        /// or access to <see cref="Password"/> will throw
        /// </remarks>
        protected LoginMessage(int protectedElementSize = 1) : base(protectedElementSize) 
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(protectedElementSize, 1);
        }

        // This is temporary until the API can be explored futher since this
        // is an implementation of a client-side security interface
#nullable disable

        /*
         * Support client security info
         */
        string IClientSecInfo.PublicKey => ClientPublicKey;

        string IClientSecInfo.ClientId => ClientId;

#nullable enable
    }
}