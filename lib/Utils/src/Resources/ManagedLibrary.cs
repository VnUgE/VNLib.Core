/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: ManagedLibrary.cs 
*
* ManagedLibrary.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;

using VNLib.Utils.IO;

namespace VNLib.Utils.Resources
{
    /// <summary>
    /// Allows dynamic/runtime loading of a managed assembly into the supplied <see cref="AssemblyLoadContext"/>
    /// and provides a mechanism for resolving dependencies and native libraries.
    /// </summary>
    public class ManagedLibrary
    {
        private readonly AssemblyLoadContext _loadContext;
        private readonly AssemblyDependencyResolver _resolver;
        private readonly Lazy<Assembly> _lazyAssembly;

        /// <summary>
        /// The absolute path to the assembly file
        /// </summary>
        public string AssemblyPath { get; }

        /// <summary>
        /// The assembly that is maintained by this loader
        /// </summary>
        public Assembly Assembly => _lazyAssembly.Value;

        /// <summary>
        /// Initializes a new <see cref="ManagedLibrary"/> and skips 
        /// initial file checks
        /// </summary>
        /// <param name="asmPath">The path to the assembly file and its dependencies</param>
        /// <param name="context">The context to load the assembly into</param>
        /// <exception cref="ArgumentNullException"></exception>
        protected ManagedLibrary(string asmPath, AssemblyLoadContext context)
        {
            _loadContext = context ?? throw new ArgumentNullException(nameof(context));
            AssemblyPath = asmPath ?? throw new ArgumentNullException(nameof(asmPath));
            _resolver = new(asmPath);

            //Add resolver for context
            context.Unloading += OnUnload;
            context.Resolving += OnDependencyResolving;
            context.ResolvingUnmanagedDll += OnNativeLibraryResolving;            

            //Lazy load the assembly
            _lazyAssembly = new(LoadAssembly, LazyThreadSafetyMode.PublicationOnly);
        }

        private Assembly LoadAssembly()
        {
            //Load the assembly into the parent context
            return _loadContext.LoadFromAssemblyPath(AssemblyPath);
        }

        /// <summary>
        /// Raised when the load context that owns this assembly 
        /// is unloaded.
        /// </summary>
        /// <param name="ctx">The context that is unloading</param>
        /// <remarks>
        /// This method should be called if the assembly is no longer 
        /// being used to free event handlers.
        /// </remarks>
        protected virtual void OnUnload(AssemblyLoadContext? ctx = null)
        {
            //Remove resolving event handlers
            _loadContext.Unloading -= OnUnload;
            _loadContext.Resolving -= OnDependencyResolving;
            _loadContext.ResolvingUnmanagedDll -= OnNativeLibraryResolving;
        }

        /*
         * Resolves a native libary isolated to the requested assembly, which 
         * should be isolated to this assembly or one of its dependencies.
         * 
         * We can usually assume the alc has the ability to fall back to safe 
         * directories (global ones also) to search for a platform native 
         * library, that is included with our assembly "package"
         */

        private IntPtr OnNativeLibraryResolving(Assembly assembly, string libname)
        {
            //Resolve the desired asm dependency for the current context
            string? requestedDll = _resolver.ResolveUnmanagedDllToPath(libname);

            //if the dep is resolved, seach in the assembly directory for the manageed dll only
            return requestedDll == null ? 
                IntPtr.Zero : 
                NativeLibrary.Load(requestedDll, assembly, DllImportSearchPath.AssemblyDirectory);
        }

        private Assembly? OnDependencyResolving(AssemblyLoadContext context, AssemblyName asmName)
        {
            //Resolve the desired asm dependency for the current context
            string? desiredAsm = _resolver.ResolveAssemblyToPath(asmName);

            //If the asm exists in the dir, load it
            return desiredAsm == null ? null : _loadContext.LoadFromAssemblyPath(desiredAsm);
        }

        /// <summary>
        /// Loads the default assembly and gets the expected export type,
        /// creates a new instance, and calls its parameterless constructor
        /// </summary>
        /// <returns>The desired type instance</returns>
        /// <exception cref="EntryPointNotFoundException"></exception>
        public T LoadTypeFromAssembly<T>()
        {
            Type resourceType = typeof(T);

            //See if the type is exported
            Type exp = (from type in Assembly.GetExportedTypes()
                        where resourceType.IsAssignableFrom(type)
                        select type)
                        .FirstOrDefault()
                        ?? throw new EntryPointNotFoundException($"Imported assembly does not export desired type {resourceType.FullName}");

            //Create instance
            return (T)Activator.CreateInstance(exp)!;
        }        

        /// <summary>
        /// Creates a new loader for the desired assembly. The assembly and its dependencies
        /// will be loaded into the specified context. If no context is specified the current assemblie's load
        /// context is captured.
        /// </summary>
        /// <param name="assemblyName">The name of the assmbly within the current plugin directory</param>
        /// <param name="loadContext">The assembly load context to load the assmbly into</param>
        /// <exception cref="FileNotFoundException"></exception>
        public static ManagedLibrary LoadManagedAssembly(string assemblyName, AssemblyLoadContext loadContext)
        {
            _ = loadContext ?? throw new ArgumentNullException(nameof(loadContext));

            //Make sure the file exists
            if (!FileOperations.FileExists(assemblyName))
            {
                throw new FileNotFoundException($"The desired assembly {assemblyName} could not be found at the file path");
            }

            //Init file info the get absolute path
            FileInfo fi = new(assemblyName);
            return new(fi.FullName, loadContext);
        }
    }
}
