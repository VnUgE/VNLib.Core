/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IVirtualEndpointTable.cs 
*
* IVirtualEndpointTable.cs is part of VNLib.Plugins.Essentials which is part 
* of the larger VNLib collection of libraries and utilities.
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

using VNLib.Plugins.Essentials.Endpoints;

#nullable enable

namespace VNLib.Plugins.Essentials
{
    /// <summary>
    /// Represents a table of virtual endpoints that can be used to process incoming connections
    /// </summary>
    public interface IVirtualEndpointTable
    {
        /// <summary>
        /// Determines the endpoint type(s) and adds them to the endpoint store(s) as necessary
        /// </summary>
        /// <param name="endpoints">Params array of endpoints to add to the store</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        void AddEndpoint(params IEndpoint[] endpoints);

        /// <summary>
        /// Removes the specified endpoint from the virtual endpoint store
        /// </summary>
        /// <param name="eps">A collection of endpoints to remove from the table</param>
        void RemoveEndpoint(params IEndpoint[] eps);

        /// <summary>
        /// Stops listening for connections to the specified <see cref="IVirtualEndpoint{T}"/> identified by its path
        /// </summary>
        /// <param name="paths">An array of endpoint paths to remove from the table</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        void RemoveEndpoint(params string[] paths);

        /// <summary>
        /// A value that indicates whether the table is empty, allows for quick checks
        /// without causing lookups
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Attempts to get the endpoint associated with the specified path
        /// </summary>
        /// <param name="path">The connection path to recover the endpoint from</param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        bool TryGetEndpoint(string path, out IVirtualEndpoint<HttpEntity>? endpoint);
    }
}