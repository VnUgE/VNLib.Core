# VNLib.Net.Messaging.FBM.Client

Fixed Buffer Messaging Protocol client library. High performance stateful messaging protocol built on top of HTTP web-sockets. Low/no allocation, completely asynchronous while providing a TPL API. This library provides a simple asynchronous request/response architecture to web-sockets. This was initially designed to provide an alternative to complete HTTP request/response overhead, but allow a simple control flow for work across a network.

The base of the library relies on creating message objects that allocate fixed size buffers are configured when the IFBMMessageis constructed. All data is written to the internal buffer adhering to the [FBM protocol](../../#)

This client library allows for messages to be streamed to the server, however this library is optimized for fixed buffers, so streaming will not be the most efficient, and will likely cause slow-downs in message transmission. However, since FBM relies on a streaming protocol,
so it was silly not to provide it. Streams add overhead of additional buffer allocation, additional copy, and message fragmentation (multiple writes to the web-socket). Since frames written to the web-socket must be synchronized, a mutex is held during transmission, which means the more message overhead, the longer the blocking period on new messages. Mutex acquisition will wait asynchronously when necessary.

## Fundamentals
The main implementation is the FBMClient class. This class provides the means for creating the stateful connection to the remote server. It also provides an internal FBMRequest message rental (object cache) that created initialized FBMRequest messages. This class may be derived to provide additional functionality, such as handling control frames that may dynamically alter the state of the connection (negotiation etc). A mechanism to do so is provided.

### FBMClient layout

``` C#
	public class FBMClient : VnDisposeable, IStatefulConnection, ICacheHolder
	{
		//Raised when an error occurs during receiving or parsing
		public event EventHandler<FMBClientErrorEventArgs>? ConnectionClosedOnError;	
        
		//Raised when connection is closed, regardless of the cause
		public event EventHandler? ConnectionClosed;	

		//Connects to the remote server at the specified websocket address (ws:// or wss://)
		public Task ConnectAsync(Uri address, CancellationToken cancellation = default);

		//When connected, sends the specified message to the remote server, with the default timeout
		public Task<FBMResponse> SendAsync(FBMRequest request, CancellationToken cancellationToken = default);

		//When connected, sends the specified message to the remote server, with the specified timeout, -1 or 0 to disable timeout
		public Task<FBMResponse> SendAsync(FBMRequest request, TimeSpan timeout, CancellationToken cancellationToken = default);

		//When connected, streams a message to the remote server, * the message payload must not be configured *
		public Task<FBMResponse> StreamDataAsync(FBMRequest request, Stream payload, ContentType ct, CancellationToken cancellationToken = default);

		//Disconnects from the remote server
		public async Task DisconnectAsync(CancellationToken cancellationToken = default);

		//Releases all held resourses 
		public void Dispose(); //Inherrited from VnDisposeable

		ICacheHolder.CacheClear();	    //Inherited member, clears cached FBMRequest objects
		ICacheHolder.CacheHardClear();	    //Inherited member, clears cached FBMRequest objects
	}
```

### Example usage
``` C#

	FBMClientConfig config = new()
	{
		 //The unmanaged heap the allocate buffers from
                BufferHeap = MemoryUtil.Shared,
                //The absolute maximum message size to buffer from the server
                MaxMessageSize = 10 * 1024 * 1024, //10KiB,
                //The size of the buffer used for buffering incoming messages server messages
                RecvBufferSize = maxExtra,
                //The FBMRequest internal buffer size, should be max message + headers
                MessageBufferSize = (int)Helpers.ToNearestKb(maxMessageSize + MAX_FBM_MESSAGE_HEADER_SIZE),
                //The space reserved in the FBM request buffer used for header storage
                MaxHeaderBufferSize = 1024,
                //Set the web-socket subprotocol
                SubProtocol = "object-cache",
                //Use the default encoding (UTF-8)
                HeaderEncoding = Helpers.DefaultEncoding,
                //How frequently to send web-socket keepalive messages
                KeepAliveInterval = TimeSpan.FromSeconds(30),
                //The default request message timeout
                RequestTimeout = TimeSpan.FromSeconds(10),
                //optional debug log, write message debug information if set
                DebugLog = null
	};

	//Create client from the config
	using (FBMClient client = new(config))
	{
		//Usually set some type of authentication headers before connecting 

		/*
		  client.ClientSocket.SetHeader("Authorization", "Authorization token");
		*/

		//Connect to server
		Uri address = new Uri("wss://localhost:8080/some/fbm/endpoint");
		await client.ConnectAsync(address, CancellationToken.None);

		do
		{
			//Rent request message
			FBMRequest request = client.RentRequest();
			//Some arbitrary header value (or preconfigured header)
			request.WriteHeader(0x10, "Hello");
			//Some arbitrary payload
			request.WriteBody(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A }, ContentType.Binary);
			//Send request message
			using (FBMResponse response = await client.SendAsync(request, CancellationToken.None))
			{
				//Extension method to raise exception if an invalid response was received (also use the response.IsSet flag)
				response.ThrowIfNotSet();

				//Check header parse status
				//response.StatusFlags
				
				//Check headers (using Linq to get first header)
				string header1 = response.Headers.First().Value.ToString();
				
				//Get payload, data is valid until the response is disposed
				ReadOnlySpan<byte> body = response.ResponseBody;
			}
			//Return request
			client.ReturnRequest(request);
			//request.Dispose(); //Alternativly dispose message

			await Task.Delay(1000);
		}
		while(true);
        //You should probably cleanup by asynchronously closing the connection before disposing
	}
```

## Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.