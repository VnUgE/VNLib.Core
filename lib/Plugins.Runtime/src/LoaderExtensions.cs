/*
* Copyright (c) 2026 Vaughn Nugent
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
using System.Linq;
using System.Collections.Generic;

using VNLib.Plugins.Runtime.Events;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// A callback function signature for plugin loading errors on plugin
    /// stacks.
    /// </summary>
    /// <param name="Loader">The loader that the exception occurred on</param>
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
            : IPluginEventListener where T: class
        {
            private T? _service;

            /// <inheritdoc/>
            public void OnPluginLoaded(PluginController controller, object? state)
            {
                //Get the service from the loaded plugins
                T service = controller.Plugins
                    .Where(pl => type.IsAssignableFrom(pl.PluginType))
                    .Select(static pl => (T)pl.Plugin!)
                    .First();

                //Call load with the exported type
                consumerEvents.OnLoad(service, state);

                //Store for unload
                _service = service;
            }

            /// <inheritdoc/>
            public void OnPluginUnloaded(PluginController controller, object? state)
            {
                if (_service is not null)
                {
                    consumerEvents.OnUnload(_service, state);
                    _service = null;
                }
            }
        }

        /// <summary>
        /// Registers a plugin event handler for the current <see cref="PluginController"/>
        /// for a specific type.
        /// </summary>
        /// <typeparam name="T">The plugin type to consume events for.</typeparam>
        /// <param name="collection">The <see cref="PluginController"/> to register the handler on.</param>
        /// <param name="consumer">The typed plugin instance event consumer</param>
        /// <returns>A <see cref="PluginEventRegistration"/> handle that manages this event registration</returns>
        /// <exception cref="ArgumentException">The requested type is not exposed by this controller.</exception>
        public static PluginEventRegistration RegisterForType<T>(this PluginController collection, ITypedPluginConsumer<T> consumer) where T: class
        {
            Type serviceType = typeof(T);

            //Confirm the type is exposed by this collection
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
        /// <param name="reg">The <see cref="IPluginEventRegistrar"/> to register the listener on.</param>
        /// <param name="listener">The event listener to register.</param>
        /// <param name="state">An optional state object passed to the listener on each event.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>A <see cref="PluginEventRegistration"/> handle that will unregister the listener when disposed</returns>
        public static PluginEventRegistration Register(this IPluginEventRegistrar reg, IPluginEventListener listener, object? state = null)
        {
            reg.Register(listener, state);
            return new(reg, listener);
        }
       
        /// <summary>
        /// Determines if the current <see cref="PluginController"/>
        /// exposes the desired type on its <see cref="IPlugin"/>
        /// type.
        /// </summary>
        /// <param name="collection">The <see cref="PluginController"/> to check.</param>
        /// <param name="type">The desired type to request</param>
        /// <returns><see langword="true"/> if the plugin exposes the desired type; otherwise, <see langword="false"/>.</returns>
        public static bool ExposesType(this PluginController collection, Type type)
        {
            return collection.Plugins
                .Where(pl => type.IsAssignableFrom(pl.PluginType))
                .Any();
        }

        /// <summary>
        /// Gets a single plugin of the exact specified type from the controller.
        /// </summary>
        /// <typeparam name="T">The exact plugin type to find</typeparam>
        /// <param name="collection">The <see cref="PluginController"/> to search.</param>
        /// <returns>
        /// The plugin instance if found; otherwise null. Returns null if multiple plugins
        /// of the specified type exist.
        /// </returns>
        public static T? GetPlugin<T>(this PluginController collection) where T : IPlugin
        {
            return collection.Plugins
                .Where(static pl => pl.PluginType == typeof(T))
                .Select(static pl => pl.Plugin)
                .Cast<T>()
                .SingleOrDefault();
        }

        /// <summary>
        /// Gets all plugins that implement or derive from the specified type.
        /// </summary>
        /// <typeparam name="T">The base type or interface that plugins must implement</typeparam>
        /// <param name="collection">The <see cref="PluginController"/> to search.</param>
        /// <returns>
        /// An enumerable of all plugins implementing the specified type.
        /// Returns an empty enumerable if no matches are found.
        /// </returns>
        public static IEnumerable<T> GetPluginsImplementing<T>(this PluginController collection) where T : IPlugin
        {
            return collection.Plugins
                .Where(pl => typeof(T).IsAssignableFrom(pl.PluginType))
                .Select(static pl => pl.Plugin)
                .OfType<T>();
        }

    }
}
