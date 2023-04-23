/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: IRestEndpointBuilder.cs 
*
* IRestEndpointBuilder.cs is part of VNLib.Net.Rest.Client which is part of 
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

namespace VNLib.Net.Rest.Client.Construction
{
    /// <summary>
    /// Represents an object used to define endpoint adapters for a <see cref="IRestSiteAdapter"/>
    /// </summary>
    public interface IRestEndpointBuilder
    {
        /// <summary>
        /// Creates a new endpoint adapter of the given entity model type and gets a <see cref="IRestRequestBuilder{TModel}"/>
        /// that may be used to configure the request message.
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <returns>A chainable <see cref="IRestRequestBuilder{TModel}"/> used to define your request</returns>
        IRestRequestBuilder<TModel> WithEndpoint<TModel>();
    }
}
