/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: FBMClientWorkerBase.cs 
*
* FBMClientWorkerBase.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// A base class for objects that implement <see cref="FBMClient"/>
    /// operations
    /// </summary>
    public abstract class FBMClientWorkerBase : VnDisposeable, IStatefulConnection
    {
        /// <summary>
        /// Allows configuration of websocket configuration options
        /// </summary>
        public ManagedClientWebSocket SocketOptions => Client.ClientSocket;

#nullable disable
        /// <summary>
        /// The <see cref="FBMClient"/> to sent requests from
        /// </summary>
        public FBMClient Client { get; private set; }

        /// <summary>
        /// Raised when the client has connected successfully
        /// </summary>
        public event Action<FBMClient, FBMClientWorkerBase> Connected;
#nullable enable
        
        ///<inheritdoc/>
        public event EventHandler ConnectionClosed
        {
            add => Client.ConnectionClosed += value;
            remove => Client.ConnectionClosed -= value;
        }

        /// <summary>
        /// Creates and initializes a the internal <see cref="FBMClient"/>
        /// </summary>
        /// <param name="config">The client config</param>
        protected void InitClient(in FBMClientConfig config)
        {
            Client = new(config);
            Client.ConnectionClosedOnError += Client_ConnectionClosedOnError;
            Client.ConnectionClosed += Client_ConnectionClosed;
        }

        private void Client_ConnectionClosed(object? sender, EventArgs e) => OnDisconnected();
        private void Client_ConnectionClosedOnError(object? sender, FMBClientErrorEventArgs e) => OnError(e);

        /// <summary>
        /// Asynchronously connects to a remote server by the specified uri
        /// </summary>
        /// <param name="serverUri">The remote uri of a server to connect to</param>
        /// <param name="cancellationToken">A token to cancel the connect operation</param>
        /// <returns>A task that compeltes when the client has connected to the remote server</returns>
        public virtual async Task ConnectAsync(Uri serverUri, CancellationToken cancellationToken = default)
        {
            //Connect to server
            await Client.ConnectAsync(serverUri, cancellationToken).ConfigureAwait(true);
            //Invoke child on-connected event
            OnConnected();
            Connected?.Invoke(Client, this);
        }

        /// <summary>
        /// Asynchronously disonnects a client only if the client is currently connected,
        /// returns otherwise
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>A task that compeltes when the client has disconnected</returns>
        public virtual Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            return Client.DisconnectAsync(cancellationToken);
        }

        /// <summary>
        /// Invoked when a client has successfully connected to the remote server
        /// </summary>
        protected abstract void OnConnected();
        /// <summary>
        /// Invoked when the client has disconnected cleanly
        /// </summary>
        protected abstract void OnDisconnected();
        /// <summary>
        /// Invoked when the connected client is closed because of a connection error
        /// </summary>
        /// <param name="e">A <see cref="EventArgs"/> that contains the client error data</param>
        protected abstract void OnError(FMBClientErrorEventArgs e);
        
        ///<inheritdoc/>
        protected override void Free()
        {
            Client.ConnectionClosedOnError -= Client_ConnectionClosedOnError;
            Client.ConnectionClosed -= Client_ConnectionClosed;
            Client.Dispose();
        }
    }
}
