/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: IAsyncAccessSerializer.cs 
*
* IAsyncAccessSerializer.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Utils.Async
{
    /// <summary>
    /// A mutual exclusion primitive that provides asynchronous waits for serialized 
    /// access to a resource based on a moniker. Similar to the <see cref="Monitor"/> 
    /// class.
    /// </summary>
    /// <typeparam name="TMoniker">The moniker type, the unique token identifying the wait</typeparam>
    public interface IAsyncAccessSerializer<TMoniker>
    {
        /// <summary>
        /// Provides waiting for exclusive access identified 
        /// by the supplied moniker
        /// </summary>
        /// <param name="moniker">The moniker used to identify the wait</param>
        /// <param name="cancellation">A token to cancel the async wait operation</param>
        /// <returns>A task that completes when the wait identified by the moniker is released</returns>
        Task WaitAsync(TMoniker moniker, CancellationToken cancellation = default);

        /// <summary>
        /// Completes the exclusive access identified by the moniker
        /// </summary>
        /// <param name="moniker">The moniker used to identify the wait to release</param>
        void Release(TMoniker moniker);
    }
}