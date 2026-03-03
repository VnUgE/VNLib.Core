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
* published by the Free Software Foundation, either version 2 of the
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
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Plugins.Runtime;
using VNLib.Plugins.Runtime.Services;
using VNLib.Utils;
using VNLib.Utils.Extensions;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.ServiceStack.Plugins
{    

    /// <summary>
    /// Independently manages plugin lifecycle within the service stack context. 
    /// Plugins are attached to an <see cref="IServiceBinder"/> target so that 
    /// plugin services are dynamically bound to the service binder target during 
    /// load/unload cycles.
    /// </summary>
    public sealed class PluginManager : VnDisposeable, IPluginManager
    {
        private readonly IPluginProvider _stack;
        private readonly IServiceBinder _target;
        private readonly ILogProvider _debugLog;       

        private PluginServiceBindingAdapter[]? _initializedPlugins;

        private PluginManager(IServiceBinder binder, ILogProvider debugLog)
        {
            ArgumentNullException.ThrowIfNull(binder);
            ArgumentNullException.ThrowIfNull(debugLog);

            _target = binder;
            _debugLog = debugLog;
            _stack = null!;
        }

        /// <summary>
        /// Initializes a new <see cref="PluginManager"/> with a runtime plugin stack
        /// </summary>
        /// <param name="binder">The service binder that plugins will be attached to</param>
        /// <param name="pluginStack">The runtime plugin stack to manage</param>
        /// <param name="debugLog">The log provider for plugin diagnostics</param>
        public PluginManager(IServiceBinder binder, IPluginStack pluginStack, ILogProvider debugLog)
            : this(binder, debugLog)
        {
            ArgumentNullException.ThrowIfNull(pluginStack);
            _stack = new DynamicPluginStackAdapter(this, pluginStack);
        }

        /// <summary>
        /// Initializes a new <see cref="PluginManager"/> with a custom plugin stack implementation
        /// </summary>
        /// <param name="binder">The service binder that plugins will be attached to</param>
        /// <param name="pluginStack">The custom plugin stack to manage</param>
        /// <param name="debugLog">The log provider for plugin diagnostics</param>
        public PluginManager(IServiceBinder binder, IPluginProvider pluginStack, ILogProvider debugLog)
            : this(binder, debugLog)
        {
            ArgumentNullException.ThrowIfNull(pluginStack);
            _stack = pluginStack;
        }

        private PluginServiceBindingAdapter[] LazyInitPluginCallback()
        {
            _stack.Build();

            /*
             * Attempt to initialize all plugins before loading them. This causes all assemblies
             * config, and dependencies to be discovered, validated, and loaded into memory.
             * 
             * Only continue with loading plugins that were successfully initialized
             */
            PluginServiceBindingAdapter[] initializedPlugins = _stack
                .GetPlugins()
                .Where(p => TryInitializePluginCore(p, _debugLog))
                .Select(p => new PluginServiceBindingAdapter(p))
                .ToArray();

            return initializedPlugins;
        }

        /// <summary>
        /// Sends a command to a plugin by its name. This is used for console command 
        /// routing to plugins. Only plugins that were successfully initialized will 
        /// be able to receive commands.
        /// </summary>
        /// <param name="pluginName">The name of the plugin to send the command to</param>
        /// <param name="command">The command text to forward to the named plugin</param>
        /// <returns>True if the plugin was found and the command was sent, false otherwise</returns>
        /// <exception cref="InvalidOperationException">Thrown if plugins have not been loaded yet</exception>
        public bool SendCommand(string pluginName, string command)
        {
            if (_initializedPlugins is null)
            {
                throw new InvalidOperationException("Plugins have not been initialized yet");
            }

            /*
             * This is a bit hacky but since dynamic plugins are assembly level, they may expose 
             * multiple internal plugin instances that all need to receive console commands, so we 
             * have to search the entire plugin stack for the correct instance to send the command to.
             */

            if (_stack is DynamicPluginStackAdapter rt)
            {
                // Select from all dynamic LivePlugin instances to send commands to
                LivePlugin? pl = _initializedPlugins
                    .Select(p => p.Plugin)
                    .Cast<DynamicPluginWrapper>()
                    .SelectMany(p => p.Plugins)
                    .FirstOrDefault(p => string.Equals(p.PluginName, pluginName, StringComparison.OrdinalIgnoreCase));

                if (pl is not null)
                {
                    pl.SendConsoleMessage(command);
                    return true;
                }
            }
            else
            {
                IManualPlugin? pl = _initializedPlugins
                    .Select(p => p.Plugin)
                    .FirstOrDefault(p => string.Equals(p.Name, pluginName, StringComparison.OrdinalIgnoreCase));
                
                if (pl is not null)
                {
                    pl.OnConsoleCommand(command);
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public void LoadPlugins(bool loadConcurrently)
        {
            Check();

            _initializedPlugins = LazyInitializer.EnsureInitialized(ref _initializedPlugins, LazyInitPluginCallback);

            /*
             * Optional concurrency for loading plugins, which can be expensive if there are 
             * many plugins with heavy initialization logic.
             */
            if (loadConcurrently)
            {
                Parallel.ForEach(_initializedPlugins, p => LoadPluginCore(p.Plugin, _debugLog));
            }
            else
            {
                _initializedPlugins.TryForeach(p => LoadPluginCore(p.Plugin, _debugLog));
            }

            if (_stack is not DynamicPluginStackAdapter)
            {
                _initializedPlugins.ForEach(AttachService);
            }
        }

        ///<inheritdoc/>
        public void ReloadPlugins(bool concurrent)
        {
            Check();

            if (_stack is DynamicPluginStackAdapter adapter)
            {
                /*
                 * Reloading should trigger dynamic unload and load events for all plugins
                 * in the stack, which will cause the binder to detach and re-attach 
                 * all plugin services, so we don't need to do anything else here
                 */

                adapter.ReloadPlugins();
            }
            else if (_initializedPlugins is not null)
            {
                /*
                 * Perform an ordered reload of plugins by first detaching them 
                 * from the service binder, then running unload and load logic, 
                 * then re-attaching them. 
                 */

                _initializedPlugins.TryForeach(DetachService);
                _initializedPlugins.TryForeach(p => p.Plugin.Unload());

                LoadPlugins(concurrent);
            }
            else
            {
                throw new InvalidOperationException("Cannot reload plugins because they have not been initialized yet");
            }
        }

        ///<inheritdoc/>
        public void UnloadPlugins()
        {
            Check();

            if (_stack is DynamicPluginStackAdapter adapter)
            {
                // Will force all assembly level plugins to unload and trigger
                // the appropriate events to detach services from the service binder
                adapter.UnloadAll();
            }
            else if (_initializedPlugins is not null)
            {
                // Detach plugins from the service binder before running unload logic
                // which might make the services undefined
                _initializedPlugins.TryForeach(DetachService);

                _initializedPlugins.TryForeach(p => p.Plugin.Unload());
            }
            else
            {
                throw new InvalidOperationException("Cannot unload plugins because they have not been initialized yet");
            }
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            /*
             * When a dynamic plugin stack is being used, plugins must 
             * be displosed by the loader, not individually on the 
             * IManualPlugin.Dispose() interface. This will cause
             * a Debug.Fail() or a noop in release mode. 
             */

            if (_stack is DynamicPluginStackAdapter adapter)
            {
                adapter.Dispose();
            }
            else if (_initializedPlugins is not null)
            {
                _initializedPlugins.TryForeach(p => p.Free());
                _initializedPlugins = null;
            }
        }

        private void AttachService(PluginServiceBindingAdapter adapter)
        {
            // Populate the adapter's service container before binding so
            // services are available when the binder resolves them
            adapter.LoadExportedServices();

            // Register the adapter with the binder, making the plugin's
            // exported services available for resolution
            _target!.Bind(adapter);
        }

        private void DetachService(PluginServiceBindingAdapter adapter)
        {
            // Remove the plugin's services from the binder before unloading or reloading
            _target.Unbind(adapter);

            // Unload the plugin's services from the adapter to clean any stale
            // references and free resources
            adapter.UnloadServices();
        }

        private static bool TryInitializePluginCore(IManualPlugin plugin, ILogProvider debugLog)
        {
            try
            {
                plugin.Initialize();
                return true;
            }
            catch (Exception ex)
            {
                debugLog.Error("Exception raised during initialization of {pl}. It has been removed from the collection\n{ex}", plugin.Name, ex);
                return false;
            }
        }

        private static void LoadPluginCore(IManualPlugin plugin, ILogProvider debugLog)
        {
            Stopwatch sw = new();
            try
            {
                sw.Start();

                plugin.Load();

                // Try to detect if the dynamic assembly did not unify correctly and
                // give the user feedback instead of a silent fail
                if (plugin is DynamicPluginWrapper wr && !wr.HasExports())
                {
                    debugLog.Warn(
                        "No plugin instances were exposed via {asm} assembly. This may be due to an assembly mismatch",
                        wr.Name
                    );
                }

                sw.Stop();

                debugLog.Debug("Loaded {pl} in {tm} ms", plugin.Name, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                debugLog.Error("Exception raised during loading {asf}. Failed to load plugin \n{ex}", plugin.Name, ex);
            }
            finally
            {
                sw.Stop();
            }
        }


        private sealed class DynamicPluginStackAdapter(
            PluginManager manager,
            IPluginStack stack
        ) : IPluginProvider, IPluginEventListener
        {

            ///<inheritdoc/>
            public void Build() => stack.BuildStack();

            ///<inheritdoc/>
            public IEnumerable<IManualPlugin> GetPlugins()
            {
                /*
                 * Captures all the plugins and registers event
                 * handlers for them before returning, so that any plugins 
                 * loaded after this point
                 */

                return stack.Plugins.Select(p =>
                {
                    DynamicPluginWrapper wrapper = new(p);

                    p.Controller.Register(this, wrapper);

                    return wrapper;
                });
            }

            public void Dispose() => stack.Dispose();

            public void UnloadAll() => stack.UnloadAll();

            public void ReloadPlugins() => stack.ReloadAll();

            /*
             * TODO: Reconsider these hooks.
             * 
             * Since dynamic plugins manage the lifecycle of their services internally,
             * we have to register hooks to capture their load/unload events. Even
             * though their operations are synchronous. That is because plugins can
             * load and unload at any time, and it's important that all services are attached 
             * and detached at the correct times to avoid stale references and errors in the service domain.
             * 
             * Assumptions:
             *  - All manual plugins are wrapped in a PluginServiceBindingAdapter 
             *  - All wrapped plugins are stored in the _initializePlugins collection
             *  - All plugins successfully initialized are in _initializePlugins and never out of sync 
             *  with the dynamic plugin stack
             *  
             *  .Single() will throw if the plugin instance is not found. It should never happen, and 
             *  if it does it will propagate up to the Load() or Unload() method if the plugin was loaded
             *  by the application logic. Otherwise it will fall back to the background of the loader.
             *  
             *  TODO: Known possible issue
             *    If reload is called on the entire stack, it will cause all assembly loaders to re-initalize
             *    which means that plugins that might have failed to initialize when the stack was first loaded,
             *    might succeed during the reload, but are not in the _initializePlugins array. Which will cause
             *    the hooks below to raise exceptions. 
             */

            ///<inheritdoc/>
            void IPluginEventListener.OnPluginLoaded(PluginController controller, object? state)
            {
                Debug.Assert(state is DynamicPluginWrapper, "State should be the plugin wrapper instance that was registered with the event listener");
                Debug.Assert(manager._initializedPlugins != null, "Initialized plugins collection should not be null when a plugin is loaded");

                // Set in the register function and should have the same reference
                // as the wrapper instance in the _initializePlugins collection
                IManualPlugin plugin = (IManualPlugin)state!;

                PluginServiceBindingAdapter binding = manager._initializedPlugins!
                    .Single(b => ReferenceEquals(b.Plugin, plugin));

                manager.AttachService(binding);
            }

            ///<inheritdoc/>
            void IPluginEventListener.OnPluginUnloaded(PluginController controller, object? state)
            {
                Debug.Assert(state is DynamicPluginWrapper, "State should be the plugin wrapper instance that was registered with the event listener");
                Debug.Assert(manager._initializedPlugins != null, "Initialized plugins collection should not be null when a plugin is loaded");

                // Set in the register function and should have the same reference
                // as the wrapper instance in the _initializePlugins collection
                IManualPlugin plugin = (IManualPlugin)state!;

                PluginServiceBindingAdapter binding = manager._initializedPlugins!
                    .Single(b => ReferenceEquals(b.Plugin, plugin));

                manager.DetachService(binding);
            }
        }

        private sealed class DynamicPluginWrapper(RuntimePluginLoader loader) : IManualPlugin
        {
            ///<inheritdoc/>
            public string Name => loader.Config.AssemblyFile;

            public IEnumerable<LivePlugin> Plugins => loader.Controller.Plugins;

            ///<inheritdoc/>
            public void Dispose()
                => Debug.Fail("DynamicPluginWrapper should not be disposed directly, it is managed by the plugin stack adapter");

            ///<inheritdoc/>
            public void GetAllExportedServices(IServiceContainer container)
            {
                PluginServiceExport[] exports = loader.Controller.GetExportedServices();
                Array.ForEach(exports, e => container.AddService(e.ServiceType, e.Service, true));
            }

            /*
             * If the plugin assembly does not expose any plugin types or there is an issue loading the assembly, 
             * its types may not unify, then we should give the user feedback instead of a silent fail.
             */
            public bool HasExports() => loader.Controller.Plugins.Any();

            ///<inheritdoc/>
            public void Initialize() => loader.InitializeController();

            ///<inheritdoc/>
            public void Load() => loader.LoadPlugins();

            ///<inheritdoc/>
            public void OnConsoleCommand(string command) => throw new NotImplementedException();

            ///<inheritdoc/>
            public void Unload() => loader.UnloadPlugins();
        }

        /// <summary>
        /// Adapts an <see cref="IManualPlugin"/> into an <see cref="IServiceBinding"/>
        /// so the plugin layer can attach services to service domains without the service 
        /// layer needing any knowledge of plugin types.
        /// </summary>
        private sealed record class PluginServiceBindingAdapter(
            IManualPlugin Plugin
        ) : IServiceBinding
        {

            private ServiceContainer? Services;

            /// <summary>
            /// Ensures that all services exported by the plugin are added to 
            /// the service container so they can be consumed
            /// </summary>
            public void LoadExportedServices()
            {
                Services = new();
                Plugin.GetAllExportedServices(Services);
            }

            /// <summary>
            /// Removes all services from the container and disposes it to free resources.
            /// This should be called when the plugin is unloaded or reloaded to avoid stale 
            /// references and free resources
            /// </summary>
            public void UnloadServices()
            {
                Services?.Dispose();
                Services = null;
            }

            /// <summary>
            /// Cleans up any internal resources and frees the plugin instance. Call
            /// this function when the plugin stack is being disposed.
            /// </summary>
            public void Free()
            {
                UnloadServices();
                Plugin.Dispose();
            }

            ///<inheritdoc/>
            IServiceProvider IServiceBinding.Services
                => Services ?? throw new InvalidOperationException("Plugin services have not been loaded yet, call LoadExportedServices first");
        }

    }
}
