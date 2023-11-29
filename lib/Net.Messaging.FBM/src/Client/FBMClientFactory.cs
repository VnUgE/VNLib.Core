/*
* Copyright (c) 2023 Vaughn Nugent
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
        /// <exception cref="ArgumentNullException"></exception>
        public FBMClientFactory(in FBMClientConfig config, IFbmWebsocketFactory webSocketManager)
        {
            _config = config;
            _ = config.MemoryManager ?? throw new ArgumentException("The client memory manager must not be null", nameof(config));
            _socketMan = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
            _internalRequestPool = ObjectRental.CreateReusable(ReuseableRequestConstructor, 1000);
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
            //Init new socket
            IFbmClientWebsocket socket = _socketMan.CreateWebsocket(in _config);
            
            //Create client wrapper
            return new(in _config, socket, _internalRequestPool);
        }

        ///<inheritdoc/>
        public void CacheClear() => _internalRequestPool.CacheClear();

        ///<inheritdoc/>
        public void CacheHardClear() => _internalRequestPool.CacheHardClear();
    }
}
