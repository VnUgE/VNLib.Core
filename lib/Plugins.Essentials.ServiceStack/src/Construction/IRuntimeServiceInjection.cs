/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IRuntimeServiceInjection.cs 
*
* IRuntimeServiceInjection.cs is part of VNLib.Plugins.Essentials.ServiceStack which 
* is part of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{
    /// <summary>
    /// Adds functionality to event processors for runtime service injection
    /// that can be used to add and remove services from the service provider
    /// at runtime.
    /// </summary>
    public interface IRuntimeServiceInjection
    {
        /// <summary>
        /// Adds a collection of services to event processors 
        /// that can be used.
        /// </summary>
        /// <param name="services">The collection of exported services</param>
        void AddServices(IServiceProvider services);

        /// <summary>
        /// Removes a collection of services from event processors
        /// </summary>
        /// <param name="services">The service container that contains services to remove</param>
        void RemoveServices(IServiceProvider services);
    }
}
