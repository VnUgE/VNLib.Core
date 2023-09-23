/*
* Copyright (c) 2023 Vaughn Nugent
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

using System.IO;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.ServiceStack
{
    /// <summary>
    /// Allows for defining virtual hosts for the service stack
    /// </summary>
    public interface IDomainBuilder
    {
        /// <summary>
        /// Adds a single virtual host to the domain that must be configured.
        /// </summary>
        /// <param name="rootDirectory">The service root directory</param>
        /// <param name="hooks">The virtual host event hook handler</param>
        /// <param name="Logger">The log provider</param>
        /// <returns>The <see cref="IVirtualHostBuilder"/> instance</returns>
        IVirtualHostBuilder WithVirtualHost(DirectoryInfo rootDirectory, IVirtualHostHooks hooks, ILogProvider Logger);

        /// <summary>
        /// Adds a single pre-configured virtual host to the domain 
        /// </summary>
        /// <param name="config">The pre-configured virtual host configuration</param>
        /// <returns>The current instance</returns>
        IDomainBuilder WithVirtualHost(VirtualHostConfiguration config);
    }
}
