/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: MiddlewareImplAttribute.cs 
*
* MiddlewareImplAttribute.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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

namespace VNLib.Plugins.Essentials.Middleware
{
    /// <summary>
    /// Specifies optional implementation flags for a middleware instance
    /// that loaders may use during soriting of the middleware chain
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MiddlewareImplAttribute : Attribute
    {
        /// <summary>
        /// The option flags for a middleware instance
        /// </summary>
        public MiddlewareImplOptions ImplOptions { get; }

        /// <summary>
        /// Creates a new <see cref="MiddlewareImplAttribute"/> instance
        /// with the specified <see cref="MiddlewareImplOptions"/>
        /// </summary>
        /// <param name="implOptions">Implementation option flags</param>
        public MiddlewareImplAttribute(MiddlewareImplOptions implOptions) => ImplOptions = implOptions;
    }
}
