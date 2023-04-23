/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: RestSiteAdapterBase.cs 
*
* RestSiteAdapterBase.cs is part of VNLib.Net.Rest.Client which is part of 
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using RestSharp;

namespace VNLib.Net.Rest.Client.Construction
{
    /// <summary>
    /// Represents a remote REST site adapter, that handles the basic housekeeping of 
    /// creating and handling operations
    /// </summary>
    public abstract class RestSiteAdapterBase : IRestSiteAdapter, IRestSiteEndpointStore
    {
        /// <summary>
        /// The collection of stored <see cref="IRestEndpointAdapter{T}"/> by their model type
        /// </summary>
        protected Dictionary<Type, object> Adapters { get; } = new();

        /// <summary>
        /// The internal client pool
        /// </summary>
        protected abstract RestClientPool Pool { get; }

        ///<inheritdoc/>
        public IRestSiteAdapter Site => this;

        ///<inheritdoc/>
        public virtual IRestEndpointAdapter<TModel> GetAdapter<TModel>() => (IRestEndpointAdapter<TModel>)Adapters[typeof(TModel)];

        ///<inheritdoc/>
        public virtual ClientContract GetClient() => Pool.Lease();

        ///<inheritdoc/>
        public abstract void OnResponse(RestResponse response);

        ///<inheritdoc/>
        public abstract Task WaitAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Adds an endpoint adapter to the store with the given model type
        /// </summary>
        /// <typeparam name="TModel">The endpoint model type</typeparam>
        /// <param name="adapter">The model typed adapter to add to the store</param>
        public virtual void AddAdapter<TModel>(IRestEndpointAdapter<TModel> adapter) => Adapters[typeof(TModel)] = adapter;
    }
}
