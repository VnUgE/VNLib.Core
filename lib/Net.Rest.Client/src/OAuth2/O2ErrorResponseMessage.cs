/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: O2ErrorResponseMessage.cs 
*
* O2ErrorResponseMessage.cs is part of VNLib.Net.Rest.Client which is part of the larger 
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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VNLib.Net.Rest.Client.OAuth2
{
    /// <summary>
    /// An OAuth2 standard error message
    /// </summary>
    public class O2ErrorResponseMessage
    {
        /// <summary>
        /// The OAuth2 error code
        /// </summary>
        [JsonPropertyName("error_code")]
        public string ErrorCode { get; set; }
        
        /// <summary>
        /// The OAuth2 human readable error description
        /// </summary>
        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; }
        /// <summary>
        /// Initializes a new <see cref="O2ErrorResponseMessage"/>
        /// </summary>
        public O2ErrorResponseMessage()
        {}
        /// <summary>
        /// Initializes a new <see cref="O2ErrorResponseMessage"/>
        /// </summary>
        /// <param name="code">The OAuth2 error code</param>
        /// <param name="description">The OAuth2 error description</param>
        public O2ErrorResponseMessage(string code, string description)
        {
            ErrorCode = code;
            ErrorDescription = description;
        }

        /// <summary>
        /// Initializes a new <see cref="O2ErrorResponseMessage"/> instance
        /// from a <see cref="JsonElement"/> error element
        /// </summary>
        /// <param name="el">An error element that represens the error</param>
        public O2ErrorResponseMessage(ref JsonElement el)
        {
            if(el.TryGetProperty("error_code", out JsonElement code))
            {
                this.ErrorCode = code.GetString();
            }
            if (el.TryGetProperty("error_code", out JsonElement description))
            {
                this.ErrorDescription = description.GetString();
            }
        }
    }
}
