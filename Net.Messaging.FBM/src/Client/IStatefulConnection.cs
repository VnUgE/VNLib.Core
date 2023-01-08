/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Messaging.FBM
* File: IStatefulConnection.cs 
*
* IStatefulConnection.cs is part of VNLib.Net.Messaging.FBM which is part of the larger 
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

namespace VNLib.Net.Messaging.FBM.Client
{
    /// <summary>
    /// An abstraction for a stateful connection client that reports its status
    /// </summary>
    public interface IStatefulConnection
    {
        /// <summary>
        /// An event that is raised when the connection state has transition from connected to disconnected
        /// </summary>
        event EventHandler ConnectionClosed;
        /// <summary>
        /// Connects the client to the remote resource
        /// </summary>
        /// <param name="serverUri">The resource location to connect to</param>
        /// <param name="cancellationToken">A token to cancel the connect opreation</param>
        /// <returns>A task that compeltes when the connection has succedded</returns>
        Task ConnectAsync(Uri serverUri, CancellationToken cancellationToken = default);
        /// <summary>
        /// Gracefully disconnects the client from the remote resource
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the disconnect operation</param>
        /// <returns>A task that completes when the client has been disconnected</returns>
        Task DisconnectAsync(CancellationToken cancellationToken = default);
    }
}
