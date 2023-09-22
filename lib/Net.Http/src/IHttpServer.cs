/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: IHttpServer.cs 
*
* IHttpServer.cs is part of VNLib.Net.Http which is part of the larger 
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

using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Net.Http
{
    /// <summary>
    /// Provides an HTTP based application layer protocol server
    /// </summary>
    public interface IHttpServer
    {
        /// <summary>
        /// Gets a value indicating whether the server is listening for connections
        /// </summary>
        bool Running { get; }

        /// <summary>
        /// Begins listening for connections on configured interfaces for configured hostnames.
        /// </summary>
        /// <param name="cancellationToken">A token used to stop listening for incomming connections and close all open websockets</param>
        /// <returns>A task that resolves when the server has exited</returns>
        Task Start(CancellationToken cancellationToken);
    }
}