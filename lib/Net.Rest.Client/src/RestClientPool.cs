/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Rest.Client
* File: RestClientPool.cs 
*
* RestClientPool.cs is part of VNLib.Net.Rest.Client which is part of the larger 
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

using RestSharp;
using RestSharp.Authenticators;

using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Rest.Client
{
    /// <summary>
    /// Maintains a pool of lazy loaded <see cref="RestClient"/> instances to allow for concurrent client usage
    /// </summary>
    public class RestClientPool : ObjectRental<RestClient>
    {
        /// <summary>
        /// Creates a new <see cref="RestClientPool"/> instance and creates the specified
        /// number of clients with the same number of concurrency.
        /// </summary>
        /// <param name="maxClients">The maximum number of clients to create and authenticate, should be the same as the number of maximum allowed tokens</param>
        /// <param name="options">A <see cref="RestClientOptions"/> used to initialze the pool of clients</param>
        /// <param name="authenticator">An optional authenticator for clients to use</param>
        /// <param name="initCb">An optional client initialzation callback</param>
        public RestClientPool(int maxClients, RestClientOptions options, Action<RestClient>? initCb = null, IAuthenticator? authenticator = null)
            : base(() =>
            {
                //Add optional authenticator
                options.Authenticator = authenticator;

                //load client
                RestClient client = new(options);

                //Invoke init callback
                initCb?.Invoke(client);
                return client;
            }, null, null, maxClients)
        {
        }

        /// <summary>
        /// Obtains a new <see cref="ClientContract"/> for a reused, or new, <see cref="RestClient"/> instance
        /// </summary>
        /// <returns>The contract that manages the client</returns>
        public ClientContract Lease() => new(base.Rent(), this);
    }
}
