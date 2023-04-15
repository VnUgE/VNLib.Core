# VNLib.Net.Messaging.FBM

High performance structured web-socket based asynchronous request/response messaging library for .NET.

[**Client Lib**](src/Client/#) - FBM Fixed Buffer Messaging client library

[**Server Lib**](src/Server/#) - FBM Fixed Buffer Messaging server/listener helper library

## Fixed Buffer Message protocol overview
FBM is a simple binary message protocol that supports asynchronous messaging between clients-servers that is built atop HTTP and web-sockets. It client and server architecture uses fixed sized buffers and assumes all messages sent/received will fit those buffers, this helps reduce copying and allocations. Streaming is minimally supported for clients but servers will still have a fixed max message size, so it cannot stream arbitrary length data. Servers will always buffer request messages into memory, so a max message size is the only guard for resource abuse.

#### FBM Message frames
Message frames consist of a 4-byte message id, a collection of key-value encoded headers, and a message body/payload. The first 4 bytes of a message is the ID (for normal messages a signed integer greater than 0), 0 is reserved for error conditions, and negative numbers are reserved for internal messages. Headers are identified by a single byte, followed by a variable length UTF8 encoded character sequence, followed by a termination of 0xFF, 0xF1 (may change).
```
	4 byte positive (big endian signed 32-bit integer) message id
	2 byte termination [0xFF,0xF1]
	1 byte header-id
	variable length UTF8 value
	2 byte termination [0xFF,0xF1]
	<-- other headers -->
	2 byte termination (extra termination, ie: empty header) [0xFF,0xF1]
	variable length payload
	<end of message is the end of the payload>
Example:
id=0x8EB26                  term       Action cmd        utf8 "read"             End header    End header section
[0x00,0x08, 0xEB, 0x26,    0xFF, 0xF1,      0x04,    0x72, 0x65, 0x61, 0x64,     0xFF, 0xF1,    0xFF, 0xF1]

```

Buffer sizes are generally negotiated on initial web-socket upgrade, so to buffer entire messages in a single read/write from the web-socket. Received messages are read into memory until the web-socket has no more data available. The message is then parsed and passed for processing on the server side, or complete a pending request on the client side. Servers may drop the web-socket connection and return an error if messages exceed the size of the pre-negotiated buffer. Servers should validate buffer sizes before accepting a connection.

#### Architecture goal
The goal of the FBM protocol for is to provide efficient use of resources (memory, network, and minimize GC load) to transfer small messages truly asynchronously, at wire speeds, with only web-socket and transport overhead. Using web-sockets simplifies implementation, and allows compatibility across platforms, programming languages, and versions.

## Final Notes
This library is likely a niche use case, and is probably not for everyone. Unless you care about reasonably efficient high frequency  request/response messaging, this probably isn't for you. This library provides a reasonable building block for distributed lock mechanisms and small data caching.

## License
The software in this repository is licensed under the GNU Affero General Public License (or any later version). See the [LICENSE](LICENSE.txt) file for more information.

## Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.