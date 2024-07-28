/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: SsBuilderExtensions.cs 
*
* SsBuilderExtensions.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.IO;
using System.Net;
using System.Collections.Generic;

using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{
    public static class VirtualHostConstructionExtensions
    {
        /// <summary>
        /// Adds a single <see cref="IHttpMiddleware"/> instance to the virtual host
        /// </summary>
        /// <param name="vhBuilder"></param>
        /// <param name="middleware">The middleware instance to add</param>
        /// <returns></returns>
        public static IVirtualHostBuilder WithMiddleware(this IVirtualHostBuilder vhBuilder, IHttpMiddleware middleware) 
            => vhBuilder.WithOption(c => c.CustomMiddleware.Add(middleware));

        /// <summary>
        /// Adds multiple <see cref="IHttpMiddleware"/> instances to the virtual host
        /// </summary>
        /// <param name="vhBuilder"></param>
        /// <param name="middleware">The array of middleware instances to add to the collection</param>
        /// <returns></returns>
        public static IVirtualHostBuilder WithMiddleware(this IVirtualHostBuilder vhBuilder, params IHttpMiddleware[] middleware) 
            => vhBuilder.WithOption(c => Array.ForEach(middleware, m => c.CustomMiddleware.Add(m)));


        /// <summary>
        /// Takes a callback to allow you to inject middelware applications into 
        /// your virtual host
        /// </summary>
        /// <param name="vhBuilder"></param>
        /// <param name="middleware">The array of middleware instances to add to the collection</param>
        /// <returns></returns>
        public static IVirtualHostBuilder WithMiddleware(this IVirtualHostBuilder vhBuilder, Action<ICollection<IHttpMiddleware>> middleware)
            => vhBuilder.WithOption(c => middleware.Invoke(c.CustomMiddleware));

        public static IVirtualHostBuilder WithLogger(this IVirtualHostBuilder vhBuilder, ILogProvider logger) 
            => vhBuilder.WithOption(c => c.LogProvider = logger);

        public static IVirtualHostBuilder WithHostnames(this IVirtualHostBuilder virtualHostBuilder, string[] hostnames) 
            => virtualHostBuilder.WithOption(c => c.Hostnames = hostnames);

        public static IVirtualHostBuilder WithDefaultFiles(this IVirtualHostBuilder vhBuidler, params string[] defaultFiles) 
            => vhBuidler.WithDefaultFiles((IReadOnlyCollection<string>)defaultFiles);

        public static IVirtualHostBuilder WithDefaultFiles(this IVirtualHostBuilder vhBuidler, IReadOnlyCollection<string> defaultFiles) 
            => vhBuidler.WithOption(c => c.DefaultFiles = defaultFiles);

        public static IVirtualHostBuilder WithExcludedExtensions(this IVirtualHostBuilder vhBuilder, params string[] excludedExtensions) 
            => vhBuilder.WithExcludedExtensions(new HashSet<string>(excludedExtensions));

        public static IVirtualHostBuilder WithExcludedExtensions(this IVirtualHostBuilder vhBuilder, IReadOnlySet<string> excludedExtensions) 
            => vhBuilder.WithOption(c => c.ExcludedExtensions = excludedExtensions);

        public static IVirtualHostBuilder WithAllowedAttributes(this IVirtualHostBuilder vhBuilder, FileAttributes attributes) 
            => vhBuilder.WithOption(c => c.AllowedAttributes = attributes);

        public static IVirtualHostBuilder WithDisallowedAttributes(this IVirtualHostBuilder vhBuilder, FileAttributes attributes) 
            => vhBuilder.WithOption(c => c.DissallowedAttributes = attributes);

        public static IVirtualHostBuilder WithDownstreamServers(this IVirtualHostBuilder vhBuilder, IReadOnlySet<IPAddress> addresses) 
            => vhBuilder.WithOption(c => c.DownStreamServers = addresses);

        public static IVirtualHostBuilder WithFilePathCache(this IVirtualHostBuilder vhBuilder, TimeSpan maxAge = default)
           => vhBuilder.WithOption(c => c.FilePathCacheMaxAge = maxAge);

        /// <summary>
        /// Adds an array of IP addresses to the downstream server collection. This is a security 
        /// features that allows event handles to trust connections/ipaddresses that originate from
        /// trusted downstream servers
        /// </summary>
        /// <param name="vhBuilder"></param>
        /// <param name="addresses">The collection of IP addresses to set as trusted servers</param>
        /// <returns></returns>
        public static IVirtualHostBuilder WithDownstreamServers(this IVirtualHostBuilder vhBuilder, params IPAddress[] addresses) 
            => vhBuilder.WithOption(c => c.DownStreamServers = new HashSet<IPAddress>(addresses));
    }
}
