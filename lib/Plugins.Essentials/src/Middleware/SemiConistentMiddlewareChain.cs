/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: SemiConistentMiddlewareChain.cs 
*
* SemiConistentMiddlewareChain.cs is part of VNLib.Plugins.Essentials which 
* is part of the larger VNLib collection of libraries and utilities.
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

using System.Reflection;
using System.Collections.Generic;

namespace VNLib.Plugins.Essentials.Middleware
{
    /// <summary>
    /// A default implementation of <see cref="IHttpMiddlewareChain"/> that
    /// maintains a semi-conistant chain of middleware handlers, for infrequent 
    /// chain updates
    /// </summary>
    internal sealed class SemiConistentMiddlewareChain : IHttpMiddlewareChain
    {
        private LinkedList<IHttpMiddleware> _middlewares = new();

        ///<inheritdoc/>
        public void Add(IHttpMiddleware middleware)
        {
            //Get security critical flag
            bool isSecCritical = middleware.GetType().GetCustomAttribute<MiddlewareImplAttribute>()
                ?.ImplOptions.HasFlag(MiddlewareImplOptions.SecurityCritical) ?? false;

            lock (_middlewares)
            {
                //Always add security critical middleware to the front of the chain
                if (isSecCritical)
                {
                    _middlewares.AddFirst(middleware);
                }
                else
                {
                    _middlewares.AddLast(middleware);
                }
            }
        }

        ///<inheritdoc/>
        public void Clear()
        {
            lock (_middlewares)
            {
                _middlewares.Clear();
            }
        }

        ///<inheritdoc/>
        public LinkedListNode<IHttpMiddleware>? GetCurrentHead() => _middlewares.First;

        ///<inheritdoc/>
        public void RemoveMiddleware(IHttpMiddleware middleware)
        {
            lock (_middlewares)
            {
                //Clone current table
                LinkedList<IHttpMiddleware> newTable = new(_middlewares);

                //Remove the middleware
                newTable.Remove(middleware);

                //Replace the current table with the new one
                _middlewares = newTable;
            }
        }
    }
}