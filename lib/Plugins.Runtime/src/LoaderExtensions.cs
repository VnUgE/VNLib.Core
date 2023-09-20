/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Text.Json;
using System.Collections.Generic;

using VNLib.Utils.IO;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Contains extension methods for PluginLoader library
    /// </summary>
    public static class LoaderExtensions
    {
        /*
         * Class that manages a collection registration for a specific type 
         * dependency, and redirects the event calls for the consumed service
         */
        private sealed class TypedRegistration<T> : IPluginEventListener where T: class
        {
            private readonly ITypedPluginConsumer<T> _consumerEvents;
            private readonly object? _userState;

            private T? _service;
            private readonly Type _type;

            public TypedRegistration(ITypedPluginConsumer<T> consumerEvents, Type type)
            {
                _consumerEvents = consumerEvents;
                _type = type;
            }
            

            public void OnPluginLoaded(PluginController controller, object? state)
            {
                //Get the service from the loaded plugins
                T service = controller.Plugins
                    .Where(pl => _type.IsAssignableFrom(pl.PluginType))
                    .Select(static pl => (T)pl.Plugin!)
                    .First();

                //Call load with the exported type
                _consumerEvents.OnLoad(service, _userState);

                //Store for unload
                _service = service;
            }

            public void OnPluginUnloaded(PluginController controller, object? state)
            {
                //Unload
                _consumerEvents.OnUnload(_service!, _userState);
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
        public static PluginEventRegistration RegisterForType<T>(this PluginController collection, ITypedPluginConsumer<T> consumer) where T: class
        {
            Type serviceType = typeof(T);

            //Confim the type is exposed by this collection
            if(!ExposesType(collection, serviceType))
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
        public static T? GetExposedTypes<T>(this PluginController collection) where T: class
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
            _ = runtime ?? throw new ArgumentNullException(nameof(runtime));

            foreach(RuntimePluginLoader loader in runtime.Plugins)
            {
                loader.InitializeController();
            }
        }

        /// <summary>
        /// Invokes the load method for all plugin instances
        /// </summary>
        /// <param name="runtime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void InvokeLoad(this IPluginStack runtime)
        {
            _ = runtime ?? throw new ArgumentNullException(nameof(runtime));

            //try loading all plugins
            runtime.Plugins.TryForeach(static p => p.LoadPlugins());
        }

        /// <summary>
        /// Invokes the unload method for all plugin instances
        /// </summary>
        /// <param name="runtime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void InvokeUnload(this IPluginStack runtime)
        {
            _ = runtime ?? throw new ArgumentNullException(nameof(runtime));

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
            _ = runtime ?? throw new ArgumentNullException(nameof(runtime));

            //try unloading all plugins and their loaders
            runtime.Plugins.TryForeach(static p => p.UnloadAll());
        }

        /// <summary>
        /// Reloads all plugins and each assembly loader
        /// </summary>
        /// <param name="runtime"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void ReloadAll(this IPluginStack runtime)
        {
            _ = runtime ?? throw new ArgumentNullException(nameof(runtime));

            //try reloading all plugins
            runtime.Plugins.TryForeach(static p => p.ReloadPlugins());
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
            _ = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _ = listener ?? throw new ArgumentNullException(nameof(listener));

            //Register for all plugins
            foreach (PluginController controller in runtime.Plugins.Select(static p => p.Controller))
            {
                controller.Register(listener, state);
            }
        }

        /// <summary>
        /// Unregisters a plugin event listener for all plugins
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="listener">The listener instance to unregister</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void UnregsiterListener(this IPluginStack runtime, IPluginEventListener listener)
        {
            _ = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _ = listener ?? throw new ArgumentNullException(nameof(listener));

            //Unregister for all plugins
            foreach (PluginController controller in runtime.Plugins.Select(static p => p.Controller))
            {
                controller.Unregister(listener);
            }
        }

        /// <summary>
        /// Specify the host configuration data to pass to the plugin
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="hostConfig">A configuration element to pass to the plugin's host config element</param>
        /// <returns>The current builder instance for chaining</returns>
        public static PluginStackBuilder WithConfigurationData(this PluginStackBuilder builder, JsonElement hostConfig)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            //Clone the host config into binary
            using VnMemoryStream ms = new();
            using (Utf8JsonWriter writer = new(ms))
            {
                hostConfig.WriteTo(writer);
            }

            //Store binary
            return builder.WithConfigurationData(ms.AsSpan());
        }

        /// <summary>
        /// Specifies the directory that the plugin loader will search for plugins in
        /// </summary>
        /// <param name="path">The search directory path</param>
        /// <param name="builder"></param>
        /// <returns>The current builder instance for chaining</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static PluginStackBuilder WithSearchDirectory(this PluginStackBuilder builder, string path) => WithSearchDirectory(builder, new DirectoryInfo(path));

        /// <summary>
        /// Specifies the directory that the plugin loader will search for plugins in
        /// </summary>
        /// <param name="dir">The search directory instance</param>
        /// <param name="builder"></param>
        /// <returns>The current builder instance for chaining</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static PluginStackBuilder WithSearchDirectory(this PluginStackBuilder builder, DirectoryInfo dir)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));
            _ = dir ?? throw new ArgumentNullException(nameof(dir));

            PluginDirectorySearcher dirSearcher = new (dir);
            builder.WithDiscoveryManager(dirSearcher);
            return builder;
        }

        /// <summary>
        /// Gets the current collection of loaded plugins for the plugin stack
        /// </summary>
        /// <param name="stack"></param>
        /// <returns>An enumeration of all <see cref="LivePlugin"/> wrappers</returns>
        public static IEnumerable<LivePlugin> GetAllPlugins(this IPluginStack stack) => stack.Plugins.SelectMany(static p => p.Controller.Plugins);

        private sealed record class PluginDirectorySearcher(DirectoryInfo Dir) : IPluginDiscoveryManager
        {
            private const string PLUGIN_FILE_EXTENSION = ".dll";

            ///<inheritdoc/>
            public string[] DiscoverPluginFiles()
            {
                //Enumerate all dll files within the seach directory
                IEnumerable<DirectoryInfo> dirs = Dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);

                //Search all directories for plugins and return the paths
                return GetPluginPaths(dirs).ToArray();
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
