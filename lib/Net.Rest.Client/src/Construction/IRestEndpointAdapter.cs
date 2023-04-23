/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: IRestEndpointAdapter.cs 
*
* IRestEndpointAdapter.cs is part of VNLib.Net.Rest.Client which is 
* part of the larger VNLib collection of libraries and utilities.
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
    /// Represents a remote http endpoint that can create requests given an entity
    /// to communicate with the remote endpoint.
    /// </summary>
    /// <typeparam name="TEntity">The request entity model</typeparam>
    public interface IRestEndpointAdapter<TEntity>
    {
        /// <summary>
        /// Gets a new <see cref="RestRequest"/> for the given request entity/arguments
        /// used to make a request against a rest endpoint
        /// </summary>
        /// <param name="entity">The entity to get the new <see cref="RestRequest"/> for</param>
        /// <returns>The configured <see cref="RestRequest"/> to send to the endpoint</returns>
        RestRequest GetRequest(TEntity entity);

        /// <summary>
        /// Called when a request has successfully completed, may be used to validate a response message
        /// </summary>
        /// <param name="model">The original request entity that was sent</param>
        /// <param name="response">The response message that was received</param>
        void OnResponse(TEntity model, RestResponse response);
    }
}
