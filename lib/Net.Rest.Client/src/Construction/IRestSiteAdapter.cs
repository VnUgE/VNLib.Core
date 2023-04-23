/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: IRestSiteAdapter.cs 
*
* IRestSiteAdapter.cs is part of VNLib.Net.Rest.Client which is part of 
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

using System.Threading;
using System.Threading.Tasks;

using RestSharp;

namespace VNLib.Net.Rest.Client.Construction
{
    /// <summary>
    /// Represents a remote REST api that defines endpoints to execute request/response 
    /// operations against, and configures RestClient's for use.
    /// </summary>
    public interface IRestSiteAdapter
    {
        /// <summary>
        /// May be called during request execution to pause an operation to avoid 
        /// resource exhaustion and avoid dropped connections from overload
        /// </summary>
        /// <param name="cancellation"></param>
        /// <returns>A task that will be awaited, when complete will continue the operation</returns>
        Task WaitAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Gets a new <see cref="ClientContract"/> that has a properly configured <see cref="RestClient"/>
        /// </summary>
        /// <returns>The new <see cref="ClientContract"/></returns>
        ClientContract GetClient();

        /// <summary>
        /// Gets an <see cref="IRestEndpointAdapter{TEntity}"/> for the given request entity type
        /// </summary>
        /// <typeparam name="TModel">The request entity model</typeparam>
        /// <returns>The <see cref="IRestEndpointAdapter{TEntity}"/> used to build a request from an entity</returns>
        IRestEndpointAdapter<TModel> GetAdapter<TModel>();

        /// <summary>
        /// Called after every successful REST operation.
        /// </summary>
        /// <param name="response">The response message returned from any endpoint</param>
        void OnResponse(RestResponse response);
    }
}
