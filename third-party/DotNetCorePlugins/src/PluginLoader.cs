// Copyright (c) Nate McMaster.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

/*
 * Modifications Copyright (c) 2024 Vaughn Nugent
 * 
 * Changes:
 *      - Removed unloadable feature as an optional pragma (aka always on)
 *      - Removed internal hot-reload since my plugin runtime handles this feature better
 *      - Removed all static creation functions as plugin config is far more direct and simple
 *      - Removed old/depricated features such as native deps resolution since thats handled now
 *      - Remove experimenal lazy loading
 *      - Only store the main asm file path instead of the mutable config instance
 */

using System;
using System.Reflection;
using System.Runtime.Loader;

using McMaster.NETCore.Plugins.Loader;

namespace McMaster.NETCore.Plugins
{
    /// <summary>
    /// This loader attempts to load binaries for execution (both managed assemblies and native libraries)
    /// in the same way that .NET Core would if they were originally part of the .NET Core application.
    /// <para>
    /// This loader reads configuration files produced by .NET Core (.deps.json and runtimeconfig.json)
    /// as well as a custom file (*.config files). These files describe a list of .dlls and a set of dependencies.
    /// The loader searches the plugin path, as well as any additionally specified paths, for binaries
    /// which satisfy the plugin's requirements.
    /// </para>
    /// </summary>
    public class PluginLoader
    {

        private readonly string _mainAssemblyPath;
        private readonly AssemblyLoadContextBuilder _contextBuilder;

        private ManagedLoadContext _context;

        /// <summary>
        /// Initialize an instance of <see cref="PluginLoader" />
        /// </summary>
        /// <param name="config">The configuration for the plugin.</param>
        public PluginLoader(PluginConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentException.ThrowIfNullOrWhiteSpace(config.MainAssemblyPath);

            _mainAssemblyPath = config.MainAssemblyPath;
            _contextBuilder = CreateLoadContextBuilder(config);
        }

        /// <summary>
        /// True when this plugin is capable of being unloaded.
        /// </summary>
        public bool IsUnloadable => _context.IsCollectible;

        /// <summary>
        /// Initializes the new load context
        /// </summary>
        public void Load() => _context = (ManagedLoadContext)_contextBuilder.Build();

        internal AssemblyLoadContext LoadContext => _context;

        /// <summary>
        /// Load the main assembly for the plugin.
        /// </summary>
        public Assembly LoadDefaultAssembly() => _context.LoadAssemblyFromFilePath(_mainAssemblyPath);

        /// <summary>
        /// Load an assembly by name.
        /// </summary>
        /// <param name="assemblyName">The assembly name.</param>
        /// <returns>The assembly.</returns>
        public Assembly LoadAssembly(AssemblyName assemblyName) => _context.LoadFromAssemblyName(assemblyName);

        /// <summary>
        /// Sets the scope used by some System.Reflection APIs which might trigger assembly loading.
        /// <para>
        /// See https://github.com/dotnet/coreclr/blob/v3.0.0/Documentation/design-docs/AssemblyLoadContext.ContextualReflection.md for more details.
        /// </para>
        /// </summary>
        /// <returns></returns>
        public AssemblyLoadContext.ContextualReflectionScope EnterContextualReflection()
            => _context.EnterContextualReflection();

        /// <summary>
        /// Unloads the internal assembly load context
        /// </summary>
        /// <param name="invokeGc">A value that indicates if a garbage collection should be run</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void Destroy(bool invokeGc)
        {
            if (!IsUnloadable)
            {
                throw new InvalidOperationException("The current assembly context cannot be unloaded");
            }

            _context.Unload();

            //Optionally wait for GC to finish
            if (invokeGc)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
      

        private static AssemblyLoadContextBuilder CreateLoadContextBuilder(PluginConfig config)
        {
            var builder = new AssemblyLoadContextBuilder();

            builder.SetMainAssemblyPath(config.MainAssemblyPath);
            builder.SetDefaultContext(config.DefaultContext);

            foreach (var ext in config.PrivateAssemblies)
            {
                builder.PreferLoadContextAssembly(ext);
            }

            if (config.PreferSharedTypes)
            {
                builder.PreferDefaultLoadContext(true);
            }

            if (config.IsUnloadable)
            {
                builder.EnableUnloading();
            }

            if (config.LoadInMemory)
            {
                builder.PreloadAssembliesIntoMemory();
                builder.ShadowCopyNativeLibraries();
            }
          
            foreach (var assemblyName in config.SharedAssemblies)
            {
                builder.PreferDefaultLoadContextAssembly(assemblyName);
            }

            return builder;
        }
    }
}
