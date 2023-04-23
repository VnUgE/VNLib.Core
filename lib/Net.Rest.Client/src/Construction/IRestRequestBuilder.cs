/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: IRestRequestBuilder.cs 
*
* IRestRequestBuilder.cs is part of VNLib.Net.Rest.Client which is part of 
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

using System;

using RestSharp;

namespace VNLib.Net.Rest.Client.Construction
{
    /// <summary>
    /// A type used to define operations required to generate requests to a REST endpoint
    /// </summary>
    /// <typeparam name="TModel">The request entity/model type</typeparam>
    public interface IRestRequestBuilder<TModel>
    {
        /// <summary>
        /// Defines a callback method that gets the request url for a given endpoint
        /// </summary>
        /// <param name="uriBuilder"></param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        IRestRequestBuilder<TModel> WithUrl(Func<TModel, string> uriBuilder);

        /// <summary>
        /// Adds a request message handler/converter, callback method used to modify 
        /// the request message programatically.
        /// </summary>
        /// <param name="requestBuilder"></param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        IRestRequestBuilder<TModel> WithModifier(Action<TModel, RestRequest> requestBuilder);

        /// <summary>
        /// Adds a response handler callback method that will be invoked when a response 
        /// is received from the endpoint. This method may be used to validate a response message
        /// </summary>
        /// <param name="onResponseBuilder"></param>
        /// <returns>The chainable <see cref="IRestRequestBuilder{TModel}"/></returns>
        IRestRequestBuilder<TModel> OnResponse(Action<TModel, RestResponse> onResponseBuilder);
    }
}
