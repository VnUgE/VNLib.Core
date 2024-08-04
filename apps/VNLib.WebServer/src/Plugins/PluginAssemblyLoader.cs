/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: PluginAssemblyLoader.cs 
*
* PluginAssemblyLoader.cs is part of VNLib.WebServer which is part of the larger 
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
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

#if USE_MCMASTER
using McMaster.NETCore.Plugins;
#endif

using VNLib.Plugins.Runtime;
using VNLib.Utils.Resources;

namespace VNLib.WebServer.Plugins
{

    internal static class PluginAsemblyLoading
    {
        public static IAssemblyLoader Create(IPluginAssemblyLoadConfig config)
        {
            return config.Unloadable ? new UnloadableAlc(config) : new ImmutableAl(config);
        }

        //Immutable assembly loader
        internal sealed record class ImmutableAl(IPluginAssemblyLoadConfig Config) : IAssemblyLoader
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

        internal sealed record class UnloadableAlc(IPluginAssemblyLoadConfig Config) : IAssemblyLoader
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
                    //ctx.Unload();
                    //ml = null!;

                    //Init new load context with the same name
                    //ctx = new AssemblyLoadContext(Config.AssemblyFile, Config.Unloadable);
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
