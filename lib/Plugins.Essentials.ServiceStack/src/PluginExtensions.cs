/*
* Copyright (c) 2023 Vaughn Nugent
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

using VNLib.Plugins.Runtime;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.ServiceStack
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
        internal static IEnumerable<IEndpoint> GetEndpoints(this IPlugin plugin) => ((IWebPlugin)plugin).GetEndpoints();

        /// <summary>
        /// Gets only plugins that implement <see cref="IWebPlugin"/> interface
        /// </summary>
        /// <param name="controller"></param>
        /// <returns></returns>
        internal static IEnumerable<LivePlugin> GetOnlyWebPlugins(this PluginController controller) => controller.Plugins.Where(p => p.Plugin is IWebPlugin);

        /// <summary>
        /// Loads all plugins that implement <see cref="IWebPlugin"/> interface into the 
        /// service stack
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="logProvider">A log provider for writing loading logs to</param>
        public static void LoadPlugins(this HttpServiceStack stack, ILogProvider logProvider) => (stack.PluginManager as PluginManager)!.LoadPlugins(logProvider);
    }
}
