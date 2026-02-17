/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IHttpServiceBinding.cs
*
* IHttpServiceBinding.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

using System;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Represents a bundle of HTTP-consumable services that can be dynamically 
    /// attached to or detached from an HTTP service domain at runtime. This is
    /// the contract between external service providers (plugins, manual services, 
    /// tests) and the HTTP hosting layer.
    /// </summary>
    public interface IHttpServiceBinding
    {
        /// <summary>
        /// Gets the service provider whose services will be resolved and injected 
        /// into the host's service pool for use during request processing. The provider
        /// may expose endpoints via <see cref="IVirtualEndpointDefinition"/>, middleware
        /// via <see cref="IHttpMiddleware"/> collections, and other request-scoped services.
        /// </summary>
        IServiceProvider Services { get; }
    }
}
