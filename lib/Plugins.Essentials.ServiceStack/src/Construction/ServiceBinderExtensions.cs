/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: ServiceBinderExtensions.cs
*
* ServiceBinderExtensions.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

using System.Linq;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{
    /// <summary>
    /// Provides fluent extension methods for creating <see cref="IServiceBinder"/> 
    /// instances from common service stack types
    /// </summary>
    public static class ServiceBinderExtensions
    {
        /// <summary>
        /// Creates an <see cref="IServiceBinder"/> targeting every distinct 
        /// <see cref="IServiceHost"/> in the domain. Hosts are deduplicated by 
        /// reference across groups before the binder is constructed, preventing 
        /// a host shared between multiple <see cref="ServiceGroup"/> instances 
        /// from receiving duplicate bind/unbind calls.
        /// </summary>
        /// <param name="domain">The service domain whose hosts will receive bindings</param>
        /// <returns>A binder scoped to all distinct hosts in the domain</returns>
        public static IServiceBinder CreateBinder(this ServiceDomain domain)
        {
            IServiceHost[] hosts = domain.ServiceGroups
                .SelectMany(static s => s.Hosts)
                .Distinct()             // guard against the same host appearing in multiple groups
                .ToArray();

            return new ServiceHostBinder(hosts);
        }

        /// <summary>
        /// Creates an <see cref="IServiceBinder"/> targeting every distinct 
        /// <see cref="IServiceHost"/> in the stack's service domain. Equivalent 
        /// to calling <c>stack.ServiceDomain.CreateBinder()</c>.
        /// </summary>
        /// <param name="stack">The service stack whose domain hosts will receive bindings</param>
        /// <returns>A binder scoped to all distinct hosts in the stack's service domain</returns>
        public static IServiceBinder CreateBinder(this HttpServiceStack stack)
            => CreateBinder(stack.ServiceDomain);
    }
}
