/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: VirtualHostConstructionExtensions.cs 
*
* VirtualHostConstructionExtensions.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
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
using System.Collections.Generic;
using System.IO;
using System.Net;

using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Essentials.ServiceStack.Construction
{
    public static class VirtualHostConstructionExtensions
    {
        /// <summary>
        /// Adds a single <see cref="IHttpMiddleware"/> instance to the virtual host
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="middleware">The middleware instance to add</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithMiddleware(this IVirtualHostBuilder vhBuilder, IHttpMiddleware middleware)
            => vhBuilder.WithOption(c => c.CustomMiddleware.Add(middleware));

        /// <summary>
        /// Adds multiple <see cref="IHttpMiddleware"/> instances to the virtual host
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="middleware">The array of middleware instances to add to the collection</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithMiddleware(this IVirtualHostBuilder vhBuilder, params IHttpMiddleware[] middleware)
            => vhBuilder.WithOption(c => Array.ForEach(middleware, m => c.CustomMiddleware.Add(m)));


        /// <summary>
        /// Takes a callback to allow you to inject middleware applications into your virtual host
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="middleware">The callback to add middleware to the collection</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithMiddleware(this IVirtualHostBuilder vhBuilder, Action<ICollection<IHttpMiddleware>> middleware)
            => vhBuilder.WithOption(c => middleware.Invoke(c.CustomMiddleware));

        /// <summary>
        /// Sets the logger provider for the virtual host
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="logger">The logger provider to use</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithLogger(this IVirtualHostBuilder vhBuilder, ILogProvider logger)
            => vhBuilder.WithOption(c => c.LogProvider = logger);

        /// <summary>
        /// Sets the hostnames for the virtual host
        /// </summary>
        /// <param name="virtualHostBuilder">The virtual host builder</param>
        /// <param name="hostnames">The array of hostnames to bind to</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithHostnames(this IVirtualHostBuilder virtualHostBuilder, string[] hostnames)
            => virtualHostBuilder.WithOption(c => c.Hostnames = hostnames);

        /// <summary>
        /// Sets the default files to serve when a directory is requested
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="defaultFiles">The array of default filenames</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithDefaultFiles(this IVirtualHostBuilder vhBuilder, params string[] defaultFiles)
            => vhBuilder.WithDefaultFiles((IReadOnlyCollection<string>)defaultFiles);

        /// <summary>
        /// Sets the default files to serve for the virtual host
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="defaultFiles">The collection of default filenames</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithDefaultFiles(this IVirtualHostBuilder vhBuilder, IReadOnlyCollection<string> defaultFiles)
            => vhBuilder.WithOption(c => c.DefaultFiles = defaultFiles);

        /// <summary>
        /// Adds file extensions to exclude from file serving
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="excludedExtensions">The array of extensions to exclude</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithExcludedExtensions(this IVirtualHostBuilder vhBuilder, params string[] excludedExtensions)
            => vhBuilder.WithExcludedExtensions(new HashSet<string>(excludedExtensions));

        /// <summary>
        /// Sets excluded file extensions for the virtual host
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="excludedExtensions">The set of extensions to exclude</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithExcludedExtensions(this IVirtualHostBuilder vhBuilder, IReadOnlySet<string> excludedExtensions)
            => vhBuilder.WithOption(c => c.ExcludedExtensions = excludedExtensions);

        /// <summary>
        /// Sets the allowed file attributes for serving files
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="attributes">The allowed file attributes</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithAllowedAttributes(this IVirtualHostBuilder vhBuilder, FileAttributes attributes)
            => vhBuilder.WithOption(c => c.AllowedAttributes = attributes);

        /// <summary>
        /// Sets the disallowed file attributes for serving files
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="attributes">The disallowed file attributes</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithDisallowedAttributes(this IVirtualHostBuilder vhBuilder, FileAttributes attributes)
            => vhBuilder.WithOption(c => c.DisallowedAttributes = attributes);

        /// <summary>
        /// Sets the downstream servers that connections will trust
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="addresses">The collection of trusted IP addresses</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithDownstreamServers(this IVirtualHostBuilder vhBuilder, IReadOnlySet<IPAddress> addresses)
            => vhBuilder.WithOption(c => c.DownStreamServers = addresses);

        /// <summary>
        /// Configures file path caching for the virtual host
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="maxAge">The maximum age for cached file paths</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithFilePathCache(this IVirtualHostBuilder vhBuilder, TimeSpan maxAge = default)
            => vhBuilder.WithOption(c => c.FilePathCacheMaxAge = maxAge);

        /// <summary>
        /// Adds an array of IP addresses to the downstream server collection. This is a security 
        /// feature that allows event handlers to trust connections from trusted downstream servers
        /// </summary>
        /// <param name="vhBuilder">The virtual host builder</param>
        /// <param name="addresses">The collection of IP addresses to set as trusted servers</param>
        /// <returns>The current instance for chaining</returns>
        public static IVirtualHostBuilder WithDownstreamServers(this IVirtualHostBuilder vhBuilder, params IPAddress[] addresses)
            => vhBuilder.WithOption(c => c.DownStreamServers = new HashSet<IPAddress>(addresses));
    }
}
