/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime
* File: PluginConfigInitializer.cs 
*
* PluginConfigInitializer.cs is part of VNLib.Plugins.Runtime which is part of the larger 
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

using VNLib.Utils.IO;
using VNLib.Utils.Resources;
using VNLib.Plugins.Attributes;
using VNLib.Plugins.Runtime.Events;

namespace VNLib.Plugins.Runtime.Batteries
{
    /// <summary>
    /// A plugin event listener used to initialize plugin configuration and logging
    /// before plugins are loaded. This is used to pass configuration data from the 
    /// host to the plugin in a flexible way without forcing a specific configuration 
    /// system on plugin authors.
    /// <para>
    /// The event listener registers the <see cref="IPluginEventListener.OnBeforeLoading(PluginController, object?)"/>
    /// handler to run attributed initializer functions like <see cref="ConfigurationInitializerAttribute"/>
    /// and <see cref="LogInitializerAttribute"/>.
    /// </para>
    /// </summary>
    /// <param name="reader">The configuration reader to use for initializing plugin configuration</param>
    /// <remarks>
    /// NOTE! This handler should be registered first or early in the listener stack.
    /// </remarks>
    public sealed class PluginConfigInitializer(IPluginConfigReader reader) : IPluginEventListener
    {
        /// <summary>
        /// Uses reflection to find and invoke an exposed method with the <see cref="ConfigurationInitializerAttribute"/>
        /// set if it exists. Use to shuttle system configuration data to plugins in a flexible way without forcing a
        /// specific configuration system on plugin authors.
        /// </summary>
        /// <param name="controller">The <see cref="PluginController"/> that owns the plugin</param>
        /// <param name="plugin">The <see cref="LivePlugin"/> to initialize configuration for</param>
        private void TryInitPluginConfig(PluginController controller, LivePlugin plugin)
        {
            // Try to get the first method with the ConfigurationInitializerAttribute
            // and invoke it with the config data
            ConfigInitializer? cfi = ManagedLibrary
                    .GetMethodsWithAttribute<ConfigurationInitializerAttribute, ConfigInitializer>(plugin.Plugin!)
                    .FirstOrDefault();

            if (cfi != null)
            {
                // Write the config to binary to pass it to the plugin
                using VnMemoryStream vms = new();

                // Read config data
                reader.ReadPluginConfigData(controller.LoaderConfig, vms);

                vms.Seek(offset: 0, SeekOrigin.Begin);

                cfi.Invoke(vms.AsSpan());
            }
        }

        /// <summary>
        /// Uses reflection to find and invoke an exposed method with the <see cref="LogInitializerAttribute"/> 
        /// set if it exists. This is used to initialize the plugin's logging system with the host's command 
        /// line arguments, allowing for plugins to configure their logging system based on the host's 
        /// runtime configuration.
        /// </summary>
        /// <param name="plugin">The <see cref="LivePlugin"/> to initialize the logger for</param>
        /// <param name="cliArgs">The command line arguments to pass to the plugin's log initializer</param>
        private static void TryInitPluginLogger(LivePlugin plugin, string[] cliArgs)
        {
            // Try to get the first method with the LogInitializerAttribute
            // and invoke it with the cli args
            ManagedLibrary.GetMethodsWithAttribute<LogInitializerAttribute, LogInitializer>(plugin.Plugin!)
                .FirstOrDefault()
                ?.Invoke(cliArgs);
        }

        /// <inheritdoc/>
        public void OnBeforeLoading(PluginController controller, object? state)
        {
            string[] cliArgs = Environment.GetCommandLineArgs();

            foreach (LivePlugin lp in controller.Plugins)
            {
                /*
                 * Attempt to assign the plugin's config then logger. This is the established convention,
                 * it allows for plugins to use the config data to configure their logger if needed. 
                 * 
                 * If the plugin does not expose a config/log initializer method attributed then these 
                 * calls will be no-ops.
                 */

                TryInitPluginConfig(controller, lp);

                TryInitPluginLogger(lp, cliArgs);
            }
        }

        /// <inheritdoc/>
        public void OnPluginLoaded(PluginController controller, object? state)
        { }

        /// <inheritdoc/>
        public void OnPluginUnloaded(PluginController controller, object? state)
        { }
    }
}
