/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IServiceHost.cs 
*
* IServiceHost.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.ServiceStack is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Represents an HTTP service host which provides information required 
    /// for HttpServer routing and the <see cref="IWebRoot"/> for processing
    /// incoming connections
    /// </summary>
    public interface IServiceHost
    {
        /// <summary>
        /// The <see cref="IWebRoot"/> that handles HTTP connection 
        /// processing.
        /// </summary>
        IWebRoot Processor { get; }

        /// <summary>
        /// Optional user state to be set during initialization and read at a later time
        /// </summary>
        object? UserState { get; }

        /// <summary>
        /// Called when an <see cref="IHttpServiceBinding"/> is being attached to this host, 
        /// allowing the host to resolve and register the binding's endpoints, middleware, and services
        /// </summary>
        /// <param name="binding">The service binding being attached</param>
        void OnServiceAttach(IHttpServiceBinding binding);

        /// <summary>
        /// Called when a previously attached <see cref="IHttpServiceBinding"/> is being 
        /// detached from this host, requiring cleanup and removal of the binding's endpoints, 
        /// middleware, and services
        /// </summary>
        /// <param name="binding">The service binding being detached</param>
        void OnServiceDetach(IHttpServiceBinding binding);
    }
}
