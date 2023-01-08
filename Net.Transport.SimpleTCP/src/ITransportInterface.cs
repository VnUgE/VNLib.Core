/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: ITransportInterface.cs 
*
* ITransportInterface.cs is part of VNLib.Net.Transport.SimpleTCP which is part of the larger 
* VNLib collection of libraries and utilities.
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

namespace VNLib.Net.Transport.Tcp
{
    /// <summary>
    /// Abstraction layer for TCP transport operations with
    /// sync and async support.
    /// </summary>
    interface ITransportInterface
    {
        /// <summary>
        /// Gets or sets the read timeout in milliseconds
        /// </summary>
        int RecvTimeoutMs { get; set; }

        /// <summary>
        /// Gets or set the time (in milliseconds) the transport should wait for a send operation
        /// </summary>
        int SendTimeoutMs { get; set; }

        /// <summary>
        /// Performs an asynchronous send operation
        /// </summary>
        /// <param name="data">The buffer containing the data to send to the client</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A ValueTask that completes when the send operation is complete</returns>
        ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation);

        /// <summary>
        /// Performs an asynchronous send operation
        /// </summary>
        /// <param name="buffer">The data buffer to write received data to</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A ValueTask that returns the number of bytes read into the buffer</returns>
        ValueTask<int> RecvAsync(Memory<byte> buffer, CancellationToken cancellation);
        
        /// <summary>
        /// Performs a synchronous send operation
        /// </summary>
        /// <param name="data">The buffer to send to the client</param>
        void Send(ReadOnlySpan<byte> data);

        /// <summary>
        /// Performs a synchronous receive operation
        /// </summary>
        /// <param name="buffer">The buffer to copy output data to</param>
        /// <returns>The number of bytes received</returns>
        int Recv(Span<byte> buffer);

        /// <summary>
        /// Raised when the interface is no longer required and resources
        /// related to the connection should be released.
        /// </summary>
        /// <returns>A task that resolves when the operation is complete</returns>
        Task CloseAsync();

    }
}