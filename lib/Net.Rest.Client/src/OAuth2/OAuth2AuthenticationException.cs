/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: OAuth2AuthenticationException.cs 
*
* OAuth2AuthenticationException.cs is part of VNLib.Net.Rest.Client which is part of the larger 
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
using System.Net;
using System.Security.Authentication;

using RestSharp;

namespace VNLib.Net.Rest.Client.OAuth2
{
    /// <summary>
    /// Raised when a token request made to the authorization endpoint 
    /// fails. Inner exceptions may be set if the response succeeds but
    /// returned invalid data
    /// </summary>
    public class OAuth2AuthenticationException : AuthenticationException
    {
        /// <summary>
        /// The status code returned from the request
        /// </summary>
        public HttpStatusCode StatusCode => ErrorResponse?.StatusCode ?? 0;
        /// <summary>
        /// The string representation of the response that was received by the token request
        /// </summary>
        public RestResponse ErrorResponse { get; }
        ///<inheritdoc/>
        public OAuth2AuthenticationException()
        { }
        ///<inheritdoc/>
        public OAuth2AuthenticationException(string message) : base(message)
        { }
        ///<inheritdoc/>
        public OAuth2AuthenticationException(string message, Exception innerException) : base(message, innerException)
        { }
        /// <summary>
        /// Initializes a new <see cref="OAuth2AuthenticationException"/> with the 
        /// specified server response
        /// </summary>
        /// <param name="response">The response containing the error result</param>
        public OAuth2AuthenticationException(RestResponse response) : base()
        {
            ErrorResponse = response;
        }
        /// <summary>
        /// Initializes a new <see cref="OAuth2AuthenticationException"/> with the 
        /// specified server response
        /// </summary>
        /// <param name="response">The response containing the error result</param>
        /// <param name="innerException">An inner excepion that caused the authentication to fail</param>
        public OAuth2AuthenticationException(RestResponse response, Exception innerException) : base(null, innerException)
        {
            ErrorResponse = response;
        }
    }
}
