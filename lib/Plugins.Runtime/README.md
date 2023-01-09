# VNLib.Plugins.Runtime

A library that manages the runtime loading/unloading of a managed .NET assembly that exposes one or more types that implement the VNLib.Plugins.IPlugin interface, and the plugins lifecycle. The `DynamicPluginLoader` class also handles "hot" assembly reload and exposes lifecycle hooks for applications to correctly detect those changes.

#### Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.

### 3rd Party Dependencies
This library does not, modify, contribute, or affect the functionality of any of the 3rd party libraries below.

**VNLib.Plugins.Runtime** relies on a single 3rd party library [McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins) from [Nate McMaster](https://github.com/natemcmaster) for runtime assembly loading. You must include this dependency in your project.

## Usage
An XML documentation file is included for all public apis.

```programming language C#
   //RuntimePluginLoader is disposable to cleanup optional config files and cleanup all PluginLoader resources
   using RuntimePluginLoader plugin = new(<fqAssemblyPath>,...<args>);
   
   //Load assembly, optional config file, capture all IPlugin types, then call IPlugin.Load() and all other lifecycle hooks
   await plugin.InitLoaderAsync();
   //Listen for reload events
   plugin.Reloaded += (object? loader, EventAargs = null) =>{};
   
   //Get all endpoints from all exposed plugins 
   IEndpoint[] endpoints = plugin.GetEndpoints().ToArray();
   
   //Your plugin types may also expose custom types, you may see if they are available 
   if(plugin.ExposesType<IMyCustomType>())
   {
      IMyCustomType mt = plugin.GetExposedTypeFromPlugin<IMyCustomType>();
   }
   
   //Trigger manual reload, will unload, then reload and trigger events
   plugin.ReloadPlugin();

   //Unload all plugins
   plugin.UnloadAll();
   
   //Leaving scope disposes the loader
```
### Warnings
##### Load/Unload/Hot reload
When hot-reload is disabled and manual reloading is not expected, or unloading is also disabled, you not worry about reload events since the assemblies will never be unloaded. If unloading is disabled and `RuntimePluginLoader.UnloadAll()` is called, only the IPlugin lifecycle hooks will be called (`IPlugin.Unload();`), internal collections are cleared, but no other actions take place. 

`RuntimePluginLoader.UnloadAll()` Should only be called when you are no longer using the assembly, and all **IPlugin** instances or custom types. The **VNLib.Plugins.Essentials.ServiceStack** library is careful to remove all instances of the exposed plugins, their endpoints, and all other custom types that were exposed, before calling this method. 

Disposing the **RuntimePluginLoader** does not unload the plugins, but simply disposes any internal resources disposes the internal **PluginLoader**, so it should only be disposed after `RuntimePluginLoader.UnloadAll()` is called.

_Please see [McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins) for more information on runtime .NET assembly loading and the dangers of doing so_

**Hot reload should only be enabled for debugging/development purposes, you should understand the security implications and compatibility of .NET collectable assemblies**

## License
The software in this repository is licensed under the GNU GPL version 2.0 (or any later version).
See the LICENSE files for more information.
