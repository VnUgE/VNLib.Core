/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: ITransportProvider.cs 
*
* ITransportProvider.cs is part of VNLib.Net.Http which is part of the larger 
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
    /// Listens for network connections and captures the information 
    /// required for application processing
    /// </summary>
    public interface ITransportProvider
    {
        /// <summary>
        /// Begins listening for connections (binds a socket if necessary) and is 
        /// called before the server begins listening for connections.
        /// </summary>
        /// <param name="stopToken">A token that is cancelled when the server is closed</param>
        void Start(CancellationToken stopToken);

        /// <summary>
        /// Waits for a new connection to be established and returns its context. This method 
        /// should only return an established connection (ie: connected socket).
        /// </summary>
        /// <param name="cancellation">A token to cancel the wait operation</param>
        /// <returns>A <see cref="ValueTask"/> that returns an established connection</returns>
        ValueTask<ITransportContext> AcceptAsync(CancellationToken cancellation);
    }
}