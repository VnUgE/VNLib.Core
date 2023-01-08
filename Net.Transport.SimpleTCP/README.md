# VNLib.Net.Transport.SimpleTCP

_A managed .NET simple, high performance - single process, low/no allocation, fully asynchronous, tcp socket server._ 

This library was created for use with the VNLib.Net.Http library and subsequent stacked framework libraries, however it was designed to be useful as a standalone high-performance .NET tcp listener. This library relies on the managed .NET [System.IO.Pipelines](https://github.com/dotnet/docs/blob/main/docs/standard/io/pipelines.md) library, and the **VNLib.Utils** library. 

##### SSL Support
The TcpServer manages ssl/tls using the SslStream class to make tls as transparent to the application as possible. The server manages authentication and negotiation based on the configured `SslServerAuthenticationOptions` 

## Usage

```programming language C#
   //Init config
   TCPConfig config = new()
   {
      ... configure
   }
  
   //Create the new server 
   TcpServer server = new(config);
   
  //Open the socket and begin listening for connections until the token is cancelled
  server.Start(<cancellationToken>);
 
  //Listen for connections 
  while(true)
  {
     TransportEventContext ctx = await server.AcceptAsync(<cancellationToken>);
     
     try
     {
        ..Do stuff with context, such as read data from stream
        byte[] buffer = new byte [1024];
        int count = await ctx.ConnectionStream,ReadAsync(buffer)
     }
     finally
     {
        await ctx.CloseConnectionAsync();
     }
  }
```


### Tuning information

##### Internal buffers
Internal buffers are allocated for reading and writing to the internal socket. Receive buffers sizes are set to the `Socket.ReceiveBufferSize`,
so if you wish to reduce socket memory consumption, you may use the `TCPConfig.OnSocketCreated` callback method to configure your socket accordingly.

##### Threading
This library uses the SocketAsyncEventArgs WinSock socket programming paradigm, so the `TPCConfig.AcceptThread` configuration property is the number of outstanding SocketAsyncEvents that will be pending. This value should be tuned to your use case, lower numbers relative to processor count may yield less accepts/second, higher numbers may see no increase or even reduced performance. 

##### Internal object cache
TcpServer maintains a complete object cache (VNLib.Utils.Memory.Caching.ObjectCache) which may grow quite large for your application depending on load, tuning the cache quota config property may be useful for your application. Lower numbers will increase GC load, higher values (or disabled) will likely yield a larger working set. Because of this the TcpServer class implements the ICacheHolder interface. **Note:** because TcpServer caches store disposable objects, the `CacheClear()` method does nothing. To programatically clear these caches, call the `CacheHardClear()` method.

##### Memory pools
Since this library implements the System.IO.Pipelines, it uses the `MemoryPool<byte>`  memory manager interface, you may consider using the VNLib.Utils `IUnmanagedHeap.ToPool<T>()` extension method to convert your `IUnmanagedHeap` to a `MemoryPool<byte>`

## Lisence 
The software in this repository is licensed under the GNU Affero General Public License (or any later version).
See the LICENSE files for more information.