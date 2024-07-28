/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: IDomainBuilder.cs 
*
* IDomainBuilder.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{
    /// <summary>
    /// Allows for defining virtual hosts for the service stack
    /// </summary>
    public interface IDomainBuilder
    {
        /// <summary>
        /// Allows for defining a new virtual host for the domain by manually configuring it.
        /// </summary>
        /// <param name="builder">A callback function that passes the new host builder</param>
        /// <returns>The current instance</returns>
        IDomainBuilder WithServiceGroups(Action<IServiceGroupBuilder> builder);

        /// <summary>
        /// Adds a collection of hosts to the domain
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        IDomainBuilder WithHosts(IServiceHost[] host);
    }
}
