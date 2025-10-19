/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: LoaderExtensions.cs 
*
* LoaderExtensions.cs is part of VNLib.Plugins.Runtime which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Utils.IO;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Runtime
{

    /// <summary>
    /// A callback function signature for plugin plugin loading errors on plugin
    /// stacks.
    /// </summary>
    /// <param name="Loader">The loader that the exception occured on</param>
    /// <param name="exception">The exception cause of the error</param>
    public delegate void PluginLoadErrorHandler(RuntimePluginLoader Loader, Exception exception);

    /// <summary>
    /// Contains extension methods for PluginLoader library
    /// </summary>
    public static class LoaderExtensions
    {
        /*
         * Class that manages a collection registration for a specific type 
         * dependency, and redirects the event calls for the consumed service
         */
        private sealed class TypedRegistration<T>(ITypedPluginConsumer<T> consumerEvents, Type type)
            : IPluginEventListener where T : class
        {
            private readonly ITypedPluginConsumer<T> _consumerEvents = consumerEvents;

            private T? _service;
            private readonly Type _type = type;

            ///<inheritdoc/>
            public void OnPluginLoaded(PluginController controller, object? state)
            {
                //Get the service from the loaded plugins
                T service = controller.Plugins
                    .Where(pl => _type.IsAssignableFrom(pl.PluginType))
                    .Select(static pl => (T)pl.Plugin!)
                    .First();

                //Call load with the exported type
                _consumerEvents.OnLoad(service, state);

                //Store for unload
                _service = service;
            }

            public void OnPluginUnloaded(PluginController controller, object? state)
            {
                //Unload
                _consumerEvents.OnUnload(_service!, state);
                _service = null;
            }
        }

        /// <summary>
        /// Registers a plugin even handler for the current <see cref="PluginController"/>
        /// for a specific type. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="consumer">The typed plugin instance event consumer</param>
        /// <returns>A <see cref="PluginEventRegistration"/> handle that manages this event registration</returns>
        /// <exception cref="ArgumentException"></exception>
        public static PluginEventRegistration RegisterForType<T>(this PluginController collection, ITypedPluginConsumer<T> consumer) where T : class
        {
            Type serviceType = typeof(T);

            //Confim the type is exposed by this collection
            if (!ExposesType(collection, serviceType))
            {
                throw new ArgumentException("The requested type is not exposed in this assembly");
            }

            //Create new typed listener
            TypedRegistration<T> reg = new(consumer, serviceType);

            //register event handler
            return Register(collection, reg, null);
        }

        /// <summary>
        /// Registers a handler to listen for plugin load/unload events
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>A <see cref="PluginEventRegistration"/> handle that will unregister the listener when disposed</returns>
        public static PluginEventRegistration Register(this IPluginEventRegistrar reg, IPluginEventListener listener, object? state = null)
        {
            reg.Register(listener, state);
            return new(reg, listener);
        }

        /// <summary>
        /// Determines if the current <see cref="PluginController"/>
        /// exposes the desired type on is <see cref="IPlugin"/>
        /// type.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="type">The desired type to request</param>
        /// <returns>True if the plugin exposes the desired type, false otherwise</returns>
        public static bool ExposesType(this PluginController collection, Type type)
        {
            return collection.Plugins
                .Where(pl => type.IsAssignableFrom(pl.PluginType))
                .Any();
        }

        /// <summary>
        /// Searches all plugins within the current loader for a 
        /// single plugin that derrives the specified type
        /// </summary>
        /// <typeparam name="T">The type the plugin must derrive from</typeparam>
        /// <param name="collection"></param>
        /// <returns>The instance of your custom type casted, or null if not found or could not be casted</returns>
        public static T? GetExposedTypes<T>(this PluginController collection) where T : class
        {
            LivePlugin? plugin = collection.Plugins
                .Where(static pl => typeof(T).IsAssignableFrom(pl.PluginType))
                .SingleOrDefault();

            return plugin?.Plugin as T;
        }

        /// <summary>
        /// Serially initialzies all plugin lifecycle controllers and configures 
        /// plugin instances.
        /// </summary>
        /// <param name="runtime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void InitializeAll(this IPluginStack runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime, nameof(runtime));

            runtime.Plugins.ForEach(static loader => loader.InitializeController());           
        }

        /// <summary>
        /// Invokes the load method for all plugin instances
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="concurrent">A value that indicates if plugins should be loaded concurrently or sequentially</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void InvokeLoad(this IPluginStack runtime, bool concurrent)
        {
            List<Exception> exceptions = new();

            //Add load exceptions into the list
            void onError(RuntimePluginLoader loader, Exception ex) => exceptions.Add(ex);

            //Invoke load with onError callback
            InvokeLoad(runtime, concurrent, onError);

            //If any exceptions occured, throw them now
            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        /// <summary>
        /// Invokes the load method for all plugin instances, and captures exceptions
        /// into the specified callback function.
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="concurrent">A value that indicates if plugins should be loaded concurrently or sequentially</param>
        /// <param name="onError">A callback function to handle error conditions instead of raising exceptions</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void InvokeLoad(this IPluginStack runtime, bool concurrent, PluginLoadErrorHandler onError)
        {
            ArgumentNullException.ThrowIfNull(runtime, nameof(runtime));

            if (concurrent)
            {
                //Invoke load in parallel
                Parallel.ForEach(runtime.Plugins, p =>
                {
                    try
                    {
                        p.LoadPlugins();
                    }
                    catch (Exception ex)
                    {
                        onError(p, ex);
                    }
                });
            }
            else
            {
                //Load sequentially
                foreach (RuntimePluginLoader loader in runtime.Plugins)
                {
                    try
                    {
                        loader.LoadPlugins();
                    }
                    catch (Exception ex)
                    {
                        onError(loader, ex);
                    }
                }
            }
        }

        /// <summary>
        /// Invokes the unload method for all plugin instances
        /// </summary>
        /// <param name="runtime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void InvokeUnload(this IPluginStack runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);

            //try unloading all plugins
            runtime.Plugins.TryForeach(static p => p.UnloadPlugins());
        }

        /// <summary>
        /// Unloads all plugins and the plugin assembly loader
        /// if unloading is supported.
        /// </summary>
        /// <param name="runtime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void UnloadAll(this IPluginStack runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);

            //try unloading all plugins and their loaders, dont invoke GC on each unload
            runtime.Plugins.TryForeach(static p => p.UnloadAll(false));

            //Invoke a gc once completely unloaded
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Reloads all plugins and each assembly loader
        /// </summary>
        /// <param name="runtime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void ReloadAll(this IPluginStack runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);

            //try reloading all plugins
            runtime.Plugins.TryForeach(static p => p.ReloadPlugins(false));

            //Invoke a gc once completely reloaded
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Registers a plugin event listener for all plugins
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="listener">The event listener instance</param>
        /// <param name="state">Optional state parameter</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void RegsiterListener(this IPluginStack runtime, IPluginEventListener listener, object? state = null)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(listener);           

            //Register  for all plugins
            runtime.Plugins
                .Select(static p => p.Controller)
                .ForEach(controller => controller.Register(listener, state));
        }

        /// <summary>
        /// Unregisters a plugin event listener for all plugins
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="listener">The listener instance to unregister</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void UnregsiterListener(this IPluginStack runtime, IPluginEventListener listener)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(listener);

            //Unregister for all plugins
            runtime.Plugins
                .Select(static p => p.Controller)
                .TryForeach(controller => controller.Unregister(listener));           
        }
    
        /// <summary>
        /// Specifies the directory that the plugin loader will search for plugins in
        /// </summary>
        /// <param name="paths">An array of search directories</param>
        /// <param name="builder"></param>
        /// <returns>The current builder instance for chaining</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static PluginStackBuilder WithSearchDirectories(this PluginStackBuilder builder, string[] paths)
            => WithSearchDirectories(builder, paths.Select(static p => new DirectoryInfo(p)).ToArray());

        /// <summary>
        /// Specifies the directory that the plugin loader will search for plugins in
        /// </summary>
        /// <param name="dirs">The search directory instance</param>
        /// <param name="builder"></param>
        /// <returns>The current builder instance for chaining</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static PluginStackBuilder WithSearchDirectories(this PluginStackBuilder builder, DirectoryInfo[] dirs)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(dirs);

            builder.WithDiscoveryManager(discoveryManager: new PluginDirectorySearcher(dirs));
            return builder;
        }


        /// <summary>
        /// Registers a new <see cref="SharedPluginServiceProvider"/> for the current plugin stack
        /// that will listen for plugin events and capture the exported services into a 
        /// single pool.
        /// </summary>
        /// <param name="stack"></param>
        /// <returns>A new <see cref="SharedPluginServiceProvider"/> that will capture all exported services when loaded</returns>
        public static SharedPluginServiceProvider RegisterServiceProvider(this IPluginStack stack)
        {
            //Init new service provider
            SharedPluginServiceProvider provider = new();

            //Register for all plugins
            RegsiterListener(stack, provider);

            return provider;
        }

        /// <summary>
        /// Gets the current collection of loaded plugins for the plugin stack
        /// </summary>
        /// <param name="stack"></param>
        /// <returns>An enumeration of all <see cref="LivePlugin"/> wrappers</returns>
        public static IEnumerable<LivePlugin> GetAllPlugins(this IPluginStack stack)
            => stack.Plugins.SelectMany(static p => p.Controller.Plugins);

        private sealed record class PluginDirectorySearcher(DirectoryInfo[] SearchDirs) : IPluginDiscoveryManager
        {
            private const string PLUGIN_FILE_EXTENSION = ".dll";

            ///<inheritdoc/>
            public string[] DiscoverPluginFiles()
            {
                /*
                 * Accumulate all plugin child directores 
                 * from the search directories
                 */

                IEnumerable<DirectoryInfo> dirs = SearchDirs.SelectMany(static p => p.EnumerateDirectories("*", SearchOption.TopDirectoryOnly));

                //Search all directories for plugins and return the paths
                return [.. GetPluginPaths(dirs)];
            }

            private static IEnumerable<string> GetPluginPaths(IEnumerable<DirectoryInfo> dirs)
            {
                //Select only dirs with a dll that is named after the directory name
                return dirs.Where(static pdir =>
                {
                    string compined = Path.Combine(pdir.FullName, pdir.Name);
                    string FilePath = string.Concat(compined, PLUGIN_FILE_EXTENSION);
                    return FileOperations.FileExists(FilePath);
                })
                //Return the name of the dll file to import
                .Select(static pdir =>
                {
                    string compined = Path.Combine(pdir.FullName, pdir.Name);
                    return string.Concat(compined, PLUGIN_FILE_EXTENSION);
                });
            }
        }       
    }
}
