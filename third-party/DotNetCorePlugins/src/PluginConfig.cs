// Copyright (c) Nate McMaster.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

/*
 * Modifications Copyright (c) 2024 Vaughn Nugent
 * 
 * Changes: 
 *      - Removed unloadable feature as an optional pragma (aka always on)
 *      - Removed hot reload as an option
 *      - Used net 8.0 auto properties instead of field indexers
 *      - Removed experimental lazy loading since it's not safe for my use cases
 */

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;

namespace McMaster.NETCore.Plugins
{
    /// <summary>
    /// Represents the configuration for a .NET Core plugin.
    /// </summary>
    public class PluginConfig
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PluginConfig" />
        /// </summary>
        /// <param name="mainAssemblyPath">The full file path to the main assembly for the plugin.</param>
        public PluginConfig(string mainAssemblyPath)
        {
            if (string.IsNullOrEmpty(mainAssemblyPath))
            {
                throw new ArgumentException("Value must be null or not empty", nameof(mainAssemblyPath));
            }

            if (!Path.IsPathRooted(mainAssemblyPath))
            {
                throw new ArgumentException("Value must be an absolute file path", nameof(mainAssemblyPath));
            }

            MainAssemblyPath = mainAssemblyPath;
        }

        /// <summary>
        /// The file path to the main assembly.
        /// </summary>
        public string MainAssemblyPath { get; }

        /// <summary>
        /// A list of assemblies which should be treated as private.
        /// </summary>
        public ICollection<AssemblyName> PrivateAssemblies { get; protected set; } = new List<AssemblyName>();

        /// <summary>
        /// A list of assemblies which should be unified between the host and the plugin.
        /// </summary>
        /// <seealso href="https://github.com/natemcmaster/DotNetCorePlugins/blob/main/docs/what-are-shared-types.md">
        /// https://github.com/natemcmaster/DotNetCorePlugins/blob/main/docs/what-are-shared-types.md
        /// </seealso>
        public ICollection<AssemblyName> SharedAssemblies { get; protected set; } = new List<AssemblyName>();

        /// <summary>
        /// Attempt to unify all types from a plugin with the host.
        /// <para>
        /// This does not guarantee types will unify.
        /// </para>
        /// <seealso href="https://github.com/natemcmaster/DotNetCorePlugins/blob/main/docs/what-are-shared-types.md">
        /// https://github.com/natemcmaster/DotNetCorePlugins/blob/main/docs/what-are-shared-types.md
        /// </seealso>
        /// </summary>
        public bool PreferSharedTypes { get; set; }

        /// <summary>
        /// If set, replaces the default <see cref="AssemblyLoadContext"/> used by the <see cref="PluginLoader"/>.
        /// Use this feature if the <see cref="AssemblyLoadContext"/> of the <see cref="Assembly"/> is not the Runtime's default load context.
        /// i.e. (AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly) != <see cref="AssemblyLoadContext.Default"/>
        /// </summary>
        public AssemblyLoadContext DefaultContext { get; set; } = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()) ?? AssemblyLoadContext.Default;

        /// <summary>
        /// The plugin can be unloaded from memory.
        /// </summary>
        public bool IsUnloadable { get; set; }

        /// <summary>
        /// Loads assemblies into memory in order to not lock files.
        /// As example use case here would be: no hot reloading but able to
        /// replace files and reload manually at later time
        /// </summary>
        public bool LoadInMemory { get; set; }
    }
}
