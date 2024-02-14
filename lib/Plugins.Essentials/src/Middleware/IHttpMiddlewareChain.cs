/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: IHttpMiddlewareChain.cs 
*
* IHttpMiddlewareChain.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

using System.Collections.Generic;

namespace VNLib.Plugins.Essentials.Middleware
{
    /// <summary>
    /// Represents a chain of <see cref="IHttpMiddleware"/> instances that 
    /// will be called by an <see cref="EventProcessor"/> during 
    /// entity processing.
    /// </summary>
    public interface IHttpMiddlewareChain
    {
        /// <summary>
        /// Gets the current head of the middleware chain
        /// </summary>
        /// <returns>A <see cref="LinkedListNode{T}"/> that points to the head of the current chain</returns>
        LinkedListNode<IHttpMiddleware>? GetCurrentHead();

        /// <summary>
        /// Adds a middleware handler to the end of the chain
        /// </summary>
        /// <param name="middleware">The middleware processor to add</param>
        void Add(IHttpMiddleware middleware);

        /// <summary>
        /// Removes a middleware handler from the chain
        /// </summary>
        /// <param name="middleware">The middleware instance to remove</param>
        void Remove(IHttpMiddleware middleware);

        /// <summary>
        /// Removes all middleware handlers from the chain
        /// </summary>
        void Clear();
    }
}