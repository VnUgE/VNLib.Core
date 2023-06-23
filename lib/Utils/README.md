# VNLib.Utils

A .NET 6 /C# library for common .NET operation and memory optimizations.  

### License  
The software in this repository is licensed under the GNU GPL version 2.0 (or any later version). See the LICENSE files for more information.  

### Documentation
Docs and articles will be available from the docs link below. This library is quite large and will take time to develop docs. I heavily document all public APIs in XML so you can at-least get basic information when using this library.

### Builds & Feeds
Builds contain the individual components listed below packaged per-project, available for download on my website. Build packages will be tgz archives (except for nuget packages). You can obtain debug and release builds, along with per-project source code.

### Links
[Home Page](https://www.vaughnnugent.com) - Website home page  
[Documentation](https://www.vaughnnugent.com/resources/software/articles?tags=docs,_VNLib.Utils) - Docs and articles for this project  
[Builds for VNLib.Core](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core) - Per-project build artifacts  
[Links for Nuget Feeds](https://www.vaughnnugent.com/resources/software/modules) - Get my NuGet feed links  

### Namespaces
- VNLib.Utils.Async - Provides classes for asynchronous access synchronization and some asynchronous collections
- VNLib.Utils.Extensions - Internal and external extensions for condensed common operations
- VNLib.Utils.IO - Memory focused data structures for IO operations
- VNLib.Utils.Logging - Logging interfaces for zero dependency logging
- VNLib.Utils.Memory - Utilities for safely accessing unmanaged memory and CLR memory
- VNLib.Utils.Memory.Caching - Data structures for managed object, data caching, and interfaces for cache-able objects
- VNLib.Utils.Memory.Diagnostics - Data structures for assisting in unmanaged memory diagnostics, and library wide memory diagnostics enablement
- VNLib.Utils.Native - Utilities for safely (dynamically) loading and accessing platform native libraries.
- VNLib.Utils.Resources - Abstractions and base data structures for holding and accessing resources.

## Recommended 3rd Party Libs
This library does not require any direct dependencies, however there are some optional ones that are recommended for higher performance. This library does not, modify, contribute, or affect the functionality of any of the 3rd party libraries recommended below.  

[**RPMalloc**](https://github.com/mjansson/rpmalloc) By Mattias Jansson - VNlib.Utils.Memory (and sub-classes) may load and bind function calls to this native library determined by environment variables. To use RPMalloc as the default unmanaged allocator simply add the dynamic library to the native lib search path, such as in the executable directory, and set the allocator environment variable as instructed below. I maintain a compatible Windows x64 [dll library](../WinRpMalloc/README.md) on my website and in this repository, that conforms to the [NativeHeap](../NativeHeapApi/README.md) api required for runtime loading.  

## Other notes
Generally for internal library data structures that require memory allocation, a constructor override or a static method will consume a heap instance so you may pass your own heap instance or the Shared heap.  
