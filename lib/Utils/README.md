# VNLib.Utils

A .NET 6 /C# library for common .NET operation and memory optimizations.

namespaces
- VNLib.Utils.Async - Provides classes for asynchronous access synchronization and some asynchronous collections
- VNLib.Utils.Extensions - Internal and external extensions for condensed common operations
- VNLib.Utils.IO - Memory focused data structures for IO operations
- VNLib.Utils.Logging - Logging interfaces for zero dependency logging
- VNLib.Utils.Memory - Utilities for safely accessing unmanaged memory and CLR memory
- VNLib.Utils.Memory.Caching - Data structures for managed object, data caching, and interfaces for cache-able objects
- VNLib.Utils.Memory.Diagnostics - Data structures for assisting in unmanaged memory diagnostics, and library wide memory diagnostics enablement
- VNLib.Utils.Native - Utilities for safely (dynamically) loading and accessing platform native libraries.
- VNLib.Utils.Resources - Abstractions and base data structures for holding and accessing resources.

#### Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my [website](https://www.vaughnnugent.com/resources/software). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.

### Recommended 3rd Party Libs
This library does not *require* any direct dependencies, however there are some optional ones that are desired for higher performance code. This library does not, modify, contribute, or affect the functionality of any of the 3rd party libraries recommended below.

[**RPMalloc**](https://github.com/mjansson/rpmalloc) By Mattias Jansson - VNlib.Utils.Memory (and sub-classes) may load and bind function calls to this native library determined by environment variables. To use RPMalloc as the default unmanaged allocator simply add the dynamic library to the native lib search path, such as in the executable directory, and set the allocator environment variable as instructed below. I maintain a compatible Windows x64 [dll library](../WinRpMalloc/README.md) on my website and in this repository, that conforms to the [NativeHeap](../NativeHeapApi/README.md) api required for runtime loading.

## Memory

### Native Memory details and allocator selection
Allocator selection has been updated to abstract the unmanaged heap loading to accommodate user defined memory allocators. A new class called the [NativeHeap](src/Memory/NativeHeap.cs) is the abstraction layer between your dll and the managed heap environment. 

There are two types of heap architectures that are in use within the Memory namespace. Global/Shared and Private/First Class. They generally make the following assumptions 

**Shared Heap** - Prefers performance over isolation, consumers expect calls to have minimal multi-threaded performance penalties at the cost of isolation.

**Private Heap** - Prefers isolation over performance, consumers assume calls may have multi-threaded performance penalties for the benefit of isolation. 

On heap creation, the `UnmanagedHeapDescriptor` structure's `HeapCreationFlags` will have the IsShared flag set if the desired heap is the global heap, or a private heap in the absence of said flag. By default **ALL** heaps are assumed **not** thread safe, and synchronization is provided by the `UnmanagedHeapBase` class, and as such, the UseSynchronization flag is always present when using the `InitializeNewHeapForProcess` method call. 

#### Note for heap implementers
Implementers may decide to clear the UseSynchronization flag if they know their implementation is thread safe, or are using their own synchronization method. Generally I would recommend letting the `UnmanagedHeapBase` class fallback to managed synchronization if your heap implementation requires synchronization, because the runtime *generally* provides the best performance for managed code (in my experience.) but you should do your own performance testing for your use case. 

Please use the [NativeHeapApi readme](../NativeHeapApi/README.md) for further instruction and implementation details. 

#### Note for consumers
The [MemoryUtil](src/Memory/MemoryUtil.cs) class allows for consumers to allocate a "private" heaps on demand or a global/shared heap for the library consumer (see AssemblyLoadContext for static class details). When creating private heaps the MemoryUtil class exposes a method called `InitializeNewHeapForProcess` which will create a new "private" heap on demand using the same configuration variables as the shared global heap (shared heap calls the same method internally). All heaps, private or shared are assumed to be **thread safe** when using this method. When calling `NativeHeap.LoadHeap` directly, you may disable thread safety and "roll your own" if you prefer. Understand that the heap implementation may override your requests, understand your heap's implementation if you choose to use this method. 

Most VNLib libraries use the Shared heap for performance reasons, and in some cases private heaps for isolation, such as HTTP or TCP libraries, however they generally prefer performance over isolation and will choose the highest performance implementation. 

### Runtime allocator selection
Setting the `VNLIB_SHARED_HEAP_FILE_PATH` environment variable will instruct the `InitializeNewHeapForProcess` method to load the native heap library at the given path, it may be an absolute path to the DLL file or a dll file name in a "safe" directory. It must conform to the NativeHeapApi, otherwise loading will fail. Understand that loading is deferred to the first access of the `MemoryUtil.Shared` property (this is subject to change) so don't be confused when seeing deferred debugging messages instead of a type initialization failure. This is also done to avoid Loader Lock contention issues, because we can. 

#### MemoryUtil fallbacks
If you don't want to use a custom heap implementation, the library has safe fallbacks for all platforms. On Windows the PrivateHeap api is used by default. The shared heap, again, prefers performance and will use the process heap returned from `GetProcessHeap()`, instead of creating a private heap that requires synchronization penalties. On all other platforms the fallback will be the .NET NativeMemory allocator, which is cross platform, but does **not** actually implement a "private" heap. So that means on non-Windows platforms unless you select your own heap, isolation is not an option. 

### Heap Diagnostics
The Memory.Diagnostics namespace was added to provide a wrapper for tracking IUnmanagedHeap memory allocations. Diagnostics can be enabled for the SharedHeap by setting the `VNLIB_SHARED_HEAP_DIAGNOSTICS` environment variable to "1". When enabled, calling `MemoryUtil.GetSharedHeapStats()` will return the heap's current statistics, otherwise an empty struct is returned. The Shared heap diagnostics are disabled by default.

### Other notes
Generally for internal library data structures that require memory allocation, a constructor override or a static method will consume a heap instance so you may pass your own heap instance or the Shared heap.

## Usage
A usage breakdown would be far to lengthy for this library, and instead I intend to keep valid and comprehensive documentation in Visual Studio XML documentation files included in this project's src directory. 

This library is a utilities library and therefor may be directly included in your application or libraries. 

### License

The software in this repository is licensed under the GNU GPL version 2.0 (or any later version). 
See the LICENSE files for more information.