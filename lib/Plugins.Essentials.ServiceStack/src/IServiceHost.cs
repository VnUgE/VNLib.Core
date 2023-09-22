/*
* Copyright (c) 2023 Vaughn Nugent
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
    /// for HttpServer routing and the <see cref="IWebRoot"/> for proccessing
    /// incomming connections
    /// </summary>
    public interface IServiceHost
    {
        /// <summary>
        /// The <see cref="IWebRoot"/> that handles HTTP connection 
        /// processing.
        /// </summary>
        IWebRoot Processor { get; }

        /// <summary>
        /// The host's transport information
        /// </summary>
        IHostTransportInfo TransportInfo { get; }
      
        /// <summary>
        /// Called when a plugin is loaded and is endpoints are extracted
        /// to be placed into service.
        /// </summary>
        /// <param name="plugin">The loaded plugin ready to be attached</param>
        /// <param name="endpoints">The dynamic endpoints of a loading plugin</param>
        void OnRuntimeServiceAttach(IManagedPlugin plugin, IEndpoint[] endpoints);

        /// <summary>
        /// Called when a <see cref="ServiceDomain"/>'s <see cref="IHttpPluginManager"/> 
        /// unloads a given plugin, and its originally discovered endpoints
        /// </summary>
        /// <param name="plugin">The unloading plugin to detach</param>
        /// <param name="endpoints">The endpoints of the unloading plugin to remove from service</param>
        void OnRuntimeServiceDetach(IManagedPlugin plugin, IEndpoint[] endpoints);

    }
}
