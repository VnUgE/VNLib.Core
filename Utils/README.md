# VNLib.Utils

A .NET/C# library for common .NET operation optimizations.

namespaces
- VNLib.Utils.Async - Provides classes for asynchronous access synchronization and some asynchronous collections
- VNLib.Utils.Extensions - Internal and external extensions for condensed common operations
- VNLib.Utils.IO - Memory focused data structures for IO operations
- VNLib.Utils.Logging - Logging interfaces for zero dependency logging
- VNLib.Utils.Memory - Utilities for safely accessing unmanaged memory and CLR memory
- VNLib.Utils.Memory.Caching - Data structures for managed object, data caching, and interfaces for cache-able objects


### Recommended 3rd Party Libs

This library does not *require* any direct dependencies, however there are some optional ones that are desired for higher performance code. This library does not, modify, contribute, or affect the functionality of any of the 3rd party libraries recommended below.

[**RPMalloc**](https://github.com/mjansson/rpmalloc) By Mattias Jansson - VNlib.Utils.Memory (and sub-classes) may load and bind function calls to this native library determined by environment variables. To use RPMalloc as the default unmanaged allocator simply add the dynamic library to the native lib search path, such as in the executable directory, and set the allocator environment variable as instructed below. 


### Allocator selection via environment variables
Valid allocator value for the `VNLIB_SHARED_HEAP_TYPE` environment variable:
- "win32" - for win32 based private heaps (only valid if using the Microsoft Windows operating system)
- "rpmalloc" - to load the RPMalloc native library if compiled for your platform
- none - the default value, will attempt to load the win32 private heap Kernel32 library, otherwise, the native ProcessHeap() cross platform allocator


## Usage
A usage breakdown would be far to lengthy for this library, and instead I intend to keep valid and comprehensive documentation in Visual Studio XML documentation files included in this project's src directory. 

This library is a utilities library and therefor may be directly included in your application or libraries, 

### License

The software in this repository is licensed under the GNU GPL version 2.0 (or any later version). 
See the LICENSE files for more information.