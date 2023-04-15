# VNLib.Net.Messaging.FBM.Server

Please see [FBM Protocol spec](../../#) for architecture details

## Usage
This library exports a main type called `FBMListener` it is the "entry point" so to speak for your server development. It listens on an active web-socket session that has been accepted an negotiated by your http server and passed to the `FBMListener.ListenAsync()` method. This call only returns once the connection is disconnected, has an error, or the session is closed. The listener listens for FBM messages on the web-socket, buffers them, and when message has been pre-processed, calls your request handler delegate if the message is valid. If the message exceeds the maximum size, the socket is gracefully closed and the method returns. 

Your `RequestHandler` delegate method accepts a type `FBMContext` that holds the request instance, and the `FBMResponseMessage` instance you will use to respond to the client. **Context instances are pooled, so you may not save them or any of their properties once your request handler returns.** Response objects implement the `IFBMMessage` interface, for your consumption. You may also use the "streaming" api, to reduce buffering and copying, implementing your own `IAsyncMessageBody` objects which allows reading data into a buffer asynchronously. Keep in mind a mutex is held while this streaming process occurs, and can cause performance issues, you should generally write a copy of your data to the internal response buffer.

A `FBMListenerSessionParams` structure must be passed on every call `ListenAsync()` to define the buffer sizes and limits per-session. 

The `FBMListenerBase` is an abstract type that provides some scaffolding for implementing your own message handler, by handling some plumbing and giving you an abstract method to process incoming messages. 

Calls to your `RequestHandler` method are invoked on a background queuing task, to avoid blocking, or delaying the receive task. However it is still task, not a background thread, so you should try **not** to synchronously block in your request handling routine to avoid blocking a threadpool thread.

## Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.