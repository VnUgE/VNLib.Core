/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMClientFactory.cs 
*
* FBMClientFactory.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Messaging.FBM is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Messaging.FBM is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// An FBMClient factory that creates immutable clients from fbm 
    /// websockets
    /// </summary>
    public sealed class FBMClientFactory: ICacheHolder
    {
        private readonly ObjectRental<FBMRequest> _internalRequestPool;
        private readonly FBMClientConfig _config;
        private readonly IFbmWebsocketFactory _socketMan;

        /// <summary>
        /// Initlaizes a new client factory from the websocket manager
        /// </summary>
        /// <param name="config">The configuration state</param>
        /// <param name="webSocketManager">The client websocket factory</param>
        /// <param name="maxClients">The maximum number of clients expected to be connected concurrently</param>
        /// <exception cref="ArgumentNullException"></exception>
        public FBMClientFactory(ref readonly FBMClientConfig config, IFbmWebsocketFactory webSocketManager, int maxClients)
        {
            ArgumentNullException.ThrowIfNull(config.MemoryManager, nameof(config.MemoryManager));
            ArgumentNullException.ThrowIfNull(webSocketManager);

            _config = config;
            _socketMan = webSocketManager;

            /*
             * Create a shared pool of reusable FBMRequest objects
             * for all clients to share. It helps cut down on total 
             * memory, and keeps pools alive if clients are created
             * and destroyed frequently. It also allows this factory
             * to manage the cache for all created clients.
             */
            _internalRequestPool = ObjectRental.CreateReusable(ReuseableRequestConstructor, maxClients);
        }

        /// <summary>
        /// The configuration for the current client
        /// </summary>
        public ref readonly FBMClientConfig Config => ref _config;

        /// <summary>
        /// Allocates and configures a new <see cref="FBMRequest"/> message object for use within the reusable store
        /// </summary>
        /// <returns>The configured <see cref="FBMRequest"/></returns>
        private FBMRequest ReuseableRequestConstructor() => new(in _config);

        /// <summary>
        /// Initializes a new websocket and creates a new <see cref="FBMClient"/> instance
        /// </summary>
        /// <returns>The initialized FBM client instance</returns>
        public FBMClient CreateClient()
        {  
            return new(
                config: in _config, 
                websocket: _socketMan.CreateWebsocket(in _config), 
                requestRental: _internalRequestPool
            );
        }

        ///<inheritdoc/>
        public void CacheClear() => _internalRequestPool.CacheClear();

        ///<inheritdoc/>
        public void CacheHardClear() => _internalRequestPool.CacheHardClear();
    }
}
