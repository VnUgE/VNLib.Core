# VNLib.Core

*A Mono-repo for "core" VNLib projects for simplicity.*

### What is VNLib.Core?

VNLib is a collection of .NET/C# (mostly) libraries that I maintain for many private and public projects. VNLib.Core is a subset of the larger collection of projects I have named VNLib. This repo contains libraries I consider to be utility only, or building blocks that are individually useful for other projects. You will likely see many or most of them used across may other VNLib type projects. These libraries are meant to be stand-alone, meaning that there are no required* external dependencies (except the **[mscorelib](https://github.com/dotnet/runtime)**). For example the [VNLib.Utils](lib/Utils/#) library is a standalone, 0 required dependency library that is useful for, logging, common extensions, and a significant collection of memory related utilities. 

### How is the code for this repo maintained?
I use many internal tools to build and maintain these projects. I use [OneDev](https://code.onedev.io/) for my internal source control (as well as other git tools) and code updates are pushed to GitHub as part of a build process. I use [Task](https://taskfile.dev) in the build process to publish builds for all projects in this repository. Builds are publicly available on my [website](https://www.vaughnnugent.com/resources/software/modules) described in the builds section. I do *not* intend to expose my internal tools for security reasons. I prefer to keep my dependencies/processes internal, and will be relying on GitHub for as little as possible. 

### Dependencies 
Any libraries in this repository that contain external dependencies will be mentioned explicitly in the library's readme. I intend to limit this behavior, as it is the reason this repository exists.

### Licensing
Projects contained in this repository are individually licensed, either GNU GPL V2+ or GNU GPL Affero V3+. Builds contain the required license txt files in the archive. Reasoning: some projects are expected for server use, that's really about it, I prefer the the GPL v2 license for other applications where usage will be more broad. For example, the [Utils](lib/Utils/#) library is used for all sorts of projects. 


## Index/NameSpaces
**VNLib.**
- [Utils](lib/Utils/#) - A mutli-use library focused on reducing complexity for working with native resources, memory, asynchronous patterns and data-structures, object/resource pooling, critical resource handling, and common logging abstractions. 
- [NativeHeapApi](lib/NativeHeapApi/#) - A C implementation overview for building unmanaged heaps for use in the Utils.Memory.NativeHeap umanaged heap architecture.
- [Hashing.Portable](lib/Hashing.Portable/#) - Common cryptographic/hashing relate operations in a single package with room to grow. Also Argon2 bindings
- [Net.Http](lib/Net.Http/#) - High performance ^HTTP/1.1 application processing library, transport not included! For web or custom use, custom app layer protocols supported - roll your own web-sockets or SSE if you want! (See [Essentials](lib/Plugins.Essentials/#) library if you want to use mine! It's not bad.)
- [Net.Transport.SimpleTCP](lib/Net.Transport.SimpleTCP/#) - A not-so-simple, performance oriented, low/no allocation, .NET/C# native, TCP listener library using the System.IO.Pipelines architecture with SSL/TLS support.
- [Plugins.Essentials](lib/Plugins.Essentials/#) - Http processing layer library, provides essentials processing abstractions, extensions and data structures for quickly scaffolding extensible web/http based applications. Dynamic file processing, web sockets, stateful sessions, uses, accounts, permissions, dynamic file routing, and more.
- [Plugins](lib/Plugins/#) - Base/shared library for implementing runtime loaded plugins to provide extensibility and development isolation.
- [Plugins.Runtime](lib/Plugins.Runtime/#) - Scaffolding library for implementing and managing runtime loaded plugin (and unloading) instances safely in an application. Can be used standalone by an developer that wants a structured way of incorporating dynamically loaded plugins.
- [Plugins.Essentials.ServiceStack](lib/Plugins.Essentials.ServiceStack/#) - A library for scaffolding structured web applications from the individual http and supporting libraries into a completely managed http service stack for an entire application. 
- [Plugins.PluginBase](lib/Plugins.PluginBase/#) - Base library/api for plugin developers to build fully managed/supported runtime loaded plugins, without worrying about the plumbing, such as the IPlugin api, endpoint creation, and logging! This library is required if you wish to use most of the Plugin.Extensions libraries.
- [Net.Messaging.FBM](lib/Net.Messaging.FBM/#) - Fixed Buffer Messaging protocol, high performance, request/response architecture, client & server library, built atop http and web-sockets. As implied, relies on fixed sized internal buffers that are negotiated to transfer data with minimal overhead for known messaging architectures.
- [WinRpMalloc](lib/WinRpMalloc/#) - A Windows x64 dll project that exposes the rpmalloc memory allocator as a NativeHeap for .NET Utils library loading in the unmanned heap architecture.
- [Net.Rest.Client](lib/Net.Rest.Client/#) - A minimal library that provides a RestSharp client resource pool for concurrent usage with async support, along with an OAuth2 client credentials IAuthenticator implementation for use with Oauth2 plugins.

## Builds
Builds contain the individual components listed below packaged per-project, available for download on [**my website**](https://www.vaughnnugent.com/resources/software/modules). Builds are maintained manually for now until I build a testing pipeline. Build packages will be tar + gzipped (except for nuget packages). 

*All downloads will contain a sha384 hash of the file by adding a .sha384 to the desired file download, eg: debug.tgz.sha384*
*PGP signed downloads will be available eventually*

- Project source code (src.tgz)
- Nuget package (where applicable), debug w/ symbols & source + release (pkg/buildType/projName.version.nupkg)
- Debug build w/ symbols & xml docs (debug.tgz)
- Release build (release.tgz)

... Why sha348 you ask? It happened while developing my build tools, it's hard coded for now.

It is easier to get started on a project from source code by obtaining the source from my website, individually packages. Not that projects may contain references to each other locally, so you need to track dependencies. This just saves you from cloning 400 files from this repo if you only want to use one or two of the projects. 

### NuGet Feeds
NuGet feeds for release and debug packages are available on [my website](https://www.vaughnnugent.com/resources/software/modules) as the links are subject to change. Debug packages contain symbols and source code, and both contain XML documentation. 

## Notes
As with all of my VNlib projects, there are in a very early pre-release state. This is my first large-scale software engineering project and it its not in highest of priorities to be a good "software developer", sorry :smiley:. I care about the highest performance code I know how to make for my applications regardless of complexity. I use ALL of the packages/libraries in these projects in my own infrastructure and as issues appear I try to fix and test them before publishing the build packages on my website (see builds below). I understand that most of my projects are often re-inventing the wheel, I'm aware, I enjoy boilerplate because it's mine... This all subject to change without warning. 

### My code style
It changes when I learn new things, it will continue to evolve. I live on hard-mode, aka ADHD (surprise...), so I prefer verbosity and very heavy comments, recognition is better than recall. Comments may get outdated from time to time, sorry, again pattern recognition helps me remember what all 450k lines of code do at a glance (among all my repos at this stage).

### Why so many individual projects? - Motivation

A few years ago I wanted to use available building blocks from .NET and others to build my own HTTP server application, that didn't required the setup and maintenance of multiple processes and dependencies from dozens of sources and out-of date plugins/libraries that fell of the planet. (I came from C, then to PHP and that's where it all started). I found that some .NET projects had fallen behind due to the rise of ASP, as great as ASP.NET is (you should probably use it), it didn't give me the freedom I was looking for in development. I wasn't interested in 'web' applications but rather wire-speed protocols that could piggy-back more structured and well supported protocols, thus my adventure into the HTTP rfc's. Such an entertaining read btw, it will really keep you engaged... Anyway, I wanted libraries that had more freedom in layers. I wanted transport freedom and processing freedom. I simply wanted the HTTP library to do HTTP layer things, then present all the data to me, so I can use the protocol for a machine-machine task, or go as far as web processing. To do this with minimal overhead the [Utils](/lib/utils/#) library was created alongside to aid, and then is became useful in my other applications, with memory, async, text reading, buffering, native libraries etc. The Utils library is now the largest library, it may not grow larger, but it may become more 'open' so to speak. The Plugins.Essentials namspace and [library](lib/Plugins.Essentials) was added to provide the essential web processing layers. The essentials library provides the foundation for extensible web based processing, or pluggable HTTP processing in a convenient-to-develop manner. Then enter plugins, high performance sessions, page routing, user & account security abstractions, etc. Then we need the ability to actually construct that into an application process, enters the [Plugins.Runtime](lib/Plugins.Runtime/#) and the [HTTP Service Stack](lib/Plugins.Essentials.ServiceStack/#) libraries. Finally we need to facilitate a standalone transport for that 'batteries included' feel with [Net.Transport.SimpleTCP](lib/Net.Transport.SimpleTCP/#) which is a not-so-simple TCP library.

I prefer 'open', or less opinionated implementations. Less opinionated libraries come at a cost, in my opinion anyway, an architectural cost, foresight, complexity, and documentation. You are expected to understand every function of every data structure you consume, which isn't for newbies.  I also have a strong dislike for including projects that require many external dependencies for a simple feature/function. I would prefer the ability to include the least amount if code required to get the job done with the least amount of side-effects. Finally I prefer homogeneity in my projects so I tend to use .NET implementations opposed to 3rd party even if the implementation is not as performant for my use-cases. 

## Helping out 
I'm not currently looking for code contributors for this project and others, and NO contributions will be accepted through GitHub directly. I would prefer you contact me via my contact information on my website if possible. 

I am however interested in what you build with my libraries, especially web-plugins. You may consider viewing my plugin repos to see what I mean. Since I spend most of my time developing the core and extension libraries (and will continue to do) I don't have much time to focus on new plugin libraries used to build web functionality. Some ideas: content management, code documentation management, NuGet feed host (I have an simple internal one I built but its private for now), I **really** want a secure chat application, secure voice chat would be brilliant as well. If you have ideas or have built a plugin you want featured or reviewed, please feel free to get in touch.

### Contact info
Coming soon. Goto my [website](https://www.vaughnnugent.com/)