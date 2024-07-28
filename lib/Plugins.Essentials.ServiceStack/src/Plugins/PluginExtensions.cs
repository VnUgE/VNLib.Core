/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginExtensions.cs 
*
* PluginExtensions.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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

using System.Linq;
using System.Collections.Generic;

using VNLib.Plugins.Essentials.Runtime;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins
{
    /// <summary>
    /// Internal and service stack specific extensions for plugins
    /// </summary>
    public static class PluginExtensions
    {
        /// <summary>
        /// Gets the endpoints exposed by the plugin
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The enumeration of web endpoints</returns>
        internal static IEnumerable<IEndpoint> GetEndpoints(this IManagedPlugin plugin)
        {
            //Try to get the endpoint defintion
            if (plugin.Services.GetService(typeof(IVirtualEndpointDefinition)) is IVirtualEndpointDefinition defintion)
            {
                //Return the endpoints from the definition
                return defintion.GetEndpoints();
            }

            //If the plugin does not have an endpoint definition, return an empty enumeration
            return Enumerable.Empty<IEndpoint>();
        }

        internal static PluginRutimeEventHandler GetListener(this ServiceDomain domain) => new(domain);
    }
}
