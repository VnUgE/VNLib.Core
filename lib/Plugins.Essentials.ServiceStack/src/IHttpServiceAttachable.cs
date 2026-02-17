/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IHttpServiceAttachable.cs
*
* IHttpServiceAttachable.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Implemented by the HTTP service stack to allow external components 
    /// (plugin stacks, tests, manual services) to dynamically attach and
    /// detach <see cref="IHttpServiceBinding"/> instances to the HTTP 
    /// service domain at runtime.
    /// <para>
    /// It is assumed that the same service binding instance that 
    /// is attached will eventually be detached such that implementations
    /// can rely on reference equality for binding management.
    /// </para>
    /// </summary>
    public interface IHttpServiceAttachable
    {
        /// <summary>
        /// Attaches a service binding to all service hosts in the domain, 
        /// registering its endpoints, middleware, and services
        /// </summary>
        /// <param name="binding">The service binding to attach</param>
        void AttachService(IHttpServiceBinding binding);

        /// <summary>
        /// Detaches a previously attached service binding from all service 
        /// hosts in the domain, removing its endpoints, middleware, and services
        /// </summary>
        /// <param name="binding">The service binding to detach</param>
        void DetachService(IHttpServiceBinding binding);
    }
}
