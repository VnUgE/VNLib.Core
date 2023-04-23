/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: IRestSiteEndpointStore.cs 
*
* IRestSiteEndpointStore.cs is part of VNLib.Net.Rest.Client which is part of 
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
    /// Represents a type used to store <see cref="IRestEndpointAdapter{TEntity}"/> instances
    /// for a <see cref="IRestSiteAdapter"/>
    /// </summary>
    public interface IRestSiteEndpointStore
    {
        /// <summary>
        /// The <see cref="IRestSiteAdapter"/> that adapters will be stored in
        /// </summary>
        IRestSiteAdapter Site { get; }

        /// <summary>
        /// Adds an adapter for the given request entity type to be used fore
        /// REST operations from the given entity model.
        /// </summary>
        /// <typeparam name="TModel">The request entity model for the endpoint</typeparam>
        /// <param name="adapter">The adapter to add to the store</param>
        void AddAdapter<TModel>(IRestEndpointAdapter<TModel> adapter);
    }
}
