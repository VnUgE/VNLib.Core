/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ISessionProvider.cs 
*
* ISessionProvider.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
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

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// Provides stateful session objects assocated with HTTP connections
    /// </summary>
    public interface ISessionProvider
    {
        /// <summary>
        /// Gets a session handle for the current connection
        /// </summary>
        /// <param name="entity">The connection to get associated session on</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A task the resolves an <see cref="SessionHandle"/> instance</returns>
        /// <exception cref="TimeoutException"></exception>
        /// <exception cref="SessionException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        public ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken);
    }
}
