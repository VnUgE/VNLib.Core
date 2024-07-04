/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: ITcpListner.cs 
*
* ITcpListner.cs is part of VNLib.Net.Transport.SimpleTCP which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.SimpleTCP is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.SimpleTCP is distributed in the hope that it will be useful,
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

using VNLib.Utils.Memory.Caching;

namespace VNLib.Net.Transport.Tcp
{
    /// <summary>
    /// An immutable TCP listening instance that has been configured to accept incoming 
    /// connections from a <see cref="TcpServer"/> instance
    /// </summary>
    public interface ITcpListner : ICacheHolder
    {
        /// <summary>
        /// Accepts a connection and returns the connection descriptor.
        /// </summary>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The connection descriptor</returns>
        /// <remarks>
        /// NOTE: You must always call the <see cref="CloseConnectionAsync"/> and 
        /// destroy all references to it when you are done. You must also dispose the stream returned
        /// from the <see cref="ITcpConnectionDescriptor.GetStream"/> method.
        /// </remarks>
        /// <exception cref="InvalidOperationException"></exception>
        ValueTask<ITcpConnectionDescriptor> AcceptConnectionAsync(CancellationToken cancellation);

        /// <summary>
        /// Cleanly closes an existing TCP connection obtained from <see cref="AcceptConnectionAsync(CancellationToken)"/>
        /// and returns the instance to the pool for reuse. 
        /// <para>
        /// If you set <paramref name="reuse"/> to true, the server will attempt to reuse the descriptor instance, you 
        /// must ensure that all previous references to the descriptor are destroyed. If the value is false, resources 
        /// are freed and the instance is disposed.
        /// </para>
        /// </summary>
        /// <param name="descriptor">The existing descriptor to close</param>
        /// <param name="reuse">A value that indicates if the server can safley reuse the descriptor instance</param>
        /// <returns>A task that represents the closing operations</returns>
        /// <exception cref="ArgumentNullException"></exception>
        ValueTask CloseConnectionAsync(ITcpConnectionDescriptor descriptor, bool reuse);

        /// <summary>
        /// Stops the listener loop and attempts to cleanup all resources,
        /// you should consider waiting for <see cref="WaitForExitAsync"/>
        /// before disposing the listener.
        /// </summary>
        void Close();

        /// <summary>
        /// Waits for all listening threads to exit before completing the task
        /// </summary>
        /// <returns>A task that completes when all listening threads exit</returns>
        Task WaitForExitAsync();
    }
}