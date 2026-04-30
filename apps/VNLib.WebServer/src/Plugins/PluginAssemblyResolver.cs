/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
 * File: PluginAssemblyResolver.cs
 *
 * PluginAssemblyResolver.cs is part of VNLib.WebServer which is part of the larger
* VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

#if USE_MCMASTER
using McMaster.NETCore.Plugins;
#endif

using VNLib.Utils.IO;
using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Plugins.Runtime;
using VNLib.Plugins.Runtime.Construction;

using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer.Plugins
{
    internal sealed class PluginAssemblyResolver(ServerPluginConfig config, ILogProvider logger) : IPluginAssemblyResolver
    {
        private const string PLUGIN_FILE_EXTENSION = ".dll";

        /// <summary>
        /// Allows for searching for assembly files within a set of plugin search directories
        /// The convention is that the dll file must be named the same as the directory it is in, and must 
        /// be located directly within that directory. This allows for multiple plugins to be located within 
        /// the same root plugin directory.
        /// </summary>
        /// <param name="dirs">Enumeration of directories to search for plugin assemblies.</param>
        /// <returns>Enumeration of paths to plugin assemblies.</returns>
        private static IEnumerable<string> GetPluginPaths(IEnumerable<DirectoryInfo> dirs)
        {
            // Select only dirs with a dll that is named after the directory name
            return dirs.Where(static pdir =>
            {
                string combined = Path.Combine(pdir.FullName, pdir.Name);
                
                string filePath = string.Concat(combined, PLUGIN_FILE_EXTENSION);
                return FileOperations.FileExists(filePath);
            })
            //Return the name of the dll file to import
            .Select(static pdir =>
            {
                string combined = Path.Combine(pdir.FullName, pdir.Name);
                return string.Concat(combined, PLUGIN_FILE_EXTENSION);
            });
        }

        /// <inheritdoc/>
        public IEnumerable<IPluginAssemblyLoadConfig> DiscoverAssemblies()
        {
            DirectoryInfo pluginRootDir = new (config.Path);

            logger.Verbose("Discovering plugins in {dir}", pluginRootDir.FullName);

            string[] pluginPaths = GetPluginPaths(pluginRootDir.EnumerateDirectories())
                                    .ToArray();

            logger.Verbose("Found {count} plugins{nl}{files}",
                pluginPaths.Length,
                pluginPaths.Length > 0 ? "\n" : "",
                string.Join('\n', pluginPaths.Select(Path.GetFileName))
            );

            return pluginPaths.Select(path => new PluginAsmConfig(config, path));
        }

        private sealed class PluginAsmConfig(ServerPluginConfig global, string asmFile) : IPluginAssemblyLoadConfig
        {
            /// <inheritdoc/>
            public bool Unloadable => global.HotReload;

            /// <inheritdoc/>
            public string AssemblyFile => asmFile;

            /// <inheritdoc/>
            public bool WatchForReload => global.HotReload;

            /// <inheritdoc/>
            public TimeSpan ReloadDelay => TimeSpan.FromSeconds(global.ReloadDelaySec);

            /// <inheritdoc/>
            public IAssemblyLoader GetLoader()
            {
                return Unloadable
                    ? new UnloadableAlc(this)
                    : new ImmutableAl(this);
            }
        }


        //Immutable assembly loader
        private sealed class ImmutableAl(IPluginAssemblyLoadConfig Config) : IAssemblyLoader
        {
            private readonly AssemblyLoadContext ctx = new(Config.AssemblyFile, Config.Unloadable);
            private ManagedLibrary ml = null!;

            ///<inheritdoc/>
            public Assembly GetAssembly() => ml.Assembly;

            ///<inheritdoc/>
            public void Load() => ml = ManagedLibrary.LoadManagedAssembly(Config.AssemblyFile, ctx);

            ///<inheritdoc/>
            public void Unload() => Debug.Fail("Unload was called on an immutable assembly loader");

            public void Dispose() { }
        }

        private sealed class UnloadableAlc(IPluginAssemblyLoadConfig Config) : IAssemblyLoader
        {

#if USE_MCMASTER

            private readonly PluginLoader _loader = new(new(Config.AssemblyFile)
            {
                PreferSharedTypes = true,
                IsUnloadable = Config.Unloadable,
                LoadInMemory = Config.Unloadable
            });

            ///<inheritdoc/>
            public Assembly GetAssembly() => _loader.LoadDefaultAssembly();

            ///<inheritdoc/>
            public void Load() => _loader.Load();

            ///<inheritdoc/>
            public void Unload()
            {
                if (Config.Unloadable)
                {
                    //Cleanup old loader, dont invoke GC because runtime will handle it
                    _loader.Destroy(false);
                }
            }

            public void Dispose() => Unload();

#else

            private AssemblyLoadContext ctx = null!;
            private ManagedLibrary ml = null!;

            public void Dispose() => Unload();

            public Assembly GetAssembly() => ml.Assembly;

            public void Load()
            {
                Debug.Assert(Config.Unloadable, "Assumed unloadable context when using UnloadableAlc");

                //A new load context is created for each load
                ctx = new(Config.AssemblyFile, Config.Unloadable);
                ml = ManagedLibrary.LoadManagedAssembly(Config.AssemblyFile, ctx);
            }

            public void Unload()
            {
                ctx.Unload();
                ml = null!;
            }

#endif
        }
    }
}
