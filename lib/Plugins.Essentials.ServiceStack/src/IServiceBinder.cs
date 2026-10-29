/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IServiceBinder.cs
*
* IServiceBinder.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
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

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Implemented by a component that accepts runtime service bindings.
    /// Consumers call <see cref="Bind"/> to inject a provider's services and 
    /// <see cref="Unbind"/> to remove them, enabling hot-plug service management.
    /// <para>
    /// It is assumed that the same binding instance attached via <see cref="Bind"/>
    /// will eventually be passed to <see cref="Unbind"/> so that implementations 
    /// can use reference equality for binding tracking.
    /// </para>
    /// </summary>
    public interface IServiceBinder
    {
        /// <summary>
        /// Binds a service provider to this component, making its exported services 
        /// available for resolution within the target scope
        /// </summary>
        /// <param name="binding">The service binding to register</param>
        void Bind(IServiceBinding binding);

        /// <summary>
        /// Removes a previously bound service provider from this component, 
        /// cleaning up all services that were registered at bind time
        /// </summary>
        /// <param name="binding">The service binding to remove</param>
        void Unbind(IServiceBinding binding);
    }
}
