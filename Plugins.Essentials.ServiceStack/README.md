# VNLib.Plugins.Essentials.ServiceStack

This library contains all of the utilities required for an application to build VNLib.Net.Http.HttpServer listeners for nearly unlimited virtual hosts.

### Breakdown

**HttpServiceStack** - This immutable data structure represents a collection of listening HttpServer instances across the ServiceDomain, and manages the entire lifecycle of those servers.

**ServiceDomain** - The immutable collection of ServiceGroups and their dynamically loaded plugins that represent the bindings between the listening servers and their application code. The service domain handles plugin reload events and their dynamic endpoint updates.

**ServiceGroup** - The immutable collection of service hosts that will share a common HttpServer because they have the same endpoint and TLS/SSL status (enabled or not)

**IServiceHost** - Represents an immutable container exposing the EventProcessor used to execute host specific operations and the transport information (IHostTransportInfo)

**IHostTransportInfo** - The service transport information (desired network endpoint to listen on, and an optional X509Certificate)

**HttpServiceStackBuilder** - The builder class used to build the HttpServiceStack

## Usage
Again, this library may be used broadly and therefor I will only show basic usage to generate listeners for applications.

```programming language C#
public static int main (string[] args)
{
   //Start with the new builder
   HttpServiceStackBuilder builder = new();
   
   //Build the service domain by loading all IServiceHosts
   bool built = builder.ServiceDomain.BuildDomain( hostCollection => ... );
   
   //Check status
   if(!built)
   {
      return -1;
   }
   
   //Load dynamic plugins
   Task loading = builder.ServiceDomain.LoadPlugins(<hostConfig>,<IlogProvider>);
   //wait for loading, we don't need to but if plugins don't load we may choose to exit
   loading.Wait();
   
   //Builds servers by retrieving required ITransportProvider for each service group
   builder.BuildServers(<HttpConfig>, group => ... );
   
   //Get service stack, in a using statement to cleanup. 
   using HttpServiceStack serviceStack = builder.ServiceStack;
   
   //Start servers
   serviceStack.StartServers();
  
   ... Wait for process exit
   
   //Stop servers and exit process
   serviceStack.StopAndWaitAsync().Wait();
   
  return 0;
}
```

## License

The software in this repository is licensed under the GNU Affero General Public License (or any later version).
See the LICENSE files for more information.


