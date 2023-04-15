# VNLib.Plugins.Runtime

A structured library for implementing runtime-loaded assemblies that expose types that implement the IPlugin runtime type and manages their lifecycle including unload-able (collectible) assemblies. Type instances are fully managed and carefully exposed as to safely control an instance's lifecycle. 

### Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software/modules). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.

## Implementation notes
This library may seem over-complicated for managing runtime plugin loading, but it was designed to remedy the common pitfalls of dynamic type loading while also providing implementation freedom to the library consumer/developer. Dependency hell is a difficult issue to work around and I have found it is really up to the application use case to know how, when, and where to load dependencies that will avoid type mismatches but allow for the best interoperability. So that being said, it is your responsibility to implement the `IPluginAssemblyLoader` interface that loads the desired plugin assembly when required and may optionally implement type unloading (likely through a collectable `AssemblyLoadContext`).

#### Type sharing
You must make sure the [VNLib.Plugins](../Plugins/README.md) assembly is shared between the host and the plugin's load context's, otherwise there will be a Runtime Type mismatch and loading will fail. The *System.CoreLib* must also be shared (this may usually be handled by falling back to the default load context). Configuration data is (optionally) passed to the `IPlugin` with a serialized utf8 JSON binary buffer and assumes your instance will de-serialize the JSON configuration data. If it does, you may want to make sure the JSON library you are using is shared with the plugin assembly. This was changed to passing serialized JSON binary to support future changes and helping avoiding dependency hell by requiring System.Text.Json library be shared, however its not perfect at the moment.

Finally, assuming you wish to use the `IPlugin` library for Http event processing, you will need to share the [VNLib.Net.Plugins](../Net.Http/readme.md) assembly, otherwise event handling will fail, and endpoints may not even register if these types are not shared. 

#### Hot Reload
Hot-reload is managed by this library if you wish to use it by setting the `IPluginConfig.WatchForReload` and the `IPluginConfig.ReloadDelay` properties correctly. Hot reload happens by watching the directory the assembly file resides for file changes. When a change has been detected, your `IPluginAssemblyLoader.Unload()` method is called and is expected to clean up resources and prepare for reloading. Unload may also be manually called if its enabled by the consumer of the `RuntimePluginLoader` instance.

#### Unloading
Unloading my be enabled, mutually exclusive to the hot-reload system. Which allows the consumer of the `RuntimePluginLoader` to manually unload plugin instances by calling `RuntimePluginLoader.UnloadAll()` and unload your `IPluginAssemblyLoader` instance, which it may then also load again at will. If unloading is disabled by your configuration calls to `RuntimePluginLoader.UnloadAll()` only unloads all plugin instances in the `PluginController` lifecycle controller. Loaded `IPlugin` instances are expected to be no longer in use and eligible for garbage collection after the `RuntimePluginLoader.UnloadAll()`, but this is only guaranteed if the consumer of the plugin respects the unload events and removes all references to ALL loaded types. (Hence the complexity of the event handling system)

## Usage
An XML documentation file is included for all public apis.

### Consumer notes
Dynamically loaded `IPlugin` instances are carefully wrapped behind multiple classes to help protect instances from improper consumption in an application, which may have undefined effects in your application. *Note* you should understand runtime assembly loading and how type isolation happens when loading via an `AssemblyLoadContext` paradigm. Plugin consumers are expected to abide by the lifecycle controller's api for proper usage. The lifecycle controller is 'event' driven, but requires registering event handlers, to avoid delegate memory leaks with a more *verbose* api (my preference). You should register your consumer event handlers before calling the `RuntimePluginLoader.LoadPlugins()` method to properly capture the `IPlugin` instance. It is safe to digest the `IPlugin` instance after this method is called by accessing the `plugin.Controller.Plugins` via the lifecycle controller. This method is **NOT** recommended, consumers should capture plugins via the event api and respect the load/unload events.

When the unload event is fired from the lifecycle controller, all references to objects captured from the plugin are expected to be removed as soon as possible to make them eligible for garbage collection, and allow proper unloading.

If you never intend to allow unloading, you may consume the `IPlugin` instances however you like as the protections provided by this library are not required or useful. If the type will never be unloaded, its safe to use everywhere in your application once its loaded.

### Code
```programming language C#
   //RuntimePluginLoader is disposable to cleanup optional config files and cleanup all resources 
   using RuntimePluginLoader plugin = new(<yourAssemblyLoader>,<hostConfig>,<errorLogProvider>?);

   //Consumer may register an event handler to capture the on-load event to consume the plugin type
   plugin.Controller.Register(<consumerEventHandler>, <optional state>?);

   //Initializes the internal assemblyLoader, initializes the IPlugin instances into the lifecycle controller
   plugin.InitializeController();

   //Load all plugins that have been initialized and invokes registered loading event handlers
   plugin.LoadPlugins();

   //Safe to consume plugins directly from the lifecycle controller, but NOT recommended. 
   plugin.Controller.Plugins.First().Plugin;

   //Trigger manual reload, will unload, then reload and trigger events
   plugin.ReloadPlugin();

   //Unload plugins only without unloading provider
   plugin.UnloadPlugins(); 

   //Unload all plugins and underlying IPluginAssemblyLoader
   plugin.UnloadAll(); 
  
   //Leaving scope disposes the loader
```
## Warnings

#### Security concerns
Plugins are required to be loading into the same AppDomain as the library consume (no remoting whatsoever) so care must be taken to understand where assemblies are loaded from, knowing the loaded code will have access to the entire AppDomain's memory. 

**Hot reload should only be enabled for debugging/development purposes, you should understand the security implications and compatibility of .NET collectable assemblies**

With new api updates, you may consider verifying the plugin assembly (or its entire dependency chain) before loading it into the application domain. 

#### Consumer warnings 
Again, consumers are expected to respect plugin lifecycle events and properly remove references when the lifecycle controller notifies of an unload event. 

Unless event handling is unregistered, events will be raised any time a manual unload/load event is called. Meaning that while it is safe to continually call the `UnloadPlugins()` or the `UnloadAll()` method, events will be raised on every call, even though the `IPlugin` instance references have been destroyed within the lifecycle controller. 

It is safe to call `ReloadPlugins()` even after a `UnloadPlugins()` or `UnloadAll()` method has been called, however `ReloadPlugins()` method will raise exceptions if unloading is not enabled.

## License
The software in this repository is licensed under the GNU GPL version 2.0 (or any later version).
See the LICENSE files for more information.
