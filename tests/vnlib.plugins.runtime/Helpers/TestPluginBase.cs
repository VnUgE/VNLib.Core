/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Runtime.Tests
* File: TestPluginBase.cs
*
* TestPluginBase.cs is part of VNLib.Plugins.Runtime.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Runtime.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Runtime.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Runtime.Tests. If not, see http://www.gnu.org/licenses/.
*/

using System;

using VNLib.Plugins.Attributes;

namespace VNLib.Plugins.Runtime.Tests.Helpers
{
    /// <summary>
    /// A test double implementation of <see cref="IPlugin"/> that provides call tracking
    /// for lifecycle methods. Subclasses can override virtual methods to inject custom behavior.
    /// </summary>
    public abstract class TestPluginBase : IPlugin
    {
        /// <summary>
        /// Gets the number of times <see cref="Load"/> has been called on this instance.
        /// </summary>
        public int LoadCallCount { get; protected set; }

        /// <summary>
        /// Gets the number of times <see cref="Unload"/> has been called on this instance.
        /// </summary>
        public int UnloadCallCount { get; protected set; }

        /// <summary>
        /// Gets the number of times <see cref="PublishServices"/> has been called on this instance.
        /// </summary>
        public int PublishServicesCallCount { get; protected set; }     

        /// <summary>
        /// Gets the number of times the configuration initializer method has been called on this instance.
        /// </summary>
        public int ConfigCalledCount { get; protected set; }

        /// <summary>
        /// Gets the number of times the log initializer method has been called on this instance.
        /// </summary>
        public int LogCalledCount { get; protected set; }

        /// <summary>
        /// Gets the last console message received by the console event handler.
        /// </summary>
        public string? LastConsoleMessage { get; protected set; }      

        /// <inheritdoc/>
        public string PluginName { get; set; } = "TestPlugin";

        /// <inheritdoc/>
        public virtual void Load() => LoadCallCount++;

        /// <inheritdoc/>
        public virtual void Unload() => UnloadCallCount++;

        /// <inheritdoc/>
        public virtual void PublishServices(IPluginServicePool pool) 
            => PublishServicesCallCount++;

        /// <summary>
        /// Configuration initializer method called by the plugin runtime when configuration data is available.
        /// Decorated with <see cref="ConfigurationInitializerAttribute"/>.
        /// </summary>
        /// <param name="configData">The configuration data for this plugin.</param>
        [ConfigurationInitializer]
        public virtual void OnConfigLoaded(ReadOnlySpan<byte> configData) 
            => ConfigCalledCount++;

        /// <summary>
        /// Log initializer method called by the plugin runtime when the logging system is ready.
        /// Decorated with <see cref="LogInitializerAttribute"/>.
        /// </summary>
        /// <param name="cliArgs">The command-line arguments passed to the application.</param>
        [LogInitializer]
        public virtual void OnLogLoaded(string[] cliArgs) 
            => LogCalledCount++;

        /// <summary>
        /// Console event handler method called when console events are dispatched.
        /// Decorated with <see cref="ConsoleEventHandlerAttribute"/>.
        /// </summary>
        /// <param name="message">The console message received.</param>
        [ConsoleEventHandler]
        public virtual void OnConsoleEvent(string message) 
            => LastConsoleMessage = message;
    }
}
