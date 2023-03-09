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
using System.Threading.Tasks;


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
        /// Loads the configuration file into its <see cref="JsonDocument"/> format 
        /// for reading.
        /// </summary>
        /// <param name="loader"></param>
        /// <returns>A new <see cref="JsonDocument"/> of the loaded configuration file</returns>
        public static async Task<JsonDocument> GetPluginConfigAsync(this RuntimePluginLoader loader)
        {
            //Open and read the config file
            await using FileStream confStream = File.OpenRead(loader.PluginConfigPath);

            JsonDocumentOptions jdo = new()
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            };

            //parse the plugin config file
            return await JsonDocument.ParseAsync(confStream, jdo);
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
        /// <param name="loader"></param>
        /// <returns>The instance of your custom type casted, or null if not found or could not be casted</returns>
        public static T? GetExposedTypes<T>(this PluginController collection) where T: class
        {
            LivePlugin? plugin = collection.Plugins
                .Where(static pl => typeof(T).IsAssignableFrom(pl.PluginType))
                .SingleOrDefault();

            return plugin?.Plugin as T;
        }
    }
}
