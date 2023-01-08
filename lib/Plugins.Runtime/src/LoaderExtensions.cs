/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Collections.Generic;
using System.Linq;

namespace VNLib.Plugins.Runtime
{
    public static class LoaderExtensions
    {
        /// <summary>
        /// Searches all plugins within the current loader for a 
        /// single plugin that derrives the specified type
        /// </summary>
        /// <typeparam name="T">The type the plugin must derrive from</typeparam>
        /// <param name="loader"></param>
        /// <returns>The instance of the plugin that derrives from the specified type</returns>
        public static LivePlugin? GetExposedPlugin<T>(this RuntimePluginLoader loader)
        {
            return loader.LivePlugins
                .Where(static pl => typeof(T).IsAssignableFrom(pl.Plugin!.GetType()))
                .SingleOrDefault();
        }

        /// <summary>
        /// Searches all plugins within the current loader for a 
        /// single plugin that derrives the specified type
        /// </summary>
        /// <typeparam name="T">The type the plugin must derrive from</typeparam>
        /// <param name="loader"></param>
        /// <returns>The instance of your custom type casted, or null if not found or could not be casted</returns>
        public static T? GetExposedTypeFromPlugin<T>(this RuntimePluginLoader loader) where T: class
        {
            LivePlugin? plugin = loader.LivePlugins
                .Where(static pl => typeof(T).IsAssignableFrom(pl.Plugin!.GetType()))
                .SingleOrDefault();

            return plugin?.Plugin as T;
        }

        /// <summary>
        /// Registers a listener delegate method to invoke when the 
        /// current <see cref="RuntimePluginLoader"/> is reloaded, and passes 
        /// the new instance of the specified type
        /// </summary>
        /// <typeparam name="T">The single plugin type to register a listener for</typeparam>
        /// <param name="loader"></param>
        /// <param name="reloaded">The delegate method to invoke when the loader has reloaded plugins</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool RegisterListenerForSingle<T>(this RuntimePluginLoader loader, Action<T, T> reloaded) where T: class
        {
            _ = reloaded ?? throw new ArgumentNullException(nameof(reloaded));

            //try to get the casted type from the loader
            T? current = loader.GetExposedTypeFromPlugin<T>();

            if (current == null)
            {
                return false;
            }
            else
            {
                loader.Reloaded += delegate (object? sender, EventArgs args)
                {
                    RuntimePluginLoader wpl = (sender as RuntimePluginLoader)!;
                    //Get the new loaded type
                    T newT = (wpl.GetExposedPlugin<T>()!.Plugin as T)!;
                    //Invoke reloaded action
                    reloaded(current, newT);
                    //update the new current instance
                    current = newT;
                };

                return true;
            }
        }

        /// <summary>
        /// Gets all endpoints exposed by all exported plugin instances
        /// within the current loader
        /// </summary>
        /// <param name="loader"></param>
        /// <returns>An enumeration of all endpoints</returns>
        public static IEnumerable<IEndpoint> GetEndpoints(this RuntimePluginLoader loader) => loader.LivePlugins.SelectMany(static pl => pl.Plugin!.GetEndpoints());

        /// <summary>
        /// Determines if any loaded plugin types exposes an instance of the 
        /// specified type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="loader"></param>
        /// <returns>True if any plugin instance exposes a the specified type, false otherwise</returns>
        public static bool ExposesType<T>(this RuntimePluginLoader loader) where T : class
        {
            return loader.LivePlugins.Any(static pl => typeof(T).IsAssignableFrom(pl.Plugin?.GetType()));
        }
    }
}
