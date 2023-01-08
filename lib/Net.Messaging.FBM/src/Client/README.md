# VNLib.Net.Messaging.FBM.Client

Fixed Buffer Messaging Protocol client library. High performance statful messaging 
protocol built on top of HTTP web-sockets. Low/no allocation, completely asynchronous
while providing a TPL API. This library provides a simple asynchronous request/response 
architecture to web-sockets. This was initially designed to provide an alternative to 
complete HTTP request/response overhead, but allow a simple control flow for work 
across a network.

The base of the library relies on creating message objects that allocate fixed size 
buffers are configured when the IFBMMessageis constructed. All data is written to the
internal buffer adhering to the format below. 

Messages consist of a 4 byte message id, a collection of headers, and a message body. 
The first 4 bytes of a message is the ID (for normal messages a signed integer greater than 0), 
0 is reserved for error conditions, and negative numbers are reserved for internal 
messages. Headers are identified by a single byte, followed by a variable length UTF8 
encoded character sequence, followed by a termination of 0xFF, 0xF1 (may change).

### Message structure
	4 byte positive (signed 32-bit integer) message id
	2 byte termination
	1 byte header-id
	variable length UTF8 value
	2 byte termination
	-- other headers --
	2 byte termination (extra termination, ie: empty header)
	variable length payload
	(end of message is the end of the payload)

Buffer sizes are generally negotiated on initial web-socket upgrade, so to buffer entire messages 
in a single read/write from the web-socket. Received messages are read into memory until
the web-socket has no more data available. The message is then parsed and passed for processing
on the server side, or complete a pending request on the client side. Servers may drop the 
web-socket connection and return an error if messages exceed the size of the pre-negotiated
buffer. Servers should validate buffer sizes before accepting a connection.

This client library allows for messages to be streamed to the server, however this library
is optimized for fixed buffers, so streaming will not be the most efficient, and will likely
cause slow-downs in message transmission. However, since FBM relies on a streaming protocol,
so it was silly not to provide it. Streams add overhead of additional buffer allocation, 
additional copy, and message fragmentation (multiple writes to the web-socket). Since frames 
written to the web-socket must be synchronized, a mutex is held during transmission, which 
means the more message overhead, the longer the blocking period on new messages. Mutex
acquisition will wait asynchronously when necessary.

The goal of the FBM protocol for is to provide efficient use of resources (memory, network, 
and minimize GC load) to transfer small messages truly asynchronously, at wire speeds, with 
only web-socket and transport overhead. Using web-sockets simplifies implementation, and allows
comparability across platforms, languages, and versions.

## fundamentals

The main implementation is the FBMClient class. This class provides the means for creating
the stateful connection to the remote server. It also provides an internal FBMRequest message 
rental (object cache) that created initialized FBMRequest messages. This class may be derrived
to provide additional functionality, such as handling control frames that may dynamically 
alter the state of the connection (negotiation etc). A mechanism to do so is provided.

### FBMClient layout

```
	public class FBMClient : VnDisposeable, IStatefulConnection, ICacheHolder
	{
		//Raised when an error occurs during receiving or parsing
		public event EventHandler<FMBClientErrorEventArgs>? ConnectionClosedOnError;	
        
		//Raised when connection is closed, regardless of the cause
		public event EventHandler? ConnectionClosed;	

		//Connects to the remote server at the specified websocket address (ws:// or wss://)
		public async Task ConnectAsync(Uri address, CancellationToken cancellation = default);

		//When connected, sends the specified message to the remote server
		public async Task<FBMResponse> SendAsync(FBMRequest request, CancellationToken cancellationToken = default);

		//When connected, streams a message to the remote server, * the message payload must not be configured *
		public async Task<FBMResponse> StreamDataAsync(FBMRequest request, Stream payload, ContentType ct, CancellationToken cancellationToken = default);

		//Disconnects from the remote server
		public async Task DisconnectAsync(CancellationToken cancellationToken = default);

		//Releases all held resourses 
		public void Dispose(); //Inherrited from VnDisposeable

		ICacheHolder.CacheClear();		//Inherited member, clears cached FBMRequest objects
		ICacheHolder.CacheHardClear();	//Inherited member, clears cached FBMRequest objects
	}
```

### Example usage
```
	FBMClientConfig config = new()
	{
		//The size (in bytes) of the internal buffer to use when receiving messages from the server
		RecvBufferSize = 1024,
		
		//FBMRequest buffer size (expected size of buffers, required for negotiation)
		RequestBufferSize = 1024,

		//The size (in chars) of headers the FBMResponse should expect to buffer from the server
		ResponseHeaderBufSize = 1024,

		//The absolute maximum message size to buffer from the server
		MaxMessageSize = 10 * 1024 * 1024, //10KiB

		//The unmanaged heap the allocate buffers from
		BufferHeap = Memory.Shared,

		//Web-socket keepalive frame interval
		KeepAliveInterval = TimeSpan.FromSeconds(30),

		//Web-socket sub-protocol header value
		SubProtocol = null
	};

	//Craete client from the config
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
	}
```

## Final Notes

XML Documentation is or will be provided for almost all public exports. APIs are intended to 
be sensibly public and immutable to allow for easy extensability (via extension methods). I
often use extension libraries to provide additional functionality. (See cache library)

This library is likely a niche use case, and is probably not for everyone. Unless you care
about reasonably efficient high frequency request/response messaging, this probably isnt 
for you. This library provides a reasonable building block for distributed lock mechanisms
and small data caching.