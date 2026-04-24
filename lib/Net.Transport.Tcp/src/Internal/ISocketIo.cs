/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.Tcp
* File: ISocketIo.cs 
*
* ISocketIo.cs is part of VNLib.Net.Transport.Tcp which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.Tcp is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.Tcp is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Net.Sockets;
using System.Threading.Tasks;



namespace VNLib.Net.Transport.Tcp.Internal
{
    /// <summary>
    /// Defines low-level asynchronous send and receive operations over a connected socket,
    /// used internally by the pipeline worker tasks.
    /// </summary>
    internal interface ISocketIo
    {
        /// <summary>
        /// Sends data to the remote endpoint asynchronously.
        /// </summary>
        /// <param name="buffer">The buffer containing the data to send.</param>
        /// <param name="socketFlags">A bitwise combination of the enumeration values that specifies the send behavior.</param>
        /// <returns>The number of bytes sent to the remote endpoint.</returns>
        ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags);

        /// <summary>
        /// Receives data from the remote endpoint asynchronously.
        /// </summary>
        /// <param name="buffer">The buffer into which received data is written.</param>
        /// <param name="socketFlags">A bitwise combination of the enumeration values that specifies the receive behavior.</param>
        /// <returns>The number of bytes received from the remote endpoint.</returns>
        ValueTask<int> ReceiveAsync(Memory<byte> buffer, SocketFlags socketFlags);
    }
}
