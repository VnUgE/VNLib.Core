# VNLib.Plugins

A compact and simple contract interface and supporting types for runtime loadable plugins. It implements types that may be used outside the scope of **VNLib.Plugins.Essentials** library, but the contract types were designed for use with it, which is why they are opinionated. 

**This library has no internal or external dependencies**

#### Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.

### Usage with VNLib.Plugins.Essentials.

The **VNLib.Plugins.Essentials** library discovers IEndpoint types at runtime inside `EventProcessor` types. For correct usage with the `EventProcessor` class you should implement the interface `IVirtualEndpoint<HttpEntity>` otherwise your endpoint will not get loaded.

All plugins that intend to be loaded with the **VNLib.Plugins.Essentials.ServiceStack** application library, should conform to these signatures. The ServiceStack library also implements the additional lifecycle hooks if you choose to implement them.

## Breakdown

The `IPlugin` interface: a simple contract
``` programming language C#
   //Should be public and concrete for runtime loading
   public sealed class MyPlugin : IPluign
   {
      private readonly LinkedList<IEndpoint> _endpoints;

      public MyPlugin()
      {
          _endpoints = new LinkedList<IEndpoint>();
     }

     public string PluginName { get; } = "MyPlugin";
	  
     public void Load()
     {
        //Load plugin, build endpoints
	IEndpoint ep1 = new MyEndpoint();
	IEndpoint ep2 = new MyEndpoint();
	//Add endpoints
	_endpoints.AddLast(ep1);
	_endpoints.AddLast(ep2);
     }
	
     public void Unload()
     {
	//Unload resources
     }
	
     public IEnumerable<IEndpoint> GetEndpoints()
     {
          //Return the endpoints for this plugin
	  return _endpoints;
     }
     
     ... Additional lifecycle methods using the Attributes namespace
  }
```

The `IEndpoint` interface: represents a resource location in a url search path
``` programming language C#
   //Should be public and concrete
   internal class MyEndpoint : IEndpoint
   {
      public string Path { get; } = "/my/resource";
   }
```

A step farther is the `IVirtualEndpoint<TEntity>` which processes an entity of the specified type, and implements the IEndpoint interface:
_This interface was built for usage with the `IHttpEvent` interface as the entity type._
```
   //Should be public and concrete
   internal class MyVirtualEndpoint : IVirtualEndpoint<IHttpEvent>
   {
      public string Path { get; } = "/my/resource";
	  
      //process HTTP connection
      public ValutTask<VfReturnType> Process(IHttpEvent entity)
      {
         //Process the entity
         return VfReturnType.ProcessAsFile;
      }
   }
```
## License 
The software in this repository is licensed under the GNU GPL version 2.0 (or any later version).
See the LICENSE files for more information.