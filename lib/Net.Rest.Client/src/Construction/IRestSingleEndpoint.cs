/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: IRestSingleEndpoint.cs 
*
* IRestSingleEndpoint.cs is part of VNLib.Net.Rest.Client which is part of 
* the larger VNLib collection of libraries and utilities.
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

using RestSharp;

namespace VNLib.Net.Rest.Client.Construction
{
    /// <summary>
    /// Allows a request entity to configure its own endpoint definition
    /// </summary>
    public interface IRestSingleEndpoint
    {
        /// <summary>
        /// Gets the endpoint url to execute the REST request against
        /// </summary>
        string Url { get; }

        /// <summary>
        /// The request method type
        /// </summary>
        Method Method { get; }

        /// <summary>
        /// Allows manually configuring the request before execution
        /// </summary>
        /// <param name="request">The request to configure</param>
        void OnRequest(RestRequest request);

        /// <summary>
        /// Called when a response is received, and may be used to validate the response
        /// message.
        /// </summary>
        /// <param name="response">The received response message</param>
        void OnResponse(RestResponse response);
    }
}
