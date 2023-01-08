/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IAlternateProtocol.cs 
*
* IAlternateProtocol.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Allows implementation for a protocol swtich from HTTP to another protocol
    /// </summary>
    public interface IAlternateProtocol
    {
        /// <summary>
        /// Initializes and executes the protocol-switch and the protocol handler
        /// that is stored
        /// </summary>
        /// <param name="transport">The prepared transport stream for the new protocol</param>
        /// <param name="handlerToken">A cancelation token that the caller may pass for operation cancelation and cleanup</param>
        /// <returns>A task that will be awaited by the server, that when complete, will cleanup resources held by the connection</returns>
        Task RunAsync(Stream transport, CancellationToken handlerToken);
    }
}