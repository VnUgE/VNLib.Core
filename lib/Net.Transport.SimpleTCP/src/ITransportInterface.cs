/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Buffers;
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
        /// Performs an asynchronous send operation
        /// </summary>
        /// <param name="data">The buffer containing the data to send to the client</param>
        /// <param name="timeout">The timeout in milliseconds</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A ValueTask that completes when the send operation is complete</returns>
        ValueTask SendAsync(ReadOnlyMemory<byte> data, int timeout, CancellationToken cancellation);

        /// <summary>
        /// Performs an asynchronous send operation
        /// </summary>
        /// <param name="buffer">The data buffer to write received data to</param>
        /// <param name="timeout">The timeout in milliseconds</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A ValueTask that returns the number of bytes read into the buffer</returns>
        ValueTask<int> RecvAsync(Memory<byte> buffer, int timeout, CancellationToken cancellation);

        /// <summary>
        /// Performs a synchronous send operation
        /// </summary>
        /// <param name="timeout">The timeout in milliseconds</param>
        /// <param name="data">The buffer to send to the client</param>
        void Send(ReadOnlySpan<byte> data, int timeout);

        /// <summary>
        /// Performs a synchronous receive operation
        /// </summary>
        /// <param name="timeout">The timeout in milliseconds</param>
        /// <param name="buffer">The buffer to copy output data to</param>
        /// <returns>The number of bytes received</returns>
        int Recv(Span<byte> buffer, int timeout);

        /// <summary>
        /// Gets as transport buffer writer for more effecient writes
        /// </summary>
        IBufferWriter<byte> SendBuffer { get; }

        /// <summary>
        /// Flushes the send buffer
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="cancellation"></param>
        /// <returns>A task that completes when pending write data has been sent</returns>
        ValueTask FlushSendAsync(int timeout, CancellationToken cancellation);
    }
}