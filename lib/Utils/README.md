# VNLib.Utils

A .NET 8 /C# library for common .NET operation and memory optimizations.  

### Builds & Feeds
Builds contain the individual components listed below packaged per-project, available for download on my website. Build packages will be tgz archives (except for nuget packages). You can obtain debug and release builds, along with per-project source code.  

### Docs and Guides
Docs and articles will be available from the docs link below. This library is quite large and will take time to develop docs. I heavily document all public APIs in XML so you can at-least get basic information when using this library.  

### License  
The software in this repository is licensed under the GNU GPL version 2.0 (or any later version). See the LICENSE files for more information.  

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
The [Utils.Memory](../Utils.Memory) namespace includes vendored and wrapped versions of recommended unmanaged heap allocator libraries to link at runtime.  
Read [this document](https://www.vaughnnugent.com/resources/software/articles?tags=docs&search=native+heap) for more information on vendored libraries and instructions on how to build them.  

## Other notes
Generally for internal library data structures that require memory allocation, a constructor override or a static method will consume a heap instance so you may pass your own heap instance or the Shared heap.  
