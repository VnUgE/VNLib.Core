/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins
* File: TestPluginLoader.cs 
*
* TestPluginLoader.cs is part of VNLib.Plugins which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins. If not, see http://www.gnu.org/licenses/.
*/

//Only export on DEBUG builds for testing purposes
#if DEBUG

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.Json;

using VNLib.Utils.IO;
using VNLib.Utils.Resources;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Attributes;

namespace VNLib.Plugins.Essentials.ServiceStack.Testing
{
    /// <summary>
    /// A utility class for loading and testing plugins in a controlled environment with 
    /// direct access to the assembly under test
    /// </summary>
    /// <typeparam name="T">The type of the plugin under test</typeparam>
    public sealed class TestPluginLoader<T> where T : class, IPlugin, new()
    {
        private T? _plugin;
        
        private byte[] _pluginConfig = Encoding.UTF8.GetBytes("{}"); //fallback to empty json object
        private byte[] _hostConfig = Encoding.UTF8.GetBytes("{}"); //fallback to empty json object
        private string[] _cliArgs = [];

        /// <summary>
        /// Sets the command line arguments for the plugin
        /// </summary>
        /// <param name="cliArgs"></param>
        /// <returns></returns>
        public TestPluginLoader<T> WithCliArgs(string[] cliArgs)
        {
            _cliArgs = cliArgs;
            return this;
        }

        /// <summary>
        /// Sets the configuration to pass to the plugin during loading
        /// </summary>
        /// <param name="configData"></param>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> WithPluginConfigData(byte[] configData)
        {
            _pluginConfig = configData ?? throw new ArgumentNullException(nameof(configData));
            return this;
        }

        /// <summary>
        /// Sets the configuration to pass to the plugin during loading
        /// </summary>
        /// <param name="configData"></param>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> WithPluginConfigData(ReadOnlySpan<byte> configData)
            => WithPluginConfigData(configData.ToArray());

        /// <summary>
        /// Sets the configuration to pass to the plugin during loading
        /// </summary>
        /// <param name="configData">A character array to pass to the plugin</param>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> WithPluginConfigData(string configData) 
            => WithPluginConfigData(Encoding.UTF8.GetBytes(configData));

        /// <summary>
        /// Sets the configuration file to pass to the plugin during loading
        /// </summary>
        /// <param name="path">The valid path to the file to fetch the plugin data from</param>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> WithPluginConfigFile(string path) 
            => WithPluginConfigData(File.ReadAllBytes(path));

        /// <summary>
        /// Sets the configuration to pass to the plugin during loading
        /// </summary>
        /// <param name="configData"></param>
        /// <returns></returns>
        public TestPluginLoader<T> WithHostConfigData(byte[] configData)
        {
            _hostConfig = configData ?? throw new ArgumentNullException(nameof(configData));
            return this;
        }

        /// <summary>
        /// Sets the configuration to pass to the plugin during loading
        /// </summary>
        /// <param name="configData"></param>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> WithHostConfigData(ReadOnlySpan<byte> configData)
            => WithHostConfigData(configData.ToArray());

        /// <summary>
        /// Sets the configuration to pass to the plugin during loading
        /// </summary>
        /// <param name="configData">A character array to pass to the plugin</param>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> WithHostConfigData(string configData) 
            => WithHostConfigData(Encoding.UTF8.GetBytes(configData));

        /// <summary>
        /// Sets the configuration file to pass to the plugin during loading
        /// </summary>
        /// <param name="path">The valid path to the file to fetch the plugin data from</param>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> WithHostConfigFile(string path) 
            => WithHostConfigData(File.ReadAllBytes(path));

        /// <summary>
        /// Loads the plugin and initializes it with the provided configuration and command line arguments
        /// </summary>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> Load()
        {
            _plugin = new();

            //Invoke initlaizers if they exist

            //Merge the configration data to the pluginbase format
            using VnMemoryStream configStream = new();
            BuildConfigData(configStream, _hostConfig, _pluginConfig);

            //Config must be applied before logging
            InitConfig(_plugin, configStream.AsSpan());
            InitLog(_plugin, _cliArgs);

            _plugin.Load();

            return this;
        }

        /// <summary>
        /// Gets all exported services from the plugin invokes the callback function with 
        /// the service pool. Your callback is invoked after services are exported successfully
        /// </summary>
        /// <param name="serviceCb">A callback function that this method will invoke when serviecs are loaded</param>
        /// <returns>The current instance</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public TestPluginLoader<T> GetServices(Action<TestPluginServicePool> serviceCb)
        {
            _ = _plugin ?? throw new InvalidOperationException("Plugin must be loaded before services can be loaded");

            TestPluginServicePool services  = new();
            _plugin!.PublishServices(services);

            serviceCb(services);

            return this;
        }

        /// <summary>
        /// Unloads the plugin and releases all resources
        /// </summary>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> Unload()
        {
            _plugin?.Unload();
            return this;
        }

        /// <summary>
        /// Unloads the plugin and releases all resources
        /// </summary>
        /// <param name="delayMilliseconds">The number of milliseconds to wait before unloading the plugin</param>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> Unload(int delayMilliseconds)
        {
            Thread.Sleep(delayMilliseconds);
            return Unload();
        }

        /// <summary>
        /// Disposes of the plugin if it implements <see cref="IDisposable"/>
        /// </summary>
        /// <returns>The current instance</returns>
        public TestPluginLoader<T> TryDispose()
        {
            if (_plugin is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return this;
        }

        private static void BuildConfigData(VnMemoryStream output, byte[] hostConfig, byte[] pluginConfig)
        {
            JsonDocumentOptions jdo = new()
            {
                AllowTrailingCommas = true,
                CommentHandling     = JsonCommentHandling.Skip,
            };

            //Parse documents with lax syntax options
            using JsonDocument hostDocument = JsonDocument.Parse(hostConfig, jdo);
            using JsonDocument pluginDocument = JsonDocument.Parse(pluginConfig, jdo);

            //Merge the documents the same as the plugin base expects
            using JsonDocument merged = hostDocument.Merge(pluginDocument, "host", "plugin");
            using Utf8JsonWriter writer = new(output);

            merged.WriteTo(writer);
        }

        private static void InitConfig(T plugin, ReadOnlySpan<byte> configData)
        {
            ManagedLibrary.GetMethodsWithAttribute<ConfigurationInitalizerAttribute, ConfigInitializer>(plugin!)
                .FirstOrDefault()
                ?.Invoke(configData);
        }
    
        private static void InitLog(T plugin, string[] cliArgs)
        {
            ManagedLibrary.GetMethodsWithAttribute<LogInitializerAttribute, LogInitializer>(plugin!)
                .FirstOrDefault()
                ?.Invoke(cliArgs);
        }
    }
}

#endif