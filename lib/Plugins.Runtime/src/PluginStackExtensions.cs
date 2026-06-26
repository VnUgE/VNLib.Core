/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: PluginStackExtensions.cs 
*
* PluginStackExtensions.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Runtime
{
    /// <summary>
    /// Extension methods for <see cref="IPluginStack"/> to simplify plugin lifecycle operations.
    /// </summary>
    public static class PluginStackExtensions
    {
        
        /// <summary>
        /// Serially initializes all plugin lifecycle controllers and configures
        /// plugin instances.
        /// </summary>
        /// <param name="runtime">The plugin stack whose loaders should be initialized.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void InitializeAll(this IPluginStack runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);

            foreach(RuntimePluginLoader loader in runtime.Plugins)
            {
                loader.InitializeController();
            }
        }

        /// <summary>
        /// Invokes the load method for all plugin instances
        /// </summary>
        /// <param name="runtime">The plugin stack whose plugins should be loaded.</param>
        /// <param name="concurrent"><see langword="true"/> to load plugins concurrently; otherwise, <see langword="false"/> to load sequentially.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void InvokeLoad(this IPluginStack runtime, bool concurrent)
        {
            IReadOnlyCollection<Exception> exceptions;

            if (concurrent)
            {
                ConcurrentBag<Exception> list = [];

                //Add load exceptions into the list
                InvokeLoad(runtime, concurrent, (loader, exception) =>
                {
                    list.Add(exception);
                });

                exceptions = list;
            }
            else
            {
                List<Exception> list = [];

                //Invoke load with onError callback
                InvokeLoad(runtime, concurrent, (loader, exception) =>
                {
                    list.Add(exception);
                });

                exceptions = list;
            }            

            //If any exceptions occurred, throw them now
            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        /// <summary>
        /// Invokes the load method for all plugin instances, and captures exceptions
        /// into the specified callback function.
        /// </summary>
        /// <param name="runtime">The plugin stack whose plugins should be loaded.</param>
        /// <param name="concurrent"><see langword="true"/> to load plugins concurrently; otherwise, <see langword="false"/> to load sequentially.</param>
        /// <param name="onError">A callback function to handle error conditions instead of raising exceptions</param>
        /// <remarks>
        /// NOTE: If <paramref name="concurrent"/> is true, the <paramref name="onError"/> callback may be invoked concurrently from 
        /// multiple threads, so it should be made thread safe.
        /// </remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public static void InvokeLoad(this IPluginStack runtime, bool concurrent, PluginLoadErrorHandler onError)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            ArgumentNullException.ThrowIfNull(onError);

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
                foreach(RuntimePluginLoader loader in runtime.Plugins)
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
        /// Unloads all plugins and the plugin assembly loader
        /// if unloading is supported.
        /// </summary>
        /// <param name="runtime">The plugin stack whose plugins and loaders should be unloaded.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void UnloadAll(this IPluginStack runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);

            //try unloading all plugins and their loaders, don't invoke GC on each unload
            runtime.Plugins.TryForeach(static p => p.UnloadAll(false));

            //Invoke a gc 
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Reloads all plugins and each assembly loader
        /// </summary>
        /// <param name="runtime">The plugin stack whose plugins and loaders should be reloaded.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AggregateException"></exception>
        public static void ReloadAll(this IPluginStack runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);

            //try reloading all plugins
            runtime.Plugins.TryForeach(static p => p.ReloadPlugins(false));

            //Invoke a gc
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Gets the current collection of loaded plugins for the plugin stack
        /// </summary>
        /// <param name="stack">The plugin stack to enumerate plugins from.</param>
        /// <returns>An enumeration of all <see cref="LivePlugin"/> wrappers</returns>
        public static IEnumerable<LivePlugin> GetAllPlugins(this IPluginStack stack)
        {
            ArgumentNullException.ThrowIfNull(stack);
            return stack.Plugins.SelectMany(static p => p.Controller.Plugins);
        }
    }
}
