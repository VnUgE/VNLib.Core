﻿/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
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
        private readonly LazyInitializer<Assembly> _lazyAssembly;

        /// <summary>
        /// The absolute path to the assembly file
        /// </summary>
        public string AssemblyPath { get; }

        /// <summary>
        /// The assembly that is maintained by this loader
        /// </summary>
        public Assembly Assembly => _lazyAssembly.Instance;

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
            _lazyAssembly = new(LoadAssembly);
        }

        //Load the assembly into the parent context
        private Assembly LoadAssembly()
        {
            AdvancedTrace.WriteLine($"Loading managed library {AssemblyPath} into context {_loadContext.Name}");
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
            AdvancedTrace.WriteLine($"Unloading managed library {AssemblyPath}");
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

            AdvancedTrace.WriteLineIf(requestedDll != null,$"Resolving native library {libname} to path {requestedDll} for library {AssemblyPath}");

            //if the dep is resolved, seach in the assembly directory for the manageed dll only
            return requestedDll == null ? 
                IntPtr.Zero : 
                NativeLibrary.Load(requestedDll, assembly, DllImportSearchPath.AssemblyDirectory);
        }

        private Assembly? OnDependencyResolving(AssemblyLoadContext context, AssemblyName asmName)
        {
            //Resolve the desired asm dependency for the current context
            string? desiredAsm = _resolver.ResolveAssemblyToPath(asmName);

            AdvancedTrace.WriteLineIf(desiredAsm != null, $"Resolving managed assembly {asmName.Name} to path {desiredAsm} for library {AssemblyPath}");

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
            //See if the type is exported
            Type exp = TryGetExportedType<T>() ?? throw new EntryPointNotFoundException($"Imported assembly does not export desired type {typeof(T).FullName}");

            AdvancedTrace.WriteLine($"Creating instance of type {exp.FullName} from assembly {AssemblyPath}");

            //Create instance
            return (T)Activator.CreateInstance(exp)!;
        }

        /// <summary>
        /// Gets the type exported from the current assembly that is 
        /// assignable to the desired type.
        /// </summary>
        /// <typeparam name="T">The desired base type to get the exported type of</typeparam>
        /// <returns>The exported type that matches the desired type from the current assembly</returns>
        public Type? TryGetExportedType<T>() => TryGetExportedType(typeof(T));

        /// <summary>
        /// Gets the type exported from the current assembly that is 
        /// assignable to the desired type.
        /// </summary>
        /// <param name="resourceType">The desired base type to get the exported type of</param>
        /// <returns>The exported type that matches the desired type from the current assembly</returns>
        public Type? TryGetExportedType(Type resourceType) => TryGetAllMatchingTypes(resourceType).FirstOrDefault();

        /// <summary>
        /// Gets all exported types from the current assembly that are 
        /// assignable to the desired type.
        /// </summary>
        /// <typeparam name="T">The desired resource type</typeparam>
        /// <returns>An enumeration of acceptable types</returns>
        public IEnumerable<Type> TryGetAllMatchingTypes<T>() => TryGetAllMatchingTypes(typeof(T));

        /// <summary>
        /// Gets all exported types from the current assembly that are 
        /// assignable to the desired type.
        /// </summary>
        /// <param name="resourceType">The desired resource type</param>
        /// <returns>An enumeration of acceptable types</returns>
        public IEnumerable<Type> TryGetAllMatchingTypes(Type resourceType)
        {
            //try to get all exported types that match the desired type
            return from type in Assembly.GetExportedTypes()
                   where resourceType.IsAssignableFrom(type)
                   select type;
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
            ArgumentNullException.ThrowIfNull(loadContext);

            //Make sure the file exists
            if (!FileOperations.FileExists(assemblyName))
            {
                throw new FileNotFoundException($"The desired assembly {assemblyName} could not be found at the file path");
            }

            //Init file info the get absolute path
            FileInfo fi = new(assemblyName);
            return new(fi.FullName, loadContext);
        }

        /// <summary>
        /// A helper method that will attempt to get a named method of the desired 
        /// delegate type from the specified object. 
        /// </summary>
        /// <typeparam name="TDelegate">The method delegate that matches the signature of the desired method</typeparam>
        /// <param name="obj">The object to discover and bind the found method to</param>
        /// <param name="methodName">The name of the method to capture</param>
        /// <param name="flags">The method binding flags</param>
        /// <returns>The namaed method delegate for the object type, or null if the method was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static TDelegate? TryGetMethod<TDelegate>(
            object obj, 
            string methodName, 
            BindingFlags flags = BindingFlags.Public
        ) where TDelegate : Delegate
        {
            ArgumentNullException.ThrowIfNull(obj);
            return TryGetMethodInternal<TDelegate>(obj.GetType(), methodName, obj, flags | BindingFlags.Instance);
        }

        /// <summary>
        /// A helper method that will attempt to get a named method of the desired 
        /// delegate type from the specified object. 
        /// </summary>
        /// <typeparam name="TDelegate">The method delegate that matches the signature of the desired method</typeparam>
        /// <param name="obj">The object to discover and bind the found method to</param>
        /// <param name="methodName">The name of the method to capture</param>
        /// <param name="flags">The method binding flags</param>
        /// <returns>The namaed method delegate for the object type or an exception if not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="MissingMethodException"></exception>
        public static TDelegate GetMethod<TDelegate>(
           object obj,
           string methodName,
           BindingFlags flags = BindingFlags.Public
        ) where TDelegate : Delegate
        {
            return TryGetMethod<TDelegate>(obj, methodName, flags)
                ?? throw new MissingMethodException($"Type {obj.GetType().FullName} is missing desired method {methodName}");
        }

        /// <summary>
        /// A helper method that will attempt to get a named static method of the desired
        /// delegate type from the specified type.
        /// </summary>
        /// <typeparam name="TDelegate"></typeparam>
        /// <param name="type">The type to get the static method for</param>
        /// <param name="methodName">The name of the static method</param>
        /// <param name="flags">The optional method binind flags</param>
        /// <returns>The delegate if found <see langword="null"/> otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static TDelegate? TryGetStaticMethod<TDelegate>(Type type, string methodName, BindingFlags flags = BindingFlags.Public) 
            where TDelegate : Delegate
            => TryGetMethodInternal<TDelegate>(type, methodName, null, flags | BindingFlags.Static);

        /// <summary>
        /// A helper method that will attempt to get a named static method of the desired
        /// delegate type from the specified type.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate method type</typeparam>
        /// <typeparam name="TType">The type to get the static method for</typeparam>
        /// <param name="methodName">The name of the static method</param>
        /// <param name="flags">The optional method binind flags</param>
        /// <returns>The delegate if found <see langword="null"/> otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static TDelegate? TryGetStaticMethod<TDelegate, TType>(string methodName,BindingFlags flags = BindingFlags.Public) 
            where TDelegate : Delegate 
            => TryGetMethodInternal<TDelegate>(typeof(TType), methodName, null, flags | BindingFlags.Static);

        private static TDelegate? TryGetMethodInternal<TDelegate>(Type type, string methodName, object? target, BindingFlags flags) where TDelegate : Delegate
        {
            ArgumentNullException.ThrowIfNull(type);

            //Get delegate argument types incase of a method overload
            Type[] delegateArgs = typeof(TDelegate).GetMethod("Invoke")!
                    .GetParameters()
                    .Select(static p => p.ParameterType)
                    .ToArray();

            //get the named method and always add the static flag
            return type.GetMethod(methodName, flags, delegateArgs)
                ?.CreateDelegate<TDelegate>(target);
        }

        /*
         * NOTE: These functions cannot be optimized (condensed) any furhter. IE: static 
         * and instance method searches. This is because the C# compiler will embed the 
         * call to getType() of the object instead of loading reflection if possible 
         * at runtime. This can cause the type to be undefined at runtime and will not 
         * be able to find some members
         */

        /// <summary>
        /// Gets an array of methods that have the specified attribute and match the 
        /// delegate signature of the desired type, and returns them as an array of delegates.
        /// </summary>
        /// <typeparam name="TAttr">The function attribute type</typeparam>
        /// <typeparam name="TFunc">The function delegate type</typeparam>
        /// <param name="obj">The object instance to get the method delegates for</param>
        /// <param name="flags">The method binding flags to search for</param>
        /// <returns>An array of function with the desired attribute assigned, an empty array if no methods are found</returns>
        public static TFunc[] GetMethodsWithAttribute<TAttr, TFunc>(object obj, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance) 
            where TFunc : Delegate
            where TAttr : Attribute
        {
            ArgumentNullException.ThrowIfNull(obj);

            //Get the delegate type
            Type funcType = typeof(TFunc);

            //Get the delegate method signature
            Type[] delegateArgs = funcType.GetMethod("Invoke")!
                .GetParameters()
                .Select(static p => p.ParameterType)
                .ToArray();

            //Get the method with the attribute that matches the same signature as the delegate
            return obj.GetType()
                .GetMethods(flags)
                .Where(static m => m.GetCustomAttribute(typeof(TAttr)) != null)
                .Where(m => m.GetParameters().Select(static p => p.ParameterType).SequenceEqual(delegateArgs))
                .Select(method => method.CreateDelegate<TFunc>(obj))
                .ToArray();
        }

        /// <summary>
        /// Gets an array of static methods that have the specified attribute and match the 
        /// delegate signature of the desired type, and returns them as an array of delegates.
        /// </summary>
        /// <typeparam name="TAttr">The function attribute type</typeparam>
        /// <typeparam name="TFunc">The function delegate type</typeparam>
        /// <param name="classType">Type of the static class to get methods for</param>
        /// <param name="flags">The method binding flags to search for</param>
        /// <returns>An array of function with the desired attribute assigned, an empty array if no methods are found</returns>
        public static TFunc[] GetStaticMethodsWithAttribute<TAttr, TFunc>(Type classType, BindingFlags flags = BindingFlags.Public | BindingFlags.Static)
             where TFunc : Delegate
             where TAttr : Attribute
        {
            ArgumentNullException.ThrowIfNull(classType);

            //Get the delegate type
            Type funcType = typeof(TFunc);

            //Get the delegate method signature
            Type[] delegateArgs = funcType.GetMethod("Invoke")!
                .GetParameters()
                .Select(static p => p.ParameterType)
                .ToArray();

            //Get the method with the attribute that matches the same signature as the delegate
            return classType.GetType()
                .GetMethods(flags)
                .Where(static m => m.GetCustomAttribute(typeof(TAttr)) != null)
                .Where(m => m.GetParameters().Select(static p => p.ParameterType).SequenceEqual(delegateArgs))
                .Select(method => method.CreateDelegate<TFunc>(null))
                .ToArray();
        }
    }
}
