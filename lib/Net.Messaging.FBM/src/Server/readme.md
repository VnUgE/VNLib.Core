# VNLib.Net.Messaging.FBM.Server

Fixed Buffer Messaging Protocol server library. High performance statful messaging 
protocol built on top of HTTP web-sockets. Low/no allocation, completely asynchronous
while providing a TPL API. This library provides a simple asynchronous request/response 
architecture to web-sockets. This was initially designed to provide an alternative to 
complete HTTP request/response overhead, but allow a simple control flow for work 
across a network.

Messages consist of a 4 byte message id, a collection of headers, and a message body. 
The first 4 bytes of a message is the ID (for normal messages a signed integer greater than 0), 
0 is reserved for error conditions, and negative numbers are reserved for internal 
messages. Headers are identified by a single byte, followed by a variable length UTF8 
encoded character sequence, followed by a termination of 0xFF, 0xF1 (may change).

### Message structure
	4 byte positive (big endian signed 32-bit integer) message id
	2 byte termination
	1 byte header-id
	variable length UTF8 value
	2 byte termination
	-- other headers --
	2 byte termination (extra termination, ie: empty header)
	variable length payload
	(end of message is the end of the payload)


XML Documentation is or will be provided for almost all public exports. APIs are intended to 
be sensibly public and immutable to allow for easy extensability (via extension methods). I
often use extension libraries to provide additional functionality. (See cache library)

This library is likely a niche use case, and is probably not for everyone. Unless you care
about reasonably efficient high frequency request/response messaging, this probably isnt 
for you. This library provides a reasonable building block for distributed lock mechanisms
and small data caching.