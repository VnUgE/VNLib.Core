/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: OauthHttpExtensions.cs 
*
* OauthHttpExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Net;

using VNLib.Net.Http;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Extensions;

namespace VNLib.Plugins.Essentials.Oauth
{
    /// <summary>
    /// An OAuth2 specification error code
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// The request is considered invalid and cannot be continued
        /// </summary>
        InvalidRequest,
        /// <summary>
        /// 
        /// </summary>
        InvalidClient,
        /// <summary>
        /// The supplied token is no longer considered valid
        /// </summary>
        InvalidToken,
        /// <summary>
        /// The token does not have the authorization required, is missing authorization, or is no longer considered acceptable
        /// </summary>
        UnauthorizedClient,
        /// <summary>
        /// The client accept content type is unacceptable for the requested endpoint and cannot be processed
        /// </summary>
        UnsupportedResponseType,
        /// <summary>
        /// The scope of the token does not allow for this operation
        /// </summary>
        InvalidScope,
        /// <summary>
        /// There was a server related error and the request could not be fulfilled 
        /// </summary>
        ServerError,
        /// <summary>
        /// The request could not be processed at this time
        /// </summary>
        TemporarilyUnavailable
    }

    public static class OauthHttpExtensions
    {
        /// <summary>
        /// Closes the current response with a json error message with the message details
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="code">The http status code</param>
        /// <param name="error">The short error</param>
        /// <param name="description">The error description message</param>
        public static void CloseResponseError(this IHttpEvent ev, HttpStatusCode code, ErrorType error, ReadOnlySpan<char> description)
        {
            //See if the response accepts json
            if (ev.Server.Accepts(ContentType.Json))
            {
                //Alloc char buffer to write output to, nearest page should give us enough room
                using UnsafeMemoryHandle<char> buffer = MemoryUtil.UnsafeAllocNearestPage<char>(description.Length + 64);
                ForwardOnlyWriter<char> writer = new(buffer.Span);

                //Build the error message string
                writer.Append("{\"error\":\"");
                switch (error)
                {
                    case ErrorType.InvalidRequest:
                        writer.Append("invalid_request");
                        break;
                    case ErrorType.InvalidClient:
                        writer.Append("invalid_client");
                        break;
                    case ErrorType.UnauthorizedClient:
                        writer.Append("unauthorized_client");
                        break;
                    case ErrorType.InvalidToken:
                        writer.Append("invalid_token");
                        break;
                    case ErrorType.UnsupportedResponseType:
                        writer.Append("unsupported_response_type");
                        break;
                    case ErrorType.InvalidScope:
                        writer.Append("invalid_scope");
                        break;
                    case ErrorType.ServerError:
                        writer.Append("server_error");
                        break;
                    case ErrorType.TemporarilyUnavailable:
                        writer.Append("temporarily_unavailable");
                        break;
                    default:
                        writer.Append("error");
                        break;
                }
                writer.Append("\",\"error_description\":\"");
                writer.Append(description);
                writer.Append("\"}");

                //Close the response with the json data
                ev.CloseResponse(code, ContentType.Json, writer.AsSpan());
            }
            //Otherwise set the error code in the wwwauth header
            else
            {
                //Set the error result in the header
                ev.Server.Headers[HttpResponseHeader.WwwAuthenticate] = error switch
                {
                    ErrorType.InvalidRequest => "Bearer error=\"invalid_request\"",
                    ErrorType.UnauthorizedClient => "Bearer error=\"unauthorized_client\"",
                    ErrorType.UnsupportedResponseType => "Bearer error=\"unsupported_response_type\"",
                    ErrorType.InvalidScope => "Bearer error=\"invalid_scope\"",
                    ErrorType.ServerError => "Bearer error=\"server_error\"",
                    ErrorType.TemporarilyUnavailable => "Bearer error=\"temporarily_unavailable\"",
                    ErrorType.InvalidClient => "Bearer error=\"invalid_client\"",
                    ErrorType.InvalidToken => "Bearer error=\"invalid_token\"",
                    _ => "Bearer error=\"error\"",
                };
                //Close the response with the status code
                ev.CloseResponse(code);
            }
        }
    }
}