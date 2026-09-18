/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: PluginConsoleEventHandler.cs 
*
* PluginConsoleEventHandler.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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
using System.Linq;

using VNLib.Utils.Resources;

using VNLib.Plugins.Attributes;
using VNLib.Plugins.Runtime.Events;

namespace VNLib.Plugins.Runtime.Batteries
{
    /// <summary>
    /// A <see cref="IPluginEventListener"/> that keeps track of loaded plugins with console event handler 
    /// methods, and allows sending console commands to those plugins by their declared name.
    /// <para>
    /// This handler must respect plugin runtime lifecycle and listens to load/unload/reload events by 
    /// the plugin runtime. Results returned by public methods should not be cached and may change 
    /// during lifecycle events. 
    /// </para>
    /// </summary>
    /// <remarks>
    /// All calls to public functions are thread safe. However you should avoid calling during lifecycle events. 
    /// </remarks>
    public sealed class PluginConsoleEventHandler : IPluginEventListener
    {
        /*
         * Keeps an entry for a plugin with a console event handler method, by the plugin's 
         * declared name.
         * 
         * Ignore plugin name string case for better UX, plugin names should not be case sensitive
         * anyway.
         */
        private readonly ConcurrentDictionary<string, ConsoleHandlerState> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the names of all loaded plugins that have declared a console event handler method.
        /// </summary>
        /// <returns>The names of all loaded plugins that have declared a console event handler method.</returns>
        public string[] GetEnabledNames() => _loadedPlugins.Keys.ToArray();

        /// <summary>
        /// Returns a value if the plugin with the given name is loaded and declared a
        /// console event handler method.
        /// </summary>
        /// <param name="pluginName">The name of the plugin to check.</param>
        /// <returns><see langword="true"/> if the plugin with the given name is loaded and has a console event handler method; otherwise, <see langword="false"/>.</returns>
        public bool IsEnabled(string pluginName) => _loadedPlugins.ContainsKey(pluginName);

        /// <summary>
        /// Sends the given console command to the plugin with the given name,
        /// if it is loaded and has a console event handler method.
        /// </summary>
        /// <param name="pluginName">The name of the plugin to send the command to.</param>
        /// <param name="command">The console command to send to the plugin.</param>
        /// <returns><see langword="true"/> if the command was sent successfully; otherwise, <see langword="false"/>.</returns>
        public bool SendConsoleCommand(string pluginName, string command)
        {
            if (_loadedPlugins.TryGetValue(pluginName, out ConsoleHandlerState? state))
            {
                state.Handler(command);
                return true;
            }

            return false;
        }

        private static ConsoleEventHandlerSignature? GetConsoleHandler(IPlugin plugin)
        {
            // Get a delegate handler for the plugin
            return ManagedLibrary.GetMethodsWithAttribute<ConsoleEventHandlerAttribute, ConsoleEventHandlerSignature>(plugin)
                .FirstOrDefault();
        }

        /// <inheritdoc/>
        void IPluginEventListener.OnPluginLoaded(PluginController controller, object? state)
        {
            foreach (LivePlugin pl in controller.Plugins)
            {
                ConsoleEventHandlerSignature? handler = GetConsoleHandler(pl.Plugin!);
                if (handler != null)
                {
                    // Add entry to table
                    _loadedPlugins[pl.PluginName] = new ConsoleHandlerState(pl, handler);
                }
            }
        }

        /// <inheritdoc/>
        void IPluginEventListener.OnPluginUnloaded(PluginController controller, object? state)
        {
            foreach (LivePlugin pl in controller.Plugins)
            {
                // Best effort remove entry from table by it's declared name, if it exists
                _ = _loadedPlugins.TryRemove(pl.PluginName, out _);
            }
        }

        private sealed record class ConsoleHandlerState(
            LivePlugin Plugin,
            ConsoleEventHandlerSignature Handler
        );
    }
}
