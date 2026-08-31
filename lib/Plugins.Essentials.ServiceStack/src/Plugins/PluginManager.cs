/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.ServiceStack
* File: PluginManager.cs
*
* PluginManager.cs is part of VNLib.Plugins.Essentials.ServiceStack which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.ServiceStack is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.ServiceStack is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using VNLib.Plugins.Runtime;
using VNLib.Utils;
using VNLib.Utils.Extensions;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins
{

    /// <summary>
    /// A convenience class for managing the lifecycle of a <see cref="IPluginStack"/> and 
    /// its associated plugins. This class provides methods for loading, reloading, and unloading plugins
    /// while also handling diagnostics and error logging. 
    /// </summary>
    public sealed class PluginManager : VnDisposeable
    {
        private readonly IPluginStack _stack;       
        private readonly ILogProvider _debugLog;

        private bool _isBuilt;

        /// <summary>
        /// Initializes a new <see cref="PluginManager"/> with a runtime plugin stack
        /// </summary>
        /// <param name="pluginStack">The runtime plugin stack to manage</param>
        /// <param name="debugLog">The log provider for plugin diagnostics</param>
        public PluginManager(IPluginStack pluginStack, ILogProvider debugLog)
        {
            ArgumentNullException.ThrowIfNull(pluginStack);
            ArgumentNullException.ThrowIfNull(debugLog);

            _stack = pluginStack;
            _debugLog = debugLog;
        }

        private void LoadPluginCore(RuntimePluginLoader loader)
        {
            Stopwatch sw = new();

            sw.Start();

            try
            {
                loader.InitializeController();
            }
            catch (Exception ex)
            {
                _debugLog.Error(
                    "Exception raised during initialization of {pl}. Failed to install plugin\n{ex}",
                    loader.Config.AssemblyFile,
                    ex
                );

                sw.Stop();

                // exit now
                return;
            }

            long initTime = sw.ElapsedMilliseconds;

            sw.Restart();

            try
            {
                loader.LoadPlugins();

                // Try to detect if the dynamic assembly did not unify correctly and
                // give the user feedback instead of a silent fail
                if (loader.Controller.Plugins.Count == 0)
                {
                    _debugLog.Warn(
                        "No plugin instances were exposed via {asm} assembly. This may be due to an assembly or version mismatch",
                        loader
                    );
                }

                sw.Stop();

                // Print short name for readability during normal operation
                _debugLog.Debug("Loaded {pl}. Init time {init} ms, load time {tm} ms",
                    Path.GetFileName(loader.Config.AssemblyFile),
                    initTime,
                    sw.ElapsedMilliseconds
                );
            }
            catch (Exception ex)
            {
                _debugLog.Error("Exception raised during loading {asf}. Failed to load plugin \n{ex}", loader.Config.AssemblyFile, ex);
            }
            finally
            {
                sw.Stop();
            }
        }

        /// <summary>
        /// Loads plugins into the current service manager. The log provider
        /// passed to the constructor will be used for plugin diagnostics.
        /// </summary>
        /// <param name="concurrent"><see langword="true"/> to load plugins concurrently; otherwise, <see langword="false"/> to load serially.</param>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void LoadPlugins(bool concurrent)
        {
            Check();       

            // First time build stack
            if (!_isBuilt)
            {
                _stack.BuildStack();
                _isBuilt = true;
            }

            /*
             * Optional concurrency for loading plugins, which can be expensive if there are 
             * many plugins with heavy initialization logic.
             */
            if (concurrent)
            {
                Parallel.ForEach(_stack.Plugins, LoadPluginCore);
            }
            else
            {
                _stack.Plugins.TryForeach(LoadPluginCore);
            }
        }

        /// <summary>
        /// Manually reloads all plugins loaded to the current service manager
        /// </summary>
        /// <param name="concurrent"><see langword="true"/> to reload plugins concurrently; otherwise, <see langword="false"/> to reload serially.</param>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void ReloadPlugins(bool concurrent)
        {
            Check();

            if (concurrent)
            {
                // Attempt to reload plugins concurrently.
                Parallel.ForEach(_stack.Plugins, static rtl => rtl.ReloadPlugins(false));

                // Invoke GC once completed
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            else
            {
                // Helper extension reload sequentially
                _stack.ReloadAll();
            }           
        }

        /// <summary>
        /// Unloads all loaded plugins and calls their event handlers
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void UnloadPlugins()
        {
            Check();

            _stack.UnloadAll();            
        }

        ///<inheritdoc/>
        protected override void Free() => _stack.Dispose();
    }
}
